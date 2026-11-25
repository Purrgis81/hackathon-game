using UnityEngine;

public sealed class PoisonFloor
{
    const float CenterLifetime=.92f, FirstArmLifetime=.45f, TipLifetime=.32f;
    public const float HalfWidth = .87f;
    public bool Pending { get; private set; }
    public float StartsAt { get; private set; }
    float delay;
    float lifetime;
    public float EndsAt { get; private set; } = -1f;
    public readonly Vector3 center;
    readonly MeshRenderer surface;
    readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    static readonly int Fill = Shader.PropertyToID("_Fill");
    static readonly int Preview = Shader.PropertyToID("_Preview");

    public PoisonFloor(SpikeTile tile, Material material)
    {
        center = tile.transform.position;
        var obj = new GameObject("Poison inlay");
        obj.transform.SetParent(tile.transform, false);
        obj.transform.localPosition = Vector3.up * .028f;
        obj.transform.localRotation = tile.warning.transform.localRotation;
        obj.transform.localScale = new Vector3(2*HalfWidth, 2*HalfWidth, 1);
        obj.AddComponent<MeshFilter>().sharedMesh = tile.warning.GetComponent<MeshFilter>().sharedMesh;
        surface = obj.AddComponent<MeshRenderer>();
        surface.sharedMaterial = material;
        surface.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        surface.enabled = false;
    }

    public void Mark(float spreadDelay=0) { delay=spreadDelay; Pending=true; }
    public void Activate(float now)
    {
        if (!Pending) return;
        Pending = false;
        StartsAt = now+delay;
        lifetime=delay<=0 ? CenterLifetime : delay<=.15f ? FirstArmLifetime : TipLifetime;
        EndsAt=StartsAt+lifetime;
    }

    public void Clear()
    {
        Pending = false;
        EndsAt = -1;
        surface.enabled = false;
    }

    public float Contact(Vector3 from, Vector3 to, float now, float slice)
    {
        if (now>=EndsAt || now+slice<StartsAt) return float.PositiveInfinity;
        float begin=slice>0 ? Mathf.Clamp01((StartsAt-now)/slice) : 0;
        var start=Vector3.Lerp(from,to,begin)-center;
        var p=new Vector2(start.x,start.z);
        var v=new Vector2(to.x-center.x-p.x,to.z-center.z-p.y);
        float radius=.87f*HalfWidth+TrapContact.PlayerHalfWidth;
        float c=p.sqrMagnitude-radius*radius, entry=0;
        if(c>0)
        {
            float a=v.sqrMagnitude,b=Vector2.Dot(p,v),disc=b*b-a*c;
            if(a<.00000001f || disc<0 || b>=0)return float.PositiveInfinity;
            entry=(-b-Mathf.Sqrt(disc))/a;
            if(entry>1)return float.PositiveInfinity;
        }
        float hit=begin+(1-begin)*entry;
        return now+slice*hit<EndsAt ? hit : float.PositiveInfinity;
    }

    public void Draw(float now)
    {
        surface.enabled = Pending || now < EndsAt;
        if (!surface.enabled) return;
        properties.SetFloat(Fill, Pending ? 1 : Mathf.Clamp01((EndsAt-now)/lifetime));
        properties.SetFloat(Preview, Pending || now<StartsAt ? 1f : 0f);
        surface.SetPropertyBlock(properties);
    }
}
