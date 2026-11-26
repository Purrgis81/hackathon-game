using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TidalWave : IDisposable
{
    public const float WarningSeconds=2.2f;
    readonly Transform root,crest,guide;
    readonly Mesh mesh;
    readonly Material water,preview;
    readonly MaterialPropertyBlock ink=new MaterialPropertyBlock();
    readonly Renderer path;
    Vector3 start,forward;
    float span,distance,speed,startsAt=float.PositiveInfinity;
    bool armed;
    public int Columns {get;private set;}
    public bool Horizontal {get;private set;}
    public int Sign {get;private set;}
    public int Signature {get;private set;}
    public Vector2Int PushDirection=>Horizontal?new Vector2Int(Sign,0):new Vector2Int(0,Sign);
    public float TravelSeconds=>distance/speed;
    public Vector3 Position(float age)=>start+forward*(speed*Mathf.Clamp(age,0,TravelSeconds));
    public TidalWave(Transform parent,Mesh quad){
        root=new GameObject("Crossing tidal wave").transform;root.SetParent(parent,false);
        water=new Material(Resources.Load<Shader>("Environment/Water/Shaders/TidalWater"));
        preview=new Material(Resources.Load<Shader>("TidalGuide"));
        crest=new GameObject("Curled water and foam").transform;crest.SetParent(root,false);
        mesh=MakeCrest();crest.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=crest.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=water;r.shadowCastingMode=ShadowCastingMode.Off;
        guide=new GameObject("Wave path and direction").transform;guide.SetParent(root,false);
        guide.localRotation=Quaternion.Euler(90,0,0);guide.gameObject.AddComponent<MeshFilter>().sharedMesh=quad;
        path=guide.gameObject.AddComponent<MeshRenderer>();path.sharedMaterial=preview;path.shadowCastingMode=ShadowCastingMode.Off;
        Clear();
    }
    static Mesh MakeCrest(){
        var profile=new[]{new Vector2(-.92f,.025f),new Vector2(-.80f,.38f),new Vector2(-.54f,1.05f),
            new Vector2(-.08f,1.56f),new Vector2(.35f,1.70f),new Vector2(.64f,1.47f),
            new Vector2(.57f,1.16f),new Vector2(.29f,1.04f),new Vector2(.19f,.76f),new Vector2(.71f,.025f)};
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        const int across=16;
        for(int x=0;x<=across;x++)for(int p=0;p<profile.Length;p++){
            float u=x/(float)across;
            vertices.Add(new Vector3(u-.5f,profile[p].y,profile[p].x));
            uv.Add(new Vector2(u,p/(float)(profile.Length-1)));
            if(x==0||p==0)continue;
            int n=x*profile.Length+p,a=n-profile.Length;
            triangles.AddRange(new[]{a-1,a,n,a-1,n,n-1});
        }
        var result=new Mesh{name="Faceted curled wave"};result.SetVertices(vertices);result.SetUVs(0,uv);
        result.SetTriangles(triangles,0);result.RecalculateNormals();return result;
    }
    public void Plan(RisingBoard board,int origin,float pressure,int cast){
        Clear();armed=true;
        Horizontal=UnityEngine.Random.Range(0,2)==0;Sign=UnityEngine.Random.Range(0,2)==0?-1:1;
        int max=Mathf.Min(board.Width-1,2+Mathf.RoundToInt(pressure*7));
        Columns=cast==0?1:UnityEngine.Random.Range(1,max+1);
        int lo=-(board.Width-1)/2,hi=board.Width/2;
        var cell=board.Cell(origin);
        int first=Mathf.Clamp((Horizontal?cell.y:cell.x)-Columns/2,lo,hi-Columns+1);
        float cross=(first+(Columns-1)*.5f)*RisingBoard.Spacing;
        float edge=Sign>0?lo*RisingBoard.Spacing-1.8f:hi*RisingBoard.Spacing+1.8f;
        forward=(Horizontal?Vector3.right:Vector3.forward)*Sign;
        start=Horizontal?new Vector3(edge,.035f,cross):new Vector3(cross,.035f,edge);
        distance=(board.Width-1)*RisingBoard.Spacing+3.6f;span=Columns*RisingBoard.Spacing-.18f;
        speed=Mathf.Lerp(18f,25f,pressure);
        crest.rotation=Quaternion.LookRotation(forward);crest.localScale=new Vector3(span,1,1);
        guide.position=Horizontal?new Vector3(board.Center,.06f,cross):new Vector3(cross,.06f,board.Center);
        guide.rotation=Quaternion.LookRotation(Vector3.down,forward);
        guide.localScale=new Vector3(span,distance,1);
        Signature=((((Horizontal?1:0)*2+(Sign>0?1:0))*11+Columns)*11+first);
        Draw(0);
    }
    public void Fire(float now){startsAt=now;path.enabled=false;}
    public float Contact(Vector3 from,Vector3 to,float now,float slice){
        if(!armed||now+slice<startsAt||now>=startsAt+TravelSeconds)return float.PositiveInfinity;
        float a=slice>0?Mathf.Clamp01((startsAt-now)/slice):0;
        float b=slice>0?Mathf.Clamp01((startsAt+TravelSeconds-now)/slice):1;
        var inverse=Quaternion.Inverse(crest.rotation);
        var p=inverse*(Vector3.Lerp(from,to,a)-Position(now+a*slice-startsAt));
        var q=inverse*(Vector3.Lerp(from,to,b)-Position(now+b*slice-startsAt));
        float hit=TrapContact.Entry(p,q,span*.5f,.72f);
        return hit<=1?a+(b-a)*hit:float.PositiveInfinity;
    }
    public void Draw(float now){
        if(!armed)return;
        bool warning=float.IsPositiveInfinity(startsAt);
        float age=warning?0:now-startsAt;
        crest.gameObject.SetActive(age<TravelSeconds);
        crest.position=Position(age);
        crest.localScale=new Vector3(span,warning?.25f:1,1);
        path.enabled=warning;
        ink.SetFloat("_Span",Columns);ink.SetFloat("_Length",distance/RisingBoard.Spacing);
        path.SetPropertyBlock(ink);
        water.SetFloat("_Fade",warning?.58f:1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(TravelSeconds-.24f,TravelSeconds,age)));
    }
    public void Clear(){armed=false;Signature=0;startsAt=float.PositiveInfinity;crest.gameObject.SetActive(false);path.enabled=false;}
    public void Dispose(){UnityEngine.Object.Destroy(mesh);UnityEngine.Object.Destroy(water);UnityEngine.Object.Destroy(preview);UnityEngine.Object.Destroy(root.gameObject);}
}
