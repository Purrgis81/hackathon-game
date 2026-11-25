using UnityEngine;
using UnityEngine.Rendering;

public sealed class DeathFocus : MonoBehaviour
{
    public const float Hold = 1.25f, Close = .28f, Reopen = .32f;
    Camera view;
    Material ink;
    MeshRenderer veil;
    Vector3 contact;
    public Color Accent { get; private set; }

    public void Prepare(Camera camera, Mesh quad)
    {
        view = camera;
        var screen = new GameObject("Contact focus");
        screen.transform.SetParent(view.transform,false);
        screen.transform.localPosition = Vector3.forward;
        screen.AddComponent<MeshFilter>().sharedMesh = quad;
        veil = screen.AddComponent<MeshRenderer>();
        ink = new Material(Resources.Load<Shader>("DeathFocus"));
        veil.sharedMaterial = ink;
        veil.shadowCastingMode = ShadowCastingMode.Off;
        veil.receiveShadows = false;
        veil.enabled = false;
    }

    public void Show(TrapKind cause, Vector3 feet)
    {
        contact = feet + Vector3.up*.4f;
        Accent = cause == TrapKind.Spike ? new Color(1,.43f,.15f) :
            cause == TrapKind.Arrow || cause==TrapKind.Wave ? new Color(.15f,.89f,1) :
            cause==TrapKind.Lava ? new Color(1,.40f,.06f) : new Color(.84f,.54f,1);
        ink.SetColor("_Ink",Accent);
        veil.enabled = true;
        Frame(0);
    }

    public void Frame(float age)
    {
        Vector3 p = view.WorldToViewportPoint(contact);
        ink.SetVector("_Focus",new Vector4(p.x,p.y,view.aspect,Mathf.Lerp(.13f,.079f,Mathf.SmoothStep(0,1,age/.18f))));
        ink.SetFloat("_Fade",Mathf.SmoothStep(0,1,(age-Hold)/Close));
        ink.SetFloat("_Pulse",Mathf.Clamp01(age/.23f));
    }

    public void Reveal(float t)
    {
        // Release both the spotlight and its dark surround as the new board appears.
        ink.SetVector("_Focus",new Vector4(.5f,.5f,0,-1));
        ink.SetFloat("_Fade",1-Mathf.SmoothStep(0,1,t));
        if (t>=1) veil.enabled=false;
    }

    void OnDestroy() { if (ink) Destroy(ink); }
}
