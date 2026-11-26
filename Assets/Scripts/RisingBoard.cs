using System.Collections.Generic;
using UnityEngine;

public sealed class RisingBoard
{
    public const float Spacing=2.32f;
    public const float CollapseLead=2.6f;
    readonly Dictionary<Vector2Int,int> indices=new Dictionary<Vector2Int,int>();
    readonly Vector2Int[] cells=new Vector2Int[100];
    readonly bool[] temporaryMissing=new bool[100];
    readonly Quaternion[] homeRotations=new Quaternion[100];
    readonly Camera camera;
    readonly Renderer sea;
    readonly Renderer[] floors = new Renderer[100];
    readonly MaterialPropertyBlock waterProperties = new MaterialPropertyBlock();
    readonly Vector4[] impactTiles = new Vector4[19];
    readonly Vector3 home, aim;
    public SpikeTile[] Tiles { get; }=new SpikeTile[100];
    public int Width { get; private set; }
    public int Count => Width*Width;
    public int Radius => Width/2;
    public int NextCount => (Width-1)*(Width-1);
    public float Center => Width%2==0 ? Spacing*.5f : 0;
    public int At(int x,int y)=>indices[new Vector2Int(x,y)];
    int fallingEnd;
    float fallAge;

    public RisingBoard(SpikeTile[] starter, Camera view)
    {
        camera=view;home=view.transform.position;aim=starter[4].transform.position+Vector3.up*.15f;
        sea=starter[0].transform.parent.Find("Coastal Sea").GetComponent<Renderer>();
        int index=0;
        for(int width=3;width<=10;width++){
            int min=-(width-1)/2, max=width/2;
            for(int z=min;z<=max;z++)for(int x=min;x<=max;x++){
                var cell=new Vector2Int(x,z);
                if(indices.ContainsKey(cell))continue;
                var tile=width==3 ? starter[index] : Object.Instantiate(starter[0],starter[0].transform.parent);
                cells[index]=cell;indices.Add(cell,index);
                tile.transform.localPosition=new Vector3(x*Spacing,0,z*Spacing);
                floors[index]=tile.transform.Find("floor").GetComponent<Renderer>();
                Tiles[index]=tile;homeRotations[index]=tile.transform.localRotation;index++;
            }
        }
    }
    public Vector2Int Cell(int index)=>cells[index];
    public bool IsPresent(int index)=>index>=0&&index<Count&&!temporaryMissing[index];
    public int Neighbor(int index,Vector2Int dir)=>IsPresent(index)&&indices.TryGetValue(cells[index]+dir,out int n)&&n<Count&&IsPresent(n) ? n : -1;
    public void SetTemporaryMissing(int index,bool missing){
        if(index<0||index>=Tiles.Length)return;
        temporaryMissing[index]=missing;
        if(missing){Tiles[index].ResetTile();Tiles[index].gameObject.SetActive(false);}
        else{SetTemporaryPose(index,Vector3.zero,0,Vector3.zero);Tiles[index].gameObject.SetActive(index<Count);}
    }
    public void SetTemporaryPose(int index,Vector3 offset,float height,Vector3 euler){
        if(index<0||index>=Count||temporaryMissing[index])return;
        var cell=cells[index];Tiles[index].transform.localPosition=new Vector3(cell.x*Spacing+offset.x,height+offset.y,cell.y*Spacing+offset.z);
        Tiles[index].transform.localRotation=homeRotations[index]*Quaternion.Euler(euler);
    }
    public int Nearest(Vector3 position){
        int result=0;float distance=float.PositiveInfinity;
        for(int i=0;i<Count;i++){
            float d=(position-Tiles[i].transform.position).sqrMagnitude;
            if(d<distance){result=i;distance=d;}
        }
        return result;
    }
    Vector3 CameraPosition()=>aim+(home-aim)*(1+(Width-3)*.34f)+new Vector3(Center,0,Center);
    public void Reset()=>Resize(10);
    public void Resize(int width){
        waterProperties.Clear();
        System.Array.Clear(temporaryMissing,0,temporaryMissing.Length);Width=Mathf.Clamp(width,3,10);fallingEnd=0;fallAge=0;camera.transform.position=CameraPosition();
        for(int i=0;i<Tiles.Length;i++){
            Tiles[i].gameObject.SetActive(i<Count);
            SetHeight(i,0);
            Tiles[i].transform.localRotation=homeRotations[i];
        }
        DressCoast();
        SetShoreBounds();
        sea.SetPropertyBlock(waterProperties);
    }
    void SetShoreBounds(){
        Bounds footprint=floors[0].bounds;
        for(int i=1;i<Count;i++) footprint.Encapsulate(floors[i].bounds);
        waterProperties.SetVector("_ShoreBounds",new Vector4(footprint.center.x,footprint.center.z,
            footprint.extents.x,footprint.extents.z));
        waterProperties.SetFloat("_ShoreHeight",footprint.max.y-sea.transform.position.y);
    }
    void DressCoast(){
        for(int i=0;i<Count;i++) DressEdge(Tiles[i].transform, cells[i], Width);
    }
    public static void DressEdge(Transform tile, Vector2Int cell, int width){
        int lo=-(width-1)/2, x=cell.x-lo, z=cell.y-lo, end=width-1;
        var floor=tile.Find("floor");
        floor.GetComponent<ParkFloor>().Fit(x,z,end);
        var fringe=floor.Find("Fringe");
        if(!fringe)return;
        int along;
        Vector2Int outward;
        if(z==0){along=x;outward=Vector2Int.down;}
        else if(x==end){along=end+z;outward=Vector2Int.right;}
        else if(z==end){along=3*end-x;outward=Vector2Int.up;}
        else if(x==0){along=4*end-z;outward=Vector2Int.left;}
        else{fringe.gameObject.SetActive(false);return;}
        bool decorated=along%3==1;
        fringe.gameObject.SetActive(decorated);
        if(!decorated)return;
        float lateral=along%2==0 ? .24f : -.24f;
        fringe.localPosition=new Vector3(.88f*outward.x+lateral*outward.y,-.015f,.88f*outward.y-lateral*outward.x);
        fringe.localRotation=Quaternion.Euler(0,along*47,0);
        bool grass=(along/3)%2==0;
        fringe.Find("Grass").gameObject.SetActive(grass);
        fringe.Find("Rock").gameObject.SetActive(!grass);
    }
    void SetHeight(int i,float y){
        var cell=cells[i];Tiles[i].transform.localPosition=new Vector3(cell.x*Spacing,y,cell.y*Spacing);
    }
    public void Preview(float progress){
        for(int i=NextCount;i<Count;i++)Tiles[i].ShowWarning(progress,-2);
    }
    public void Collapse(){
        System.Array.Clear(temporaryMissing,0,temporaryMissing.Length);fallingEnd=Count;Width--;fallAge=0;
        DressCoast();
    }
    public void Animate(float dt){
        if(fallingEnd==0)return;
        fallAge+=dt;
        float t=Mathf.Clamp01(fallAge/.68f);
        int contacts=0;
        float waterY=sea.transform.position.y;
        for(int i=Count;i<fallingEnd;i++){
            float previousY=Tiles[i].transform.position.y;
            SetHeight(i,-12*t*t);
            Bounds footprint=floors[i].bounds;
            float previousBottom=footprint.min.y+previousY-Tiles[i].transform.position.y;
            if(previousBottom>waterY && footprint.min.y<=waterY)
                impactTiles[contacts++]=new Vector4(footprint.center.x,footprint.center.z,
                    footprint.extents.x,footprint.extents.z);
            if(t>=1)Tiles[i].gameObject.SetActive(false);
        }
        if(contacts>0){
            waterProperties.SetVectorArray("_ImpactTiles",impactTiles);
            waterProperties.SetInteger("_ImpactCount",contacts);
            waterProperties.SetFloat("_ImpactStart",Time.timeSinceLevelLoad);
        }
        if(contacts>0||t>=1){
            SetShoreBounds();
            sea.SetPropertyBlock(waterProperties);
        }
        camera.transform.position=Vector3.Lerp(camera.transform.position,CameraPosition(),t>=1 ? 1 : Mathf.Min(1,dt*6));
        if(t>=1)fallingEnd=0;
    }
}
