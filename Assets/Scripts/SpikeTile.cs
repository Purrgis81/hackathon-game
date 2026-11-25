using UnityEngine;

public sealed class SpikeTile : MonoBehaviour
{
    public Renderer warning;
    public Transform spikes;
    public ParticleSystem dust;
    MaterialPropertyBlock properties;
    static readonly int Progress = Shader.PropertyToID("_Progress");
    int lastProgress=-1,lastOrder;
    Transform[] blades;
    const float BladeBeat=.035f;

    void Awake()
    {
        if (!spikes.Find("Central spike"))
        {
            var center = Instantiate(spikes.GetChild(0), spikes);
            center.name = "Central spike";
            center.localPosition = Vector3.up * .012f;
        }
        blades = new Transform[spikes.childCount];
        for (int i = 0; i < blades.Length; i++)
        {
            blades[i] = spikes.GetChild(i);
            blades[i].localScale = new Vector3(1.8f, 1.65f, 1.8f);
        }
        System.Array.Sort(blades,(a,b)=>
        {
            int row=a.localPosition.z.CompareTo(b.localPosition.z);
            return row!=0 ? row : a.localPosition.x.CompareTo(b.localPosition.x);
        });
    }

    public float Contact(Vector3 from, Vector3 to,float age,float slice,bool simultaneous)
    {
        float entry = float.PositiveInfinity;
        for(int i=0;i<blades.Length;i++)
        {
            float wait=(simultaneous ? 0 : i*BladeBeat)-age;
            if(wait>slice+.000001f)continue;
            float begin=slice>0 ? Mathf.Clamp01(wait/slice) : 0;
            var center=blades[i].position;
            float hit=TrapContact.Entry(Vector3.Lerp(from,to,begin)-center,to-center,.26f,.26f);
            if(hit<=1)entry=Mathf.Min(entry,begin+(1-begin)*hit);
        }
        return entry;
    }

    public void ResetTile()
    {
        warning.enabled = false;
        spikes.gameObject.SetActive(false);
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void ShowWarning(float progress,int order=1)
    {
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
        ShowWarning(1,simultaneous ? 0 : 1);
        spikes.gameObject.SetActive(true);
        dust.Emit(simultaneous ? 2 : 6);
    }

    public void Sample(float age,float duration,bool simultaneous)
    {
        for(int i=0;i<blades.Length;i++)
        {
            float t=age-(simultaneous ? 0 : i*BladeBeat);
            bool visible=t>=-.000001f;
            if(blades[i].gameObject.activeSelf!=visible)blades[i].gameObject.SetActive(visible);
            if(!visible)continue;
            float rise=Mathf.Lerp(.48f,1,Mathf.Clamp01(t/.065f));
            float retract=1-.39f*Mathf.InverseLerp(duration-.10f,duration,age);
            blades[i].localScale=new Vector3(1.8f,1.65f*rise*retract,1.8f);
        }
    }
}
