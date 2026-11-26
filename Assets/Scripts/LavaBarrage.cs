using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class LavaBarrage : IDisposable
{
    public const float Lead=2.8f,Hot=.18f,Beat=.72f;
    const int Capacity=8;
    readonly Transform root;
    readonly Transform[] stones=new Transform[Capacity],marks=new Transform[Capacity];
    readonly Transform[,] sparks=new Transform[Capacity,6];
    readonly Renderer[] surfaces=new Renderer[Capacity];
    readonly MaterialPropertyBlock ink=new MaterialPropertyBlock();
    readonly int[] targets=new int[Capacity];
    readonly bool[] sounded=new bool[Capacity];
    readonly Material targetInk,rock;
    float started=-100;
    float beat=Beat;
    public float WarningSeconds {get;private set;}=Lead;
    int count;
    Vector3 vent;
    RisingBoard board;
    public bool Active {get;private set;}
    public int Count=>count;
    public int Signature {get;private set;}
    public LavaBarrage(Transform parent,Mesh quad){
        root=new GameObject("Volcanic barrage").transform;root.SetParent(parent,false);
        targetInk=new Material(Resources.Load<Shader>("LavaTarget"));
        rock=new Material(Resources.Load<Shader>("MoltenRock"));
        for(int i=0;i<Capacity;i++){
            var mark=new GameObject("Lava landing tell").transform;mark.SetParent(root,false);
            mark.localRotation=Quaternion.Euler(90,0,0);mark.localScale=new Vector3(1.98f,1.98f,1);
            mark.gameObject.AddComponent<MeshFilter>().sharedMesh=quad;
            var surface=mark.gameObject.AddComponent<MeshRenderer>();surface.sharedMaterial=targetInk;surface.shadowCastingMode=ShadowCastingMode.Off;
            marks[i]=mark;surfaces[i]=surface;
            stones[i]=Stone("Flying molten rock",root);
            for(int j=0;j<6;j++)sparks[i,j]=Stone("Molten impact fragment",root);
        }
        Clear();
    }
    Transform Stone(string name,Transform parent){
        var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name=name;
        obj.transform.SetParent(parent,false);UnityEngine.Object.Destroy(obj.GetComponent<Collider>());
        var renderer=obj.GetComponent<Renderer>();renderer.sharedMaterial=rock;renderer.shadowCastingMode=ShadowCastingMode.Off;
        return obj.transform;
    }
    public void Plan(RisingBoard room,bool[] exits,float now,float pressure,float warningSeconds=Lead,float shotBeat=Beat){
        Clear();board=room;started=now;
        WarningSeconds=warningSeconds;beat=shotBeat;
        var available=new List<int>();
        for(int i=0;i<room.Count;i++)if(!exits[i])available.Add(i);
        for(int i=available.Count-1;i>0;i--){
            int j=UnityEngine.Random.Range(0,i+1);int old=available[i];available[i]=available[j];available[j]=old;
        }
        count=Mathf.Min(available.Count,Mathf.RoundToInt(Mathf.Lerp(2,4,pressure)));
        Active=count>0;Signature=count;
        vent=new Vector3(room.Center,-2.4f,(-(room.Width-1)/2f-1.4f)*RisingBoard.Spacing);
        unchecked{for(int i=0;i<count;i++){targets[i]=available[i];Signature=Signature*107+targets[i];}}
    }
    public float LastDanger(int cell){
        if(!Active)return -1;
        for(int i=0;i<count;i++)if(targets[i]==cell)return StrikeAt(i)+Hot;
        return -1;
    }
    public int Target(int i)=>targets[i];
    public float StrikeAt(int i)=>started+i*beat+WarningSeconds;
    public float End=>StrikeAt(count-1)+Hot+.42f;
    public float Contact(Vector3 from,Vector3 to,float now,float slice){
        if(!Active)return float.PositiveInfinity;
        float earliest=float.PositiveInfinity;
        for(int i=0;i<count;i++){
            float strike=StrikeAt(i);
            if(now+slice<strike||now>=strike+Hot)continue;
            float a=slice>0?Mathf.Clamp01((strike-now)/slice):0,b=slice>0?Mathf.Clamp01((strike+Hot-now)/slice):1;
            var center=board.Tiles[targets[i]].transform.position;
            float hit=TrapContact.Entry(Vector3.Lerp(from,to,a)-center,Vector3.Lerp(from,to,b)-center,.84f,.84f);
            if(hit<=1)earliest=Mathf.Min(earliest,a+(b-a)*hit);
        }
        return earliest;
    }
    public void Sample(float now,ChamberAudio sound){
        if(!Active)return;
        for(int i=0;i<count;i++)if(!sounded[i]&&now>=StrikeAt(i)){
            sounded[i]=true;sound.Activate(TrapKind.Lava);
        }
        if(now>=End)Clear();
    }
    public void Draw(float now){
        if(!Active)return;
        for(int i=0;i<count;i++){
            float age=now-StrikeAt(i),tell=age+WarningSeconds;
            var center=board.Tiles[targets[i]].transform.position;
            bool visible=tell>=0&&age<Hot+.42f;
            surfaces[i].enabled=visible;marks[i].position=center+Vector3.up*.065f;
            if(visible){
                ink.SetFloat("_Progress",Mathf.Clamp01(tell/WarningSeconds));
                ink.SetFloat("_ImpactAge",age);ink.SetFloat("_Hot",Hot);surfaces[i].SetPropertyBlock(ink);
            }
            float fallSeconds=Mathf.Min(.74f,WarningSeconds);
            bool falling=age>=-fallSeconds&&age<0;
            stones[i].gameObject.SetActive(falling);
            if(falling){
                float t=(age+fallSeconds)/fallSeconds;
                stones[i].position=Vector3.Lerp(vent,center+Vector3.up*.3f,t)+Vector3.up*(20*t*(1-t));
                stones[i].localScale=new Vector3(.58f,.75f,.58f);
                stones[i].rotation=Quaternion.Euler(t*460+i*43,t*190,27);
            }
            for(int j=0;j<6;j++){
                var fragment=sparks[i,j];bool splash=age>=0&&age<.48f;fragment.gameObject.SetActive(splash);
                if(!splash)continue;
                float t=age/.48f,angle=j*Mathf.PI/3+i*.23f;
                fragment.position=center+new Vector3(Mathf.Cos(angle)*t,Mathf.Sin(Mathf.PI*t)*.92f+.05f,Mathf.Sin(angle)*t);
                fragment.localScale=Vector3.one*Mathf.Lerp(.24f,.015f,t);
            }
        }
    }
    public void Clear(){
        Active=false;Signature=0;count=0;
        for(int i=0;i<Capacity;i++){
            sounded[i]=false;surfaces[i].enabled=false;stones[i].gameObject.SetActive(false);
            for(int j=0;j<6;j++)sparks[i,j].gameObject.SetActive(false);
        }
    }
    public void Dispose(){UnityEngine.Object.Destroy(targetInk);UnityEngine.Object.Destroy(rock);UnityEngine.Object.Destroy(root.gameObject);}
}
