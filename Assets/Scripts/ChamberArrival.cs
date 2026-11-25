using UnityEngine;
using UnityEngine.Rendering;

// Runs once on entering the chamber. Survival time and trap scheduling start later.
public sealed class ChamberArrival : MonoBehaviour
{
    const float DropTime = .76f;
    const float Duration = 2.65f;
    Camera view;
    Transform actor;
    ExplorerHop rig;
    Vector3 floor, homePosition;
    Quaternion homeRotation;
    Material eyelids, dustInk;
    GameObject mask;
    ParticleSystem plume;
    AudioSource impact;
    float time;

    public void Prepare(ChamberGame game, ExplorerHop hop)
    {
        view = game.chamberCamera;
        actor = game.explorer;
        rig = hop;
        floor = game.tiles[4].transform.position;
        homePosition = view.transform.position;
        homeRotation = view.transform.rotation;

        mask = new GameObject("Opening eyes");
        mask.transform.SetParent(view.transform,false);
        mask.transform.localPosition = Vector3.forward;
        mask.AddComponent<MeshFilter>().sharedMesh = game.tiles[4].warning.GetComponent<MeshFilter>().sharedMesh;
        var renderer = mask.AddComponent<MeshRenderer>();
        eyelids = new Material(Resources.Load<Shader>("Eyelids"));
        renderer.sharedMaterial = eyelids;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var dustObject = new GameObject("Landing dust");
        dustObject.transform.SetParent(transform,false);
        dustObject.transform.position = floor;
        plume = dustObject.AddComponent<ParticleSystem>();
        plume.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = plume.main;
        main.playOnAwake = false;
        main.loop = false;
        main.gravityModifier = .16f;
        var emission = plume.emission; emission.enabled = false;
        var shape = plume.shape; shape.enabled = false;
        var fade = plume.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            new[]{new GradientAlphaKey(.86f,0),new GradientAlphaKey(.55f,.43f),new GradientAlphaKey(0,1)});
        fade.color = gradient;
        var size = plume.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.42f,1,1.5f));
        dustInk = new Material(Resources.Load<Shader>("ImpactDust"));
        plume.GetComponent<ParticleSystemRenderer>().sharedMaterial = dustInk;
        impact = dustObject.AddComponent<AudioSource>();
        impact.playOnAwake = false;
        impact.spatialBlend = 0;
        impact.clip = Resources.Load<AudioClip>("StoneLanding");
        if (!view.TryGetComponent<AudioListener>(out _)) view.gameObject.AddComponent<AudioListener>();
        Sample();
    }

    public bool Advance(float dt)
    {
        float previous=time;
        time=Mathf.Min(Duration,time+dt);
        Sample();
        if (previous<DropTime && time>=DropTime) Impact();
        if (time<Duration) return false;
        view.transform.SetPositionAndRotation(homePosition,homeRotation);
        mask.SetActive(false);
        rig.Reset();
        return true;
    }

    void Impact()
    {
        impact.Play();
        plume.Play();
        for (int i=0; i<42; i++)
        {
            float angle=i*2.39996f;
            var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            float variation=Mathf.Repeat(i*.618f,1);
            var puff=new ParticleSystem.EmitParams
            {
                position=radial*(.24f+.33f*variation)+Vector3.up*.1f,
                velocity=radial*(1.6f+2.3f*variation)+Vector3.up*(.52f+.48f*variation),
                startSize=.54f+.64f*variation,
                startLifetime=.75f+.52f*variation,
                startColor=Color.Lerp(new Color(.39f,.32f,.27f),new Color(.8f,.71f,.55f),variation)
            };
            plume.Emit(puff,1);
        }
    }

    void Sample()
    {
        float fall=Mathf.Clamp01(time/DropTime);
        float since=time-DropTime;
        actor.position=floor+Vector3.up*(6.8f*(1-fall*fall));
        float compression= since<0 ? -.09f*fall : .34f*(1-Mathf.SmoothStep(0,1,since/.35f));
        rig.ArrivalPose(compression,since<0 ? -16f*fall : 20f*(1-Mathf.SmoothStep(0,1,since/1.6f)));

        var near=floor+new Vector3(0,3.85f,-6.9f);
        float pullBack=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.32f,Duration,time));
        var nearRotation=Quaternion.LookRotation(floor+Vector3.up*1.28f-near);
        float shake=since<0 ? 0 : .18f*Mathf.Exp(-10*since);
        Vector3 kick=new Vector3(Mathf.Sin(68*since),Mathf.Cos(53*since),0)*shake;
        view.transform.SetPositionAndRotation(Vector3.Lerp(near,homePosition,pullBack)+kick,
            Quaternion.Slerp(nearRotation,homeRotation,pullBack));
        float close=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.40f,since));
        float open=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.68f,1.73f,since));
        float aperture=1-close+open;
        mask.SetActive(aperture<.9999f);
        eyelids.SetFloat("_Open",aperture);
    }

    void OnDestroy()
    {
        if (eyelids) Destroy(eyelids);
        if (dustInk) Destroy(dustInk);
    }
}
