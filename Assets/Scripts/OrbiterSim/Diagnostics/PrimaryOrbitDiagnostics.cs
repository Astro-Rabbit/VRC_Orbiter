using UdonSharp;
using UnityEngine;
using System;

public class PrimaryOrbitDiagnostics : UdonSharpBehaviour
{
    [Header("References")]
    public BodyCatalog bodies;
    public CraftStateModel craft;

    [Header("Tolerances")]
    public double eTol = 1e-6;
    public double iTolRad = 1e-6;

    [Header("Outputs: Primary-equatorial elements (radians)")]
    public byte primaryId;
    public bool valid;

    public double aMeters;
    public double e;
    public double iRad;
    public double raanRad;
    public double argpRad;
    public double nuRad;

    [Header("Derived")]
    public double periapsisMeters;
    public double apoapsisMeters;
    public double periodSeconds;

    [Header("Debug")]
    public double rMeters;
    public double vMetersPerSec;

    // Primary-equatorial inertial basis expressed in SOLVER frame (unit vectors)
    [Header("Basis (solver frame)")]
    public Vector3 I_hat;
    public Vector3 J_hat;
    public Vector3 K_hat;

    public void Evaluate()
    {
        valid = false;
        if (bodies == null || craft == null) return;

        primaryId = craft.primaryBodyId;

        // Primary-relative state in solver frame
        double rx, ry, rz, vx, vy, vz;
        bodies.ToPrimaryRelative(primaryId, craft, out rx, out ry, out rz, out vx, out vy, out vz);

        double mu = bodies.GetMu(primaryId);
        if (mu <= 0.0) return;

        // Build primary-equatorial inertial basis (I,J,K) in solver frame
        if (!BuildEquatorialBasis(primaryId, out I_hat, out J_hat, out K_hat))
            return;

        // Transform r,v into primary-equatorial coordinates
        double rIx = rx * I_hat.x + ry * I_hat.y + rz * I_hat.z;
        double rJy = rx * J_hat.x + ry * J_hat.y + rz * J_hat.z;
        double rKz = rx * K_hat.x + ry * K_hat.y + rz * K_hat.z;

        double vIx = vx * I_hat.x + vy * I_hat.y + vz * I_hat.z;
        double vJy = vx * J_hat.x + vy * J_hat.y + vz * J_hat.z;
        double vKz = vx * K_hat.x + vy * K_hat.y + vz * K_hat.z;

        // Run standard element fit in this local frame where +Z is the primary pole
        FitFromState(mu, rIx, rJy, rKz, vIx, vJy, vKz);
    }

