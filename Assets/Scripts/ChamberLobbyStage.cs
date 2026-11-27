using UnityEngine;

// A front-facing view of the same explorers used on the board.
public sealed class ChamberLobbyStage
{
    const float Spacing = 2.2f;
    readonly GameObject stage = new GameObject("Party lobby");
    readonly Camera camera;
    readonly Transform[] models = new Transform[ChamberLan.Capacity];
    readonly ChamberAnimalAvatar[] avatars = new ChamberAnimalAvatar[ChamberLan.Capacity];
    readonly Transform[] vacancySigns = new Transform[ChamberLan.Capacity];
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
        view.transform.localPosition = new Vector3(0,1.8f,-10);
        view.transform.LookAt(stage.transform.position + Vector3.up * .35f);
        camera = view.AddComponent<Camera>();
        camera.depth = game.chamberCamera.depth + 1;
        camera.farClipPlane = 150;
        camera.clearFlags = CameraClearFlags.Skybox;
        var originalSea=game.tiles[0].transform.parent.Find("Coastal Sea");
        var sea=Object.Instantiate(originalSea,stage.transform,false);
        sea.localPosition=new Vector3(0,-.28f,0);
        sea.localScale=new Vector3(360,360,1);
        for (int i=0;i<models.Length;i++)
        {
            float x = (i-(models.Length-1)*.5f)*Spacing;
            rims[i] = Platform(x,-.09f,1.78f,.085f);
            var top = Platform(x,0,1.70f,.075f);
            ink.SetColor("_Color",new Color(.47f,.71f,.32f));
            top.SetPropertyBlock(ink);
            var sign=new GameObject("Vacant seat").transform;
            sign.SetParent(stage.transform,false);
            sign.localPosition=new Vector3(x,top.bounds.max.y,.08f);
            vacancySigns[i]=sign;
            SignPart(sign,PrimitiveType.Cube,new Vector3(0,.26f,0),new Vector3(.09f,.52f,.09f),new Color32(158,110,68,255));
            var plaque=SignPart(sign,PrimitiveType.Cylinder,new Vector3(0,.66f,0),new Vector3(.70f,.065f,.70f),PartyMenuUI.Teal);
            plaque.localRotation=Quaternion.Euler(90,0,0);
            SignPart(sign,PrimitiveType.Cube,new Vector3(0,.66f,-.08f),new Vector3(.34f,.08f,.035f),PartyMenuUI.Paper);
            SignPart(sign,PrimitiveType.Cube,new Vector3(0,.66f,-.08f),new Vector3(.08f,.34f,.035f),PartyMenuUI.Paper);
            var actor = new GameObject($"Seat {i+1}").transform;
            actor.SetParent(stage.transform,false);
            actor.position = new Vector3(top.bounds.center.x,top.bounds.max.y,top.bounds.center.z);
            actor.localRotation = Quaternion.Euler(0,180,0);
            models[i] = actor;
            avatars[i] = new ChamberAnimalAvatar(actor);
        }
    }

    Transform SignPart(Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Color color)
    {
        var piece=GameObject.CreatePrimitive(type);
        Object.Destroy(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent,false);
        piece.transform.localPosition=position;
        piece.transform.localScale=scale;
        ink.SetColor("_Color",color);
        piece.GetComponent<Renderer>().SetPropertyBlock(ink);
        return piece.transform;
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
        camera.fieldOfView=Mathf.Max(30f,Camera.HorizontalToVerticalFieldOfView(50f,camera.aspect));
        for (int i=0;i<models.Length;i++)
        {
            bool occupied = i<room.OccupiedSeats;
            var model = models[i];
            model.gameObject.SetActive(occupied);
            vacancySigns[i].gameObject.SetActive(!occupied);
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
