using UnityEngine;
using UnityEngine.Rendering;

public sealed class GroundCrackVisual : MonoBehaviour
{
    const int ShardCount=8;
    static readonly Vector2[] Rim={
        new Vector2(-.82f,-.82f),new Vector2(0,-.82f),new Vector2(.82f,-.82f),new Vector2(.82f,0),
        new Vector2(.82f,.82f),new Vector2(0,.82f),new Vector2(-.82f,.82f),new Vector2(-.82f,0)
    };
    static readonly Vector3[][] Paths={
        new[]{new Vector3(-.82f,.11f,-.76f),new Vector3(-.35f,.11f,-.39f),new Vector3(-.49f,.11f,-.08f),new Vector3(.05f,.11f,.20f),new Vector3(.31f,.11f,.48f),new Vector3(.82f,.11f,.78f)},
        new[]{new Vector3(.76f,.115f,-.76f),new Vector3(.37f,.115f,-.39f),new Vector3(.12f,.115f,-.20f),new Vector3(-.12f,.115f,.19f),new Vector3(-.40f,.115f,.42f),new Vector3(-.78f,.115f,.78f)},
        new[]{new Vector3(0,.12f,-.72f),new Vector3(-.14f,.12f,-.37f),new Vector3(.12f,.12f,-.08f),new Vector3(.03f,.12f,.23f),new Vector3(.22f,.12f,.45f),new Vector3(.05f,.12f,.76f)}
    };
    LineRenderer[] lines;
    MaterialPropertyBlock properties;
    static readonly int ColorId=Shader.PropertyToID("_Color");
    readonly Transform[] shards=new Transform[ShardCount];
    readonly Mesh[] shardMeshes=new Mesh[ShardCount];
    readonly Vector3[] shardStarts=new Vector3[ShardCount],shardVelocity=new Vector3[ShardCount],shardSpin=new Vector3[ShardCount];
    readonly Quaternion[] shardRotations=new Quaternion[ShardCount];
    bool breaking;

    public void Show(float progress,Material material){
        if(lines==null){
            lines=new LineRenderer[Paths.Length];
            for(int i=0;i<lines.Length;i++){
                var child=new GameObject("Stone crack");child.transform.SetParent(transform,false);
                var line=child.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=material;
                line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
                line.startWidth=.055f;line.endWidth=.04f;line.numCapVertices=2;
                lines[i]=line;
            }
        }
        progress=Mathf.Clamp01(progress);properties??=new MaterialPropertyBlock();
        properties.SetColor(ColorId,Color.black);
        foreach(var line in lines)line.SetPropertyBlock(properties);
        for(int p=0;p<lines.Length;p++){
            int points=Mathf.Clamp(Mathf.CeilToInt(progress*Paths[p].Length),1,Paths[p].Length);
            lines[p].positionCount=points;
            for(int i=0;i<points;i++)lines[p].SetPosition(i,Paths[p][i]);
            lines[p].gameObject.SetActive(progress>.015f);
        }
    }

    public void BeginBreak(Material debrisMaterial,int seed){
        if(breaking)return;breaking=true;
        Vector2 center=new Vector2(Mathf.Sin(seed*1.31f)*.045f,Mathf.Cos(seed*.91f)*.045f);
        for(int i=0;i<shards.Length;i++){
            var piece=new GameObject("Fractured stone shard");
            var mesh=BuildShard(center,Rim[i],Rim[(i+1)%Rim.Length]);
            piece.AddComponent<MeshFilter>().sharedMesh=mesh;shardMeshes[i]=mesh;
            var renderer=piece.AddComponent<MeshRenderer>();renderer.sharedMaterial=debrisMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            piece.transform.SetParent(transform.parent,true);
            shardStarts[i]=transform.position;shardRotations[i]=transform.rotation;
            piece.transform.SetPositionAndRotation(shardStarts[i],shardRotations[i]);
            shards[i]=piece.transform;
            Vector2 direction=new Vector2(center.x+(Rim[i].x+Rim[(i+1)%Rim.Length].x)*.5f,center.y+(Rim[i].y+Rim[(i+1)%Rim.Length].y)*.5f).normalized;
            float angle=seed*1.31f+i*1.5708f;
            shardVelocity[i]=new Vector3(direction.x*2.15f,.25f+(i%3)*.2f,direction.y*2.15f);
            shardSpin[i]=new Vector3(120+Mathf.Abs(Mathf.Sin(angle))*230,90+Mathf.Abs(Mathf.Cos(angle))*210,130+Mathf.Abs(Mathf.Sin(angle*.7f))*190);
        }
    }

    static Mesh BuildShard(Vector2 center,Vector2 edgeA,Vector2 edgeB){
        Vector2 a=Vector2.Lerp(center,edgeA,.98f),b=Vector2.Lerp(center,edgeB,.98f);
        const float top=.105f,bottom=-.045f;
        var mesh=new Mesh();mesh.vertices=new[]{
            new Vector3(center.x,top,center.y),new Vector3(a.x,top,a.y),new Vector3(b.x,top,b.y),
            new Vector3(center.x,bottom,center.y),new Vector3(a.x,bottom,a.y),new Vector3(b.x,bottom,b.y)
        };
        mesh.triangles=new[]{0,2,1,3,4,5, 1,2,5,1,5,4, 2,0,3,2,3,5, 0,1,4,0,4,3};
        mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }

    public void AnimateBreak(float age){
        if(!breaking)return;
        for(int i=0;i<shards.Length;i++)if(shards[i]){
            if(age>=.9f){shards[i].gameObject.SetActive(false);continue;}
            shards[i].gameObject.SetActive(true);
            float t=Mathf.Max(0,age);
            shards[i].position=shardStarts[i]+shardVelocity[i]*t+Vector3.down*(22f*t*t);
            shards[i].rotation=shardRotations[i]*Quaternion.Euler(shardSpin[i]*t);
        }
    }

    public void Clear(){
        if(lines!=null)foreach(var line in lines)line.gameObject.SetActive(false);
        for(int i=0;i<shards.Length;i++){
            if(shards[i]){shards[i].gameObject.SetActive(false);Destroy(shards[i].gameObject);shards[i]=null;}
            if(shardMeshes[i]){Destroy(shardMeshes[i]);shardMeshes[i]=null;}
        }
        breaking=false;
    }

    void OnDestroy(){
        for(int i=0;i<shardMeshes.Length;i++)if(shardMeshes[i])Destroy(shardMeshes[i]);
    }
}