    private void FitFromState(double mu,
        double rx, double ry, double rz,
        double vx, double vy, double vz)
    {
        double rMag = Math.Sqrt(rx * rx + ry * ry + rz * rz);
        double vMag = Math.Sqrt(vx * vx + vy * vy + vz * vz);
        rMeters = rMag;
        vMetersPerSec = vMag;
        if (rMag <= 0.0) return;

        // h = r x v
        double hx = ry * vz - rz * vy;
        double hy = rz * vx - rx * vz;
        double hz = rx * vy - ry * vx;
        double hMag = Math.Sqrt(hx * hx + hy * hy + hz * hz);
        if (hMag <= 0.0) return;

        iRad = Math.Acos(Clamp(hz / hMag, -1.0, 1.0));

        // n = k x h with k=(0,0,1) => (-hy, hx, 0)
        double nx = -hy;
        double ny =  hx;
        double nMag = Math.Sqrt(nx * nx + ny * ny);

        // evec = (v x h)/mu - r/|r|
        double vxh_x = vy * hz - vz * hy;
        double vxh_y = vz * hx - vx * hz;
        double vxh_z = vx * hy - vy * hx;

        double ex = (vxh_x / mu) - (rx / rMag);
        double ey = (vxh_y / mu) - (ry / rMag);
        double ez = (vxh_z / mu) - (rz / rMag);
        e = Math.Sqrt(ex * ex + ey * ey + ez * ez);

        double energy = 0.5 * vMag * vMag - mu / rMag;
        if (Math.Abs(energy) < 1e-14) return;
        aMeters = -mu / (2.0 * energy);

        bool equatorial = (iRad < iTolRad);
        bool circular = (e < eTol);

        raanRad = 0.0;
        if (!equatorial && nMag > 1e-12)
            raanRad = Wrap2Pi(Math.Atan2(ny, nx));

        argpRad = 0.0;
        if (!circular && !equatorial && nMag > 1e-12)
        {
            double ndote = nx * ex + ny * ey;
            double cosw = Clamp(ndote / (nMag * e), -1.0, 1.0);

            // sign from (n x e) · h
            double c_x = ny * ez;
            double c_y = -nx * ez;
            double c_z = nx * ey - ny * ex;
            double sign = c_x * hx + c_y * hy + c_z * hz;

            double w = Math.Acos(cosw);
            argpRad = Wrap2Pi((sign >= 0.0) ? w : (2.0 * Math.PI - w));
        }
        else if (!circular && equatorial)
        {
            // longitude of periapsis
            raanRad = 0.0;
            argpRad = Wrap2Pi(Math.Atan2(ey, ex));
        }

        nuRad = 0.0;
        if (!circular)
        {
            double edotr = ex * rx + ey * ry + ez * rz;
            double cosnu = Clamp(edotr / (e * rMag), -1.0, 1.0);

            // sign from (e x r) · h
            double er_x = ey * rz - ez * ry;
            double er_y = ez * rx - ex * rz;
            double er_z = ex * ry - ey * rx;
            double sign = er_x * hx + er_y * hy + er_z * hz;

            double nuth = Math.Acos(cosnu);
            nuRad = Wrap2Pi((sign >= 0.0) ? nuth : (2.0 * Math.PI - nuth));
        }
        else
        {
            if (equatorial)
            {
                nuRad = Wrap2Pi(Math.Atan2(ry, rx));
                raanRad = 0.0;
                argpRad = 0.0;
            }
            else if (nMag > 1e-12)
            {
                double ndotr = nx * rx + ny * ry;
                double cosu = Clamp(ndotr / (nMag * rMag), -1.0, 1.0);

                // sign from (n x r) · h
                double nr_x = ny * rz;
                double nr_y = -nx * rz;
                double nr_z = nx * ry - ny * rx;
                double sign = nr_x * hx + nr_y * hy + nr_z * hz;

                double u = Math.Acos(cosu);
                u = (sign >= 0.0) ? u : (2.0 * Math.PI - u);
                nuRad = Wrap2Pi(u);
                argpRad = 0.0;
            }
            else
            {
                nuRad = Wrap2Pi(Math.Atan2(ry, rx));
            }
        }

        periapsisMeters = aMeters * (1.0 - e);
        apoapsisMeters = aMeters * (1.0 + e);

        if (aMeters > 0.0)
            periodSeconds = 2.0 * Math.PI * Math.Sqrt(aMeters * aMeters * aMeters / mu);
        else
            periodSeconds = 0.0;

        valid = true;
    }

    private bool BuildEquatorialBasis(byte bodyId, out Vector3 I, out Vector3 J, out Vector3 K)
    {
        I = Vector3.right;
        J = Vector3.up;
        K = Vector3.forward;

        // Body-fixed -> inertial (solver frame)
        Quaternion qBI = bodies.GetBodyFixedToInertial(bodyId);
        Vector3 k = qBI * Vector3.forward; // body +Z in solver frame
        if (k.sqrMagnitude < 1e-12f) return false;
        k.Normalize();

        // Choose a stable inertial reference direction
        Vector3 refI = Vector3.right; // solver +X
        float d = Mathf.Abs(Vector3.Dot(refI, k));
        if (d > 0.9f) refI = Vector3.up; // solver +Y fallback

        // Project ref into equatorial plane
        Vector3 i = refI - Vector3.Dot(refI, k) * k;
        if (i.sqrMagnitude < 1e-12f) return false;
        i.Normalize();

        Vector3 j = Vector3.Cross(k, i);
        if (j.sqrMagnitude < 1e-12f) return false;
        j.Normalize();

        I = i; J = j; K = k;
        return true;
    }

    private static double Wrap2Pi(double a)
    {
        double twoPi = 2.0 * Math.PI;
        a = a % twoPi;
        if (a < 0.0) a += twoPi;
        return a;
    }

    private static double Clamp(double x, double lo, double hi)
    {
        if (x < lo) return lo;
        if (x > hi) return hi;
        return x;
    }
}
