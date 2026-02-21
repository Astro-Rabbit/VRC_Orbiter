using UdonSharp;
using UnityEngine;

public class GravityModel : UdonSharpBehaviour
{
    public BodyCatalog bodies;
    public ConicState conic;          // optional fallback
    public CraftStateModel craft;

    public double minR = 1.0;

    // New: evaluate gravity about a primary given body-centered inertial rRel
    public void EvaluatePrimaryRel(byte primaryId,
        double rx, double ry, double rz,
        out double ax, out double ay, out double az)
    {
        ax = ay = az = 0.0;
        if (bodies == null) return;

        double r2 = rx * rx + ry * ry + rz * rz;
        double minR2 = minR * minR;
        if (r2 < minR2) r2 = minR2;

        double r = System.Math.Sqrt(r2);
        double mu = bodies.GetMu(primaryId);
        if (mu <= 0.0) return;

        double invR3 = 1.0 / (r2 * r);
        double s = -mu * invR3;

        ax = s * rx;
        ay = s * ry;
        az = s * rz;
    }

    // Backwards-compatible: evaluate using craft/conic and bodies
    public void Evaluate(out double ax, out double ay, out double az)
    {
        ax = ay = az = 0.0;
        if (bodies == null || craft == null) return;

        // Prefer craft.primaryBodyId (authoritative in the new architecture)
        byte pid = craft.primaryBodyId;

        // Fallback if unset
        if (pid == 0 && conic != null) pid = conic.primaryBodyId;

        // Compute relative vector craft - primary in heliocentric inertial
        double px, py, pz;
        bodies.GetBodyPos(pid, out px, out py, out pz);

        double rx = craft.rx - px;
        double ry = craft.ry - py;
        double rz = craft.rz - pz;

        EvaluatePrimaryRel(pid, rx, ry, rz, out ax, out ay, out az);
    }
}
