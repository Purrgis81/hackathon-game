using UnityEngine;

public sealed class SpikeTile : MonoBehaviour
{
    public Renderer warning;
    public Mesh stakeMesh;
    public Material stakeMaterial;
    public ParticleSystem dust;
    MaterialPropertyBlock properties;
    static readonly int Progress = Shader.PropertyToID("_Progress");
    int lastProgress=-1,lastOrder;
    Transform cluster;
    Transform[] stakes;
    public bool Erupted => cluster && cluster.gameObject.activeSelf;
    public bool CanErupt => gameObject.activeInHierarchy && Mathf.Abs(transform.localPosition.y)<.01f;
    static readonly Vector3[] Sites = {
        new Vector3(-.46f,0,-.46f), new Vector3(.46f,0,-.46f), Vector3.zero,
        new Vector3(-.46f,0,.46f), new Vector3(.46f,0,.46f)
    };
    const float Beat=.035f, Rise=.095f, Retreat=.12f, Height=1.46f;
    const float ContactFraction=.20f;

    void CreateStakes()
    {
        cluster=new GameObject("Wooden stakes").transform;
        cluster.SetParent(transform,false);
        stakes=new Transform[Sites.Length];
        var ground=new MaterialPropertyBlock();
        ground.SetFloat("_GroundY",transform.position.y);
        for(int i=0;i<stakes.Length;i++)
        {
            var stake=new GameObject("Stake").transform;
            stake.SetParent(cluster,false);
            stake.localRotation=Quaternion.Euler(0,53*i,0);
            stake.gameObject.AddComponent<MeshFilter>().sharedMesh=stakeMesh;
            var renderer=stake.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=stakeMaterial;
            renderer.SetPropertyBlock(ground);
            stake.gameObject.SetActive(false);
            stakes[i]=stake;
        }
    }

    public float Contact(Vector3 from,Vector3 to,float age,float slice,bool simultaneous,float duration)
    {
        if(!CanErupt || slice<=0)return float.PositiveInfinity;
        float entry=float.PositiveInfinity;
        for(int i=0;i<Sites.Length;i++)
        {
            float beat=simultaneous?0:i*Beat;
            float start=Mathf.Max(0,beat+Rise*ContactFraction-age);
            float end=Mathf.Min(slice,duration-Retreat*ContactFraction-age);
            if(end<start)continue;
            float a=start/slice,b=end/slice;
            var center=transform.TransformPoint(Sites[i]);
            float hit=TrapContact.Entry(Vector3.Lerp(from,to,a)-center,Vector3.Lerp(from,to,b)-center,.19f,.19f);
            if(hit<=1)entry=Mathf.Min(entry,Mathf.Lerp(a,b,hit));
        }
        return entry;
    }

    public void ResetTile()
    {
        warning.enabled = false;
        if(cluster)cluster.gameObject.SetActive(false);
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void ShowWarning(float progress,int order=1)
    {
        if(order>=0 && !CanErupt)return;
        int level=Mathf.RoundToInt(Mathf.Clamp01(progress)*60);
        if(warning.enabled&&level==lastProgress&&order==lastOrder)return;
        lastProgress=level;lastOrder=order;
        properties ??= new MaterialPropertyBlock();
        warning.enabled = true;
        properties.SetFloat(Progress, progress);
        properties.SetFloat("_Order",order);
        warning.SetPropertyBlock(properties);
    }

    public void Strike(bool simultaneous)
    {
        if(!CanErupt)return;
        warning.enabled=false;
        if(!cluster)CreateStakes();
        cluster.gameObject.SetActive(true);
        dust.Emit(simultaneous ? 2 : 3);
    }

    public void Sample(float age,float duration,bool simultaneous)
    {
        if(!Erupted)return;
        for(int i=0;i<stakes.Length;i++)
        {
            float t=age-(simultaneous?0:i*Beat);
            float exposed=Mathf.Clamp01(Mathf.Min(t/Rise,(duration-age)/Retreat));
            var stake=stakes[i];
            stake.localPosition=Sites[i]+Vector3.down*(Height*(1-exposed));
            if(stake.gameObject.activeSelf!=(exposed>0))stake.gameObject.SetActive(exposed>0);
        }
    }
}
