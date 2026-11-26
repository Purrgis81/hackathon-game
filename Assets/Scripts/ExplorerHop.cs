using UnityEngine;

// The game owns the ground position and clock. Only the character rig follows this arc.
public sealed class ExplorerHop
{
    const float LandingCompression = .18f;
    const float RecoverySeconds = .095f;
    readonly Transform feet, body;
    public Transform Body => body;
    readonly Quaternion homeFacing;
    Quaternion turnFrom, turnTo;
    float compression, launchCompression, recovery;
    int stride;
    Vector3 intendedPosition,intendedScale,shownPosition,shownScale;
    Quaternion intendedRotation,shownRotation;

    public ExplorerHop(Transform explorer)
    {
        feet = explorer;
        homeFacing = feet.rotation;
        // Older scenes contain the original explorer under this anchor.
        var original = feet.Find("character-human");
        if (original)
        {
            original.gameObject.SetActive(false);
            Object.Destroy(original.gameObject);
        }
        body = new GameObject("Hop pose").transform;
        body.SetParent(feet, false);
        Reset();
    }

    public void Reset()
    {
        feet.rotation = turnFrom = turnTo = homeFacing;
        recovery = 0;
        stride = -1;
        Pose(0, 0, 0, 0);
        shownPosition=intendedPosition;shownScale=intendedScale;shownRotation=intendedRotation;
    }

    public void ArrivalPose(float squash, float pitch) => Pose(0,squash,pitch,0);

    public void Present(float dt,bool freeze){
        float blend=freeze?1:1-Mathf.Exp(-dt*32);
        shownPosition=Vector3.Lerp(shownPosition,intendedPosition,blend);
        shownScale=Vector3.Lerp(shownScale,intendedScale,blend);
        shownRotation=Quaternion.Slerp(shownRotation,intendedRotation,blend);
        body.localPosition=shownPosition;body.localScale=shownScale;body.localRotation=shownRotation;
    }

    public void PushOff(Vector2Int direction)
    {
        turnFrom = feet.rotation;
        turnTo = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.y));
        launchCompression = compression;
        stride = -stride;
    }

    // Apply the local hop to the mesh only. The host's feet, facing, and
    // collision trajectory continue to advance on confirmed simulation ticks.
    public void PresentLocalStep(float t){
        float flight=Mathf.InverseLerp(.12f,.9f,t);
        float arc=4f*flight*(1f-flight);
        body.localPosition=Vector3.up*(.41f*arc);
        body.localRotation=Quaternion.Euler(12f*arc,0,0);
        body.localScale=new Vector3(1-.05f*arc,1+.1f*arc,1-.05f*arc);
    }

    public void Step(float t)
    {
        feet.rotation = Quaternion.Slerp(turnFrom, turnTo, Mathf.SmoothStep(0, 1, t/.48f));
        float flight = Mathf.InverseLerp(.12f, .9f, t);
        float arc = 4f * flight * (1f-flight);
        float crouch = Mathf.Lerp(launchCompression, .15f, Mathf.SmoothStep(0, 1, t/.12f))
            * (1f-Mathf.SmoothStep(0, 1, (t-.12f)/.19f));
        float landing = LandingCompression * Mathf.SmoothStep(0, 1, (t-.77f)/.23f);
        Pose(.41f*arc, crouch + landing - .105f*arc, 12f*arc, stride*4f*arc);
        recovery = RecoverySeconds;
    }

    public void Settle(float dt)
    {
        if (recovery <= 0) return;
        recovery = Mathf.Max(0, recovery-dt);
        float t = 1-recovery/RecoverySeconds;
        float spring = LandingCompression*(1-Mathf.SmoothStep(0,1,t/.62f))
            - .045f*Mathf.Sin(Mathf.PI*Mathf.InverseLerp(.48f,1,t));
        Pose(0, spring, 0, 0);
    }

    void Pose(float height, float squash, float pitch, float roll)
    {
        compression = squash;
        body.localPosition = Vector3.up * height;
        body.localRotation = Quaternion.Euler(pitch, 0, roll);
        body.localScale = new Vector3(1+squash*.5f, 1-squash, 1+squash*.5f);
        intendedPosition=body.localPosition;intendedScale=body.localScale;intendedRotation=body.localRotation;
    }
}
