using UnityEngine;

public sealed class ChamberAnimalAvatar
{
    static readonly string[] Animals = { "Bear", "Rabbit", "Frog", "Cat" };
    readonly Transform anchor;
    readonly MaterialPropertyBlock ink = new MaterialPropertyBlock();
    GameObject model;
    Renderer[] surfaces;
    int currentAnimal = -1;
    Color currentColor;

    public ChamberAnimalAvatar(Transform anchor, Transform sceneBear = null)
    {
        this.anchor = anchor;
        if (sceneBear)
        {
            sceneBear.SetParent(anchor, false);
            model = sceneBear.gameObject;
            surfaces = model.GetComponentsInChildren<Renderer>();
            currentAnimal = 0;
        }
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
            surfaces = model.GetComponentsInChildren<Renderer>();
            currentAnimal = animal;
        }
        ink.SetColor("_Color", color);
        foreach (var surface in surfaces) surface.SetPropertyBlock(ink);
        currentColor = color;
    }
}
