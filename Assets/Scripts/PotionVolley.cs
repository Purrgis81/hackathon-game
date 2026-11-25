using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class PotionVolley : MonoBehaviour
{
    const float FallTime=.78f;
    Transform bottle, spray;
    readonly Transform[] drops=new Transform[16];
    readonly List<Mesh> meshes=new List<Mesh>();
    Material glass, brew, cork, lip;
    Vector3 target;
    bool casting;

    void Awake()
    {
        var template=GetComponent<ChamberGame>().tiles[0].spikes.GetChild(0).GetComponent<Renderer>().sharedMaterial;
        glass=new Material(Resources.Load<Shader>("PotionGlass"));
        brew=new Material(template){ color=new Color(.53f,.045f,.79f) };
        brew.EnableKeyword("_EMISSION"); brew.SetColor("_EmissionColor",new Color(.29f,.025f,.38f));
        cork=new Material(template){color=new Color(.56f,.31f,.12f)};
        lip=new Material(template){color=new Color(.85f,.94f,.84f)};
        bottle=Node("Falling alchemist flask",transform);
        var body=Profile(new[]{new Vector2(.19f,-.46f),new Vector2(.43f,-.28f),new Vector2(.42f,.13f),new Vector2(.16f,.36f),new Vector2(.16f,.56f),new Vector2(.21f,.59f)});
        var liquid=Profile(new[]{new Vector2(.17f,-.41f),new Vector2(.38f,-.25f),new Vector2(.38f,.055f),new Vector2(.32f,.08f)});
        var stopper=Profile(new[]{new Vector2(.16f,.57f),new Vector2(.17f,.72f)});
        Part(bottle,"Faceted glass",body,glass);
        Part(bottle,"Violet liquid",liquid,brew);
        Part(bottle,"Cork",stopper,cork);
        Part(bottle,"Bottle lip",Profile(new[]{new Vector2(.19f,.51f),new Vector2(.21f,.56f)}),lip);
        spray=Node("Shattering potion",transform);
        var bead=Profile(new[]{new Vector2(.02f,-.48f),new Vector2(.39f,-.08f),new Vector2(.27f,.23f),new Vector2(.02f,.55f)});
        for(int i=0;i<drops.Length;i++) drops[i]=Part(spray,"Splash fragment",bead,i%4==0 ? lip : brew);
        Clear();
    }

    static Transform Node(string name,Transform parent)
    {
        var obj=new GameObject(name).transform; obj.SetParent(parent,false); return obj;
    }

    static Transform Part(Transform root,string name,Mesh mesh,Material material)
    {
        var part=Node(name,root);
        part.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=part.gameObject.AddComponent<MeshRenderer>();
        r.sharedMaterial=material; r.shadowCastingMode=ShadowCastingMode.Off;
        return part;
    }

    Mesh Profile(Vector2[] rings)
    {
        const int sides=8;
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int j=0;j<rings.Length;j++)
        for(int i=0;i<sides;i++)
        {
            float a=(i+.5f)*2*Mathf.PI/sides;
            vertices.Add(new Vector3(Mathf.Cos(a)*rings[j].x,rings[j].y,Mathf.Sin(a)*rings[j].x));
            if(j==0)continue;
            int lo=(j-1)*sides+i,next=(j-1)*sides+(i+1)%sides;
            triangles.AddRange(new[]{lo,lo+sides,next,next,lo+sides,next+sides});
        }
        int end=(rings.Length-1)*sides;
        for(int i=1;i<sides-1;i++) triangles.AddRange(new[]{0,i,i+1,end,end+i+1,end+i});
        var m=new Mesh();m.SetVertices(vertices);m.SetTriangles(triangles,0);m.RecalculateNormals();
        meshes.Add(m);return m;
    }

    public void Aim(Vector3 point) {Clear();casting=true;target=point;}
    public void Fall(float remaining)
    {
        if(!casting || remaining>FallTime)return;
        float t=Mathf.Clamp01(1-remaining/FallTime);
        bottle.gameObject.SetActive(true);
        bottle.position=target+new Vector3(-1.45f*(1-t),.34f+3.8f*(1-t*t),1.3f*(1-t));
        bottle.rotation=Quaternion.AngleAxis(52+t*102,Vector3.up)*Quaternion.AngleAxis(76+18*t,Vector3.forward);
    }
    public void Impact(float age)
    {
        if(!casting)return;
        bottle.gameObject.SetActive(false);
        float t=Mathf.Clamp01(age/.28f);
        if(t>=1){Clear();return;}
        spray.gameObject.SetActive(true); spray.position=target+Vector3.up*.13f;
        for(int i=0;i<drops.Length;i++)
        {
            float a=i*2.39996f;
            var path=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            drops[i].localPosition=path*(.14f+t*(1.1f+.38f*(i%4)))+Vector3.up*(.12f+4*t*(1-t)*(.43f+.29f*(i%3)));
            drops[i].localScale=new Vector3(.26f,.41f,.24f)*(1-t*.94f);
            drops[i].localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,25+155*t);
        }
    }
    public void Clear(){casting=false;bottle.gameObject.SetActive(false);spray.gameObject.SetActive(false);}
    void OnDestroy(){foreach(var m in meshes)Destroy(m);Destroy(glass);Destroy(brew);Destroy(cork);Destroy(lip);}
}
