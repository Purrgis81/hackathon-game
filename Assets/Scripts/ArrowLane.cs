using UnityEngine;

public sealed class ArrowLane : MonoBehaviour
{
    const float BaseReach = 3.75f;
    float reach = BaseReach;
    const float HeadLength = .45f;
    const float ShaftLength = .72f;
    Transform guide, bolt, launcher, flash;
    LineRenderer wake;
    readonly Transform[] strings=new Transform[2];
    readonly Transform[] chevrons=new Transform[10];
    Material blue, ivory, bronze, wood, streak;
    Vector3 start, direction;
    public bool Armed { get; private set; }
    public Vector3 LaunchPosition => start;
    public float Speed { get; private set; }

    void Awake()
    {
        // The scene's Standard shader is also available in player builds.
        var template = GetComponent<ChamberGame>().trapMetal;
        blue = Ink(template, new Color(.06f,.53f,.65f));
        ivory = Ink(template, new Color(.64f,.85f,.91f));
        bronze = new Material(template){color=new Color(.59f,.34f,.12f)};
        wood = new Material(template){color=new Color(.19f,.11f,.075f)};
        guide = new GameObject("Arrow lane • preview").transform;
        bolt = new GameObject("Flying arrow").transform;
        guide.SetParent(transform, false);
        bolt.SetParent(transform, false);
        for(int i=-6;i<=6;i++)
            Bar(guide,"Lane dash",0,i*.56f,.046f,.32f,0,blue);
        for (int i=-2; i<=2; i++)
        {
            chevrons[(i+2)*2]=Bar(guide, "Chevron L", -.1f, i*1.35f, .065f, .36f, 40, blue);
            chevrons[(i+2)*2+1]=Bar(guide, "Chevron R", .1f, i*1.35f, .065f, .36f, -40, blue);
        }
        Bar(guide, "Launch marker", 0, -BaseReach+.12f, .58f,.13f,0,blue);
        Bar(bolt, "Shaft", 0, -ShaftLength/2, .105f, ShaftLength, 0, wood);
        Bar(bolt, "Fletching L", -.07f,-.64f,.10f,.22f,35,ivory);
        Bar(bolt, "Fletching R", .07f,-.64f,.10f,.22f,-35,ivory);
        Bar(bolt,"Bronze ferrule",0,-.055f,.14f,.16f,0,bronze);
        var tip = new GameObject("Arrowhead");
        tip.transform.SetParent(bolt,false);
        var blade = GetComponent<ChamberGame>().arrowheadMesh;
        tip.AddComponent<MeshFilter>().sharedMesh = blade;
        tip.AddComponent<MeshRenderer>().sharedMaterial = ivory;
        tip.transform.localRotation = Quaternion.Euler(90,0,0);
        tip.transform.localScale = new Vector3(.98f, HeadLength/.92f,.86f);
        launcher=new GameObject("Bronze bolt launcher").transform;
        launcher.SetParent(transform,false);
        Bar(launcher,"Carved stock",0,-.59f,.29f,1.03f,0,wood).localScale=new Vector3(.29f,.25f,1.03f);
        Bar(launcher,"Guide rail L",-.14f,-.25f,.08f,.80f,0,bronze);
        Bar(launcher,"Guide rail R",.14f,-.25f,.08f,.80f,0,bronze);
        for(int i=0;i<2;i++)
        {
            int sign=i*2-1;
            Link(Bar(launcher,"Curved limb",0,0,.12f,.2f,0,bronze),new Vector3(0,0,-.21f),new Vector3(sign*.52f,0,-.13f),.13f);
            Link(Bar(launcher,"Limb tip",0,0,.1f,.2f,0,bronze),new Vector3(sign*.52f,0,-.13f),new Vector3(sign*.76f,0,-.43f),.095f);
            strings[i]=Bar(launcher,"Tensioned cord",0,0,.03f,1,0,ivory);
        }
        flash=Bar(launcher,"Release glint",0,.1f,.22f,.38f,45,ivory);
        wake=new GameObject("Bolt slipstream").AddComponent<LineRenderer>();
        wake.transform.SetParent(bolt,false);
        wake.useWorldSpace=false;
        wake.positionCount=2;
        wake.startWidth=.015f;
        wake.endWidth=.17f;
        streak=new Material(Resources.Load<Shader>("BoltStreak"));
        wake.sharedMaterial=streak;
        Clear();
    }

    static Material Ink(Material template, Color color)
    {
        var mat = new Material(template) { color = color };
        mat.SetFloat("_Metallic", .1f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color*.55f);
        return mat;
    }

