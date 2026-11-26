using UnityEngine;

public sealed class ChamberAnimalAvatar
{
    static readonly string[] Animals = { "Bear", "Rabbit", "Frog", "Cat" };
    // Height of each retargeted idle pose's soles above the gameplay prefab pivot, in tile-space units.
    static readonly float[] SoleHeights = { .689f, .861f, .855f, .694f };
    static readonly int Moving = Animator.StringToHash("Moving");
    static readonly int Falling = Animator.StringToHash("Falling");
    readonly Transform anchor;
    readonly MaterialPropertyBlock ink = new MaterialPropertyBlock();
    GameObject model;
    Renderer[] surfaces;
    Animator animator;
    bool running;
    bool falling;
    int currentAnimal = -1;
    public string CharacterId => Animals[currentAnimal];
    Color currentColor;

    public ChamberAnimalAvatar(Transform anchor, Transform sceneBear = null)
    {
        this.anchor = anchor;
        if (sceneBear)
        {
            sceneBear.SetParent(anchor, false);
            model = sceneBear.gameObject;
            BindModel(0);
        }
    }

    void BindModel(int animal)
    {
        currentAnimal = animal;
        model.transform.localPosition += Vector3.down * SoleHeights[animal];
        surfaces = model.GetComponentsInChildren<Renderer>();
        animator = model.GetComponentInChildren<Animator>();
        if (animator && animator.runtimeAnimatorController)
        {
            animator.SetBool(Moving, running);
            animator.SetBool(Falling, falling);
        }
    }

    public void SetRunning(bool moving)
    {
        if (running == moving) return;
        running = moving;
        if (animator && animator.runtimeAnimatorController)
            animator.SetBool(Moving, running);
    }

    public void SetFalling(bool value)
    {
        if (falling == value) return;
        falling = value;
        if (animator && animator.runtimeAnimatorController)
            animator.SetBool(Falling, falling);
    }

    public void Show(ChamberProfile profile, int seat)
    {
        int animal = profile.Animal == 0 ? seat : profile.Animal - 1;
        Color color = Color.Lerp(Color.white, profile.Tint(seat), profile.Swatch == 0 ? .12f : .60f);
        if (currentAnimal == animal && currentColor == color) return;
        if (currentAnimal != animal)
        {
            var prefab = Resources.Load<GameObject>("ChamberAnimals/" + Animals[animal]);
            if (model)
            {
                model.SetActive(false);
                Object.Destroy(model);
            }
            model = Object.Instantiate(prefab, anchor, false);
            BindModel(animal);
        }
        ink.SetColor("_Color", color);
        foreach (var surface in surfaces) surface.SetPropertyBlock(ink);
        currentColor = color;
    }
}
