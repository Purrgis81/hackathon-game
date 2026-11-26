using UnityEngine;

// A front-facing view of the same explorers used on the board.
public sealed class ChamberLobbyStage
{
    const float Spacing = 2.2f;
    readonly GameObject stage = new GameObject("Party lobby");
    readonly Camera camera;
    readonly Transform[] models = new Transform[ChamberLan.Capacity];
    readonly ChamberAnimalAvatar[] avatars = new ChamberAnimalAvatar[ChamberLan.Capacity];
    readonly Renderer[] rims = new Renderer[ChamberLan.Capacity];
    readonly Color[] previousRims = new Color[ChamberLan.Capacity];
    readonly MaterialPropertyBlock ink = new MaterialPropertyBlock();
    static readonly Color Empty = new Color(.20f,.32f,.36f);
    static readonly Color Waiting = new Color(.48f,.50f,.54f);
    static readonly Color Ready = new Color(.24f,1f,.60f);
    static readonly Color Host = new Color(1f,.74f,.29f);

    public ChamberLobbyStage(ChamberGame game)
    {
        stage.transform.SetParent(game.transform,false);
        stage.transform.position = Vector3.right * 2000;
        var view = new GameObject("Lobby camera");
        view.transform.SetParent(stage.transform,false);
        view.transform.localPosition = new Vector3(0,3.4f,-9);
        view.transform.LookAt(stage.transform.position + Vector3.up * .55f);
        camera = view.AddComponent<Camera>();
        camera.depth = game.chamberCamera.depth + 1;
        camera.farClipPlane = 20;
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.057f,.085f,.11f);
        for (int i=0;i<models.Length;i++)
        {
            float x = (i-(models.Length-1)*.5f)*Spacing;
            rims[i] = Platform(x,-.09f,1.78f,.085f);
            var top = Platform(x,0,1.70f,.045f);
            ink.SetColor("_Color",new Color(.17f,.22f,.25f));
            top.SetPropertyBlock(ink);
            var actor = new GameObject($"Seat {i+1}").transform;
            actor.SetParent(stage.transform,false);
            actor.position = new Vector3(top.bounds.center.x,top.bounds.max.y,top.bounds.center.z);
            actor.localRotation = Quaternion.Euler(0,180,0);
            models[i] = actor;
            avatars[i] = new ChamberAnimalAvatar(actor);
        }
    }

    Renderer Platform(float x,float y,float diameter,float halfHeight)
    {
        var disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disk.transform.SetParent(stage.transform,false);
        disk.transform.localPosition = new Vector3(x,y,0);
        disk.transform.localScale = new Vector3(diameter,halfHeight,diameter);
        Object.Destroy(disk.GetComponent<Collider>());
        return disk.GetComponent<Renderer>();
    }

    public float SeatX(int slot) => camera.WorldToViewportPoint(models[slot].position).x;
    public void Hide() { if(stage.activeSelf) stage.SetActive(false); }

    public void Show(ChamberLan room)
    {
        stage.SetActive(true);
        camera.orthographicSize = Mathf.Max(2.4f,4.6f/camera.aspect);
        for (int i=0;i<models.Length;i++)
        {
            bool occupied = i<room.OccupiedSeats;
            var model = models[i];
            model.gameObject.SetActive(occupied);
            if (occupied) avatars[i].Show(room.Profile(i),i);
            var rim = !occupied ? Empty : i==0 ? Host : room.IsSeatReady(i) ? Ready : Waiting;
            if(previousRims[i]!=rim){
                previousRims[i]=rim;
                ink.SetColor("_Color",rim);
                rims[i].SetPropertyBlock(ink);
            }
        }
    }
}
