using UnityEngine;

// Sweep the explorer's foot position against a trap footprint in the ground plane.
// A moving projectile uses the same sweep in its own moving reference frame.
public static class TrapContact
{
    public const float PlayerHalfWidth = .21f;

    public static float Entry(Vector3 from, Vector3 to, float halfX, float halfZ)
    {
        float enter = 0f, leave = 1f;
        if (Clip(from.x, to.x - from.x, halfX + PlayerHalfWidth, ref enter, ref leave)
            && Clip(from.z, to.z - from.z, halfZ + PlayerHalfWidth, ref enter, ref leave)) return enter;
        return float.PositiveInfinity;
    }

    static bool Clip(float start, float delta, float extent, ref float enter, ref float leave)
    {
        if (Mathf.Abs(delta) < .000001f) return Mathf.Abs(start) <= extent;
        float a = (-extent - start) / delta, b = (extent - start) / delta;
        enter = Mathf.Max(enter, Mathf.Min(a,b));
        leave = Mathf.Min(leave, Mathf.Max(a,b));
        return enter <= leave;
    }
}
