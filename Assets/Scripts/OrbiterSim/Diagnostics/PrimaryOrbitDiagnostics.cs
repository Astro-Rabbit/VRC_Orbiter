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

    private bool _basisInit = false;
    private Vector3 _prevI;
    private Vector3 _prevJ;
    private Vector3 _prevK;
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

        double hxy = Math.Sqrt(hx * hx + hy * hy);
        iRad = Wrap2Pi(Math.Atan2(hxy, hz)); // 0..2pi but inclination is usually 0..pi; you can clamp if you prefer
        if (iRad > Math.PI) iRad = 2.0 * Math.PI - iRad;

        // n = k x h with k=(0,0,1) => (-hy, hx, 0)
        double nx = -hy;
        double ny =  hx;
        double nMag = Math.Sqrt(nx * nx + ny * ny);

        // RAAN (stable)
        raanRad = 0.0;
        bool equatorial = (iRad < iTolRad);
        if (!equatorial && nMag > 1e-12)
            raanRad = Wrap2Pi(Math.Atan2(ny, nx));

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

        bool circular = (e < eTol);

        // arg of periapsis ω
        argpRad = 0.0;
        if (!circular && !equatorial && nMag > 1e-12)
        {
            // ω = atan2( (k·(n×e)), (n·e) ), with k = (0,0,1)
            // n×e = (0,0, nx*ey - ny*ex) + (other terms with ez, but k·(...) only needs z component)
            double kdot_nxe = nx * ey - ny * ex;
            double ndote = nx * ex + ny * ey; // (since nz=0)
            argpRad = Wrap2Pi(Math.Atan2(kdot_nxe, ndote));
        }
        else if (!circular && equatorial)
        {
            // longitude of periapsis: atan2(ey, ex)
            raanRad = 0.0;
            argpRad = Wrap2Pi(Math.Atan2(ey, ex));
        }

        // true anomaly ν
        nuRad = 0.0;
        if (!circular)
        {
            // ν = atan2( (k·(e×r)), (e·r) )
            double kdot_exr = ex * ry - ey * rx; // z-component of e×r
            double edotr = ex * rx + ey * ry + ez * rz;
            nuRad = Wrap2Pi(Math.Atan2(kdot_exr, edotr));
        }
        else
        {
            // circular: use argument of latitude (or true longitude if equatorial)
            if (equatorial)
            {
                nuRad = Wrap2Pi(Math.Atan2(ry, rx));
                raanRad = 0.0;
                argpRad = 0.0;
            }
            else if (nMag > 1e-12)
            {
                // u = atan2( (k·(n×r)), (n·r) )
                double kdot_nxr = nx * ry - ny * rx;
                double ndotr = nx * rx + ny * ry;
                double u = Wrap2Pi(Math.Atan2(kdot_nxr, ndotr));
                nuRad = u;
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

    Quaternion qBI = bodies.GetBodyFixedToInertial(bodyId);

    // Primary pole (+Z body) expressed in solver frame
    Vector3 k = qBI * Vector3.forward;
    float k2 = k.sqrMagnitude;
    if (k2 < 1e-12f) return false;
    k *= 1.0f / Mathf.Sqrt(k2);

    // Choose a seed direction to define I in the equator plane.
    // Key idea: use previous I if possible -> continuity.
    Vector3 seed = _basisInit ? _prevI : Vector3.right;

    Vector3 i = seed - Vector3.Dot(seed, k) * k;
    float i2 = i.sqrMagnitude;

    // If degenerate, try fixed fallbacks (but *without* threshold flips)
    if (i2 < 1e-10f)
    {
        seed = Vector3.right;
        i = seed - Vector3.Dot(seed, k) * k;
        i2 = i.sqrMagnitude;

        if (i2 < 1e-10f)
        {
            seed = Vector3.up;
            i = seed - Vector3.Dot(seed, k) * k;
            i2 = i.sqrMagnitude;

            if (i2 < 1e-10f)
            {
                seed = Vector3.forward;
                i = seed - Vector3.Dot(seed, k) * k;
                i2 = i.sqrMagnitude;
                if (i2 < 1e-10f) return false;
            }
        }
    }

    i *= 1.0f / Mathf.Sqrt(i2);

    // Right-handed basis
    Vector3 j = Vector3.Cross(k, i);
    float j2 = j.sqrMagnitude;
    if (j2 < 1e-12f) return false;
    j *= 1.0f / Mathf.Sqrt(j2);

    // Enforce continuity (avoid 180° flips)
    if (_basisInit && Vector3.Dot(i, _prevI) < 0f)
    {
        i = -i;
        j = -j;
    }

    I = i; J = j; K = k;

    _prevI = i; _prevJ = j; _prevK = k;
    _basisInit = true;

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
