using System.Collections.Generic;
using UnityEngine;

public sealed class RisingBoard
{
    public const float Spacing=2.32f;
    public const float CollapseLead=2.6f;
    readonly Dictionary<Vector2Int,int> indices=new Dictionary<Vector2Int,int>();
    readonly Vector2Int[] cells=new Vector2Int[100];
    readonly Camera camera;
    readonly Vector3 home, aim;
    public SpikeTile[] Tiles { get; }=new SpikeTile[100];
    public int Width { get; private set; }
    public int Count => Width*Width;
    public int Radius => Width/2;
    public int NextCount => (Width-1)*(Width-1);
    public float Center => Width%2==0 ? Spacing*.5f : 0;
    readonly List<int> lifts=new List<int>();
    readonly int[] liftRisk=new int[100];
    public int LiftPattern {get;private set;}
    public bool LiftHorizontal=>LiftPattern%2==0;
    public int LiftRisk(int index)=>liftRisk[index];
    public bool Lowered {get;private set;}
    public bool OnLift(int cell)=>lifts.Contains(cell);
    public void PlanLift(int origin,int pattern=0){
        lifts.Clear();System.Array.Clear(liftRisk,0,liftRisk.Length);LiftPattern=pattern%4;
        int lo=-(Width-1)/2;
        for(int i=0;i<Count;i++){
            var c=cells[i];int x=c.x-lo,y=c.y-lo;
            bool down=LiftPattern==0?x%3!=2:LiftPattern==1?y%3!=2:
                LiftPattern==2?(x/2+y/2)%2==0:(x+y)%3!=2;
            if(!down)continue;
            lifts.Add(i);liftRisk[i]=(LiftHorizontal?y:x)%3;
        }
        PreviewLift(0);
    }
    public void PreviewLift(float t){foreach(int i in lifts)Tiles[i].ShowWarning(t,-4-liftRisk[i]);}
    public void LiftHeight(float depth){
        Lowered=true;
        foreach(int i in lifts)SetHeight(i,-2.8f*Mathf.SmoothStep(0,1,depth));
    }
    public void RestoreLift(){foreach(int i in lifts){SetHeight(i,0);Tiles[i].warning.enabled=false;}lifts.Clear();Lowered=false;}
    public int At(int x,int y)=>indices[new Vector2Int(x,y)];
    int fallingEnd;
    float fallAge;

    public RisingBoard(SpikeTile[] starter, Camera view)
    {
        camera=view;home=view.transform.position;aim=starter[4].transform.position+Vector3.up*.15f;
        int index=0;
        for(int width=3;width<=10;width++){
            int min=-(width-1)/2, max=width/2;
            for(int z=min;z<=max;z++)for(int x=min;x<=max;x++){
                var cell=new Vector2Int(x,z);
                if(indices.ContainsKey(cell))continue;
                var tile=width==3 ? starter[index] : Object.Instantiate(starter[0],starter[0].transform.parent);
                cells[index]=cell;indices.Add(cell,index);
                tile.transform.localPosition=new Vector3(x*Spacing,0,z*Spacing);
                Tiles[index++]=tile;
            }
        }
    }
    public Vector2Int Cell(int index)=>cells[index];
    public int Neighbor(int index,Vector2Int dir)=>indices.TryGetValue(cells[index]+dir,out int n)&&n<Count && (!Lowered||OnLift(index)==OnLift(n)) ? n : -1;
    public int Nearest(Vector3 position){
        int result=0;float distance=float.PositiveInfinity;
        for(int i=0;i<Count;i++){
            float d=(position-Tiles[i].transform.position).sqrMagnitude;
            if(d<distance){result=i;distance=d;}
        }
        return result;
    }
    Vector3 CameraPosition()=>aim+(home-aim)*(1+(Width-3)*.34f)+new Vector3(Center,0,Center);
    public void Reset(){
        RestoreLift();Width=10;fallingEnd=0;camera.transform.position=CameraPosition();
        for(int i=0;i<Tiles.Length;i++){
            Tiles[i].gameObject.SetActive(true);
            SetHeight(i,0);
        }
    }
    void SetHeight(int i,float y){
        var cell=cells[i];Tiles[i].transform.localPosition=new Vector3(cell.x*Spacing,y,cell.y*Spacing);
    }
    public void Preview(float progress){
        for(int i=NextCount;i<Count;i++)Tiles[i].ShowWarning(progress,-2);
    }
    public void Collapse(){
        fallingEnd=Count;Width--;fallAge=0;
    }
    public void Animate(float dt){
        if(fallingEnd==0)return;
        fallAge+=dt;
        float t=Mathf.Clamp01(fallAge/.68f);
        for(int i=Count;i<fallingEnd;i++){
            SetHeight(i,-12*t*t);
            if(t>=1)Tiles[i].gameObject.SetActive(false);
        }
        camera.transform.position=Vector3.Lerp(camera.transform.position,CameraPosition(),t>=1 ? 1 : Mathf.Min(1,dt*6));
        if(t>=1)fallingEnd=0;
    }
}
