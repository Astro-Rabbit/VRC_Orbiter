using UdonSharp;
using UnityEngine;

public class AttitudePropagator : UdonSharpBehaviour
{
    [Header("References")]
    public CraftConfig config;
    public CraftAttitudeState att;

    [Header("Applied torque (Nm, body frame)")]
    public Vector3 tau_B = Vector3.zero;

    [Header("Integration")]
    public bool normalizeEachStep = true;

    public void Step(double dt)
    {
        if (config == null || att == null) return;

        double Ixx = config.Ixx;
        double Iyy = config.Iyy;
        double Izz = config.Izz;

        if (Ixx <= 0.0 || Iyy <= 0.0 || Izz <= 0.0) return;

        // Current body rates
        double wx = att.wx;
        double wy = att.wy;
        double wz = att.wz;

        double tx = (double)tau_B.x;
        double ty = (double)tau_B.y;
        double tz = (double)tau_B.z;

        // Euler rigid body equations for diagonal inertia:
        // I * wdot + w x (I w) = tau
        double wdotX = (tx - (Izz - Iyy) * wy * wz) / Ixx;
        double wdotY = (ty - (Ixx - Izz) * wz * wx) / Iyy;
        double wdotZ = (tz - (Iyy - Ixx) * wx * wy) / Izz;

        // Semi-implicit Euler
        wx += wdotX * dt;
        wy += wdotY * dt;
        wz += wdotZ * dt;

        att.wx = wx;
        att.wy = wy;
        att.wz = wz;

        // Quaternion integration: qdot = 0.5 * q ⊗ [0, w]
        Quaternion q = att.qBE;
        Quaternion wq = new Quaternion((float)wx, (float)wy, (float)wz, 0f);

        Quaternion qdot = Mul(q, wq);
        qdot.x *= 0.5f; qdot.y *= 0.5f; qdot.z *= 0.5f; qdot.w *= 0.5f;

        q.x += qdot.x * (float)dt;
        q.y += qdot.y * (float)dt;
        q.z += qdot.z * (float)dt;
        q.w += qdot.w * (float)dt;

        if (normalizeEachStep) q = Normalize(q);

        att.qBE = q;
    }

    private static Quaternion Mul(Quaternion a, Quaternion b)
    {
        return new Quaternion(
            a.w*b.x + a.x*b.w + a.y*b.z - a.z*b.y,
            a.w*b.y - a.x*b.z + a.y*b.w + a.z*b.x,
            a.w*b.z + a.x*b.y - a.y*b.x + a.z*b.w,
            a.w*b.w - a.x*b.x - a.y*b.y - a.z*b.z
        );
    }

    private static Quaternion Normalize(Quaternion q)
    {
        float m = Mathf.Sqrt(q.x*q.x + q.y*q.y + q.z*q.z + q.w*q.w);
        if (m < 1e-8f) return Quaternion.identity;
        float inv = 1.0f / m;
        q.x *= inv; q.y *= inv; q.z *= inv; q.w *= inv;
        return q;
    }
}