    static Transform Bar(Transform parent, string label, float x,float z,float width,float length,float yaw,Material mat)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name=label;
        obj.transform.SetParent(parent,false);
        obj.transform.localPosition=new Vector3(x,0,z);
        obj.transform.localRotation=Quaternion.Euler(0,yaw,0);
        obj.transform.localScale=new Vector3(width,.055f,length);
        obj.GetComponent<Collider>().enabled=false;
        obj.GetComponent<Renderer>().sharedMaterial=mat;
        return obj.transform;
    }

    static void Link(Transform part,Vector3 a,Vector3 b,float width)
    {
        part.localPosition=(a+b)*.5f;
        part.localRotation=Quaternion.LookRotation(b-a);
        part.localScale=new Vector3(width,width,(b-a).magnitude);
    }

    void StringPose(float pull)
    {
        for(int i=0;i<2;i++)
            Link(strings[i],new Vector3((i*2-1)*.76f,.04f,-.43f),new Vector3(0,.04f,-.34f-.55f*pull),.04f);
    }

    public float FlightTime => (reach*2+ShaftLength)/Speed;

    public void Aim(Vector3 laneCenter, Vector3 forward, float radius,float pressure)
    {
        Armed=true;
        direction=forward;
        Speed=Random.Range(Mathf.Lerp(23f,31f,pressure),Mathf.Lerp(31f,41f,pressure));
        reach=radius*RisingBoard.Spacing+1.45f;
        start=laneCenter-forward*reach+Vector3.up*.55f;
        guide.localScale = new Vector3(1,1,reach/BaseReach);
        guide.position=laneCenter+Vector3.up*.08f;
        guide.rotation=bolt.rotation=Quaternion.LookRotation(forward);
        guide.gameObject.SetActive(true);
        launcher.SetPositionAndRotation(start,bolt.rotation);
        launcher.gameObject.SetActive(true);
        flash.gameObject.SetActive(false);
        wake.gameObject.SetActive(false);
        Draw(0,0);
        bolt.position=start;
        bolt.gameObject.SetActive(true);
    }

    public void Draw(float t,float age)
    {
        if(!Armed)return;
        StringPose(Mathf.SmoothStep(0,1,t));
        bolt.position=start+direction*(.23f*(1-Mathf.SmoothStep(0,1,t)));
        blue.SetColor("_EmissionColor",blue.color*Mathf.Lerp(.15f,.9f,t));
        float travel=age*Speed/guide.localScale.z;
        for(int i=0;i<chevrons.Length;i++)
        {
            var pos=chevrons[i].localPosition;
            pos.z=Mathf.Repeat(i/2*1.35f+travel,6.75f)-3.375f;
            chevrons[i].localPosition=pos;
        }
    }
    public void Fire()
    {
        guide.gameObject.SetActive(false);
        wake.gameObject.SetActive(true);
        StringPose(0);
    }
    public void Preview(bool visible){
        guide.gameObject.SetActive(visible);launcher.gameObject.SetActive(visible);bolt.gameObject.SetActive(visible);
    }
    Vector3 Position(float t) => start+direction*((reach*2+ShaftLength)*Mathf.Clamp01(t));
    public void Sample(float t)
    {
        float age=Mathf.Max(0,t)*FlightTime;
        bolt.position=Position(t);
        wake.SetPosition(0,Vector3.back*(ShaftLength+Mathf.Min(1.65f,age*10)));
        wake.SetPosition(1,Vector3.back*ShaftLength);
        launcher.position=start-direction*(.24f*Mathf.Sin(Mathf.PI*Mathf.Clamp01(age/.16f)));
        flash.gameObject.SetActive(age<.075f);
        flash.localScale=Vector3.one*(.29f*(1-Mathf.Clamp01(age/.075f)));
        if(t>=1)bolt.gameObject.SetActive(false);
    }

    public float Contact(Vector3 playerFrom, Vector3 playerTo, float t0, float t1)
    {
        var inverse=Quaternion.Inverse(bolt.rotation);
        Vector3 from=inverse*(playerFrom-Position(t0)), to=inverse*(playerTo-Position(t1));
        Vector3 shaftCenter=Vector3.back*(ShaftLength/2);
        Vector3 headCenter=Vector3.forward*(HeadLength*.45f);
        return Mathf.Min(TrapContact.Entry(from-shaftCenter,to-shaftCenter,.055f,ShaftLength/2),
            TrapContact.Entry(from-headCenter,to-headCenter,.12f,HeadLength*.55f));
    }

    public void Clear()
    {
        Armed=false;
        guide.gameObject.SetActive(false);
        bolt.gameObject.SetActive(false);
        launcher.gameObject.SetActive(false);
    }

    void OnDestroy() { Destroy(blue); Destroy(ivory); Destroy(bronze); Destroy(wood); Destroy(streak); }
}
