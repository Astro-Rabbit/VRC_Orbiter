using UdonSharp;
using UnityEngine;
using System;

public class OrbitInitializerFromPrimaryElements : UdonSharpBehaviour
{
    [Header("References")]
    public EphemerisSystem ephemSystem; // optional but recommended for time-consistent snapshot
    public BodyCatalog bodies;
    public CraftStateModel craft;
    public ConicFitter fitter;

    [Header("Init Settings")]
    public byte primaryId = 1;          // 1=Earth by default
    public double t0Seconds = 0.0;      // scenario time

    [Header("Primary-equatorial elements input")]
    public double aMeters = 7000e3;
    public double e = 0.0;
    public double iRad = 0.0;
    public double raanRad = 0.0;
    public double argpRad = 0.0;

    [Header("Anomaly input (choose one)")]
    public bool useMeanAnomaly = true;
    public double M0Rad = 0.0;
    public double nu0Rad = 0.0;

    [Header("Behavior")]
    public bool autoFitConicAfterSettingState = true;

    [Header("Debug")]
    public bool logInit = false;

    public void InitializeNow()
    {
        if (bodies == null || craft == null) return;

        // Ensure snapshot corresponds to epoch if you have an ephemeris system
        if (ephemSystem != null)
            ephemSystem.Evaluate(t0Seconds);

        double mu = bodies.GetMu(primaryId);
        if (mu <= 0.0) return;

        // Build primary equatorial basis (I,J,K) in solver inertial
        Vector3 I, J, K;
        if (!BuildEquatorialBasis(primaryId, out I, out J, out K))
            return;

        // Determine true anomaly from selected anomaly input
        double nu = useMeanAnomaly ? MeanToTrueAnomaly(M0Rad, e) : Wrap2Pi(nu0Rad);

        // Compute r,v in PQW
        double r_pf_x, r_pf_y, v_pf_x, v_pf_y;
        if (!PQWStateFromAENu(mu, aMeters, e, nu, out r_pf_x, out r_pf_y, out v_pf_x, out v_pf_y))
            return;

        // Rotate PQW -> Equatorial inertial coordinates (in IJK basis coordinates)
        double rxE, ryE, rzE, vxE, vyE, vzE;
        PQWToInertial(r_pf_x, r_pf_y, v_pf_x, v_pf_y,
            raanRad, iRad, argpRad,
            out rxE, out ryE, out rzE,
            out vxE, out vyE, out vzE);

        // Map equatorial inertial coords into solver frame vectors
        double rxS = rxE * I.x + ryE * J.x + rzE * K.x;
        double ryS = rxE * I.y + ryE * J.y + rzE * K.y;
        double rzS = rxE * I.z + ryE * J.z + rzE * K.z;

        double vxS = vxE * I.x + vyE * J.x + vzE * K.x;
        double vyS = vxE * I.y + vyE * J.y + vzE * K.y;
        double vzS = vxE * I.z + vyE * J.z + vzE * K.z;

        // Compose with primary heliocentric inertial state
        double px, py, pz, pvx, pvy, pvz;
        bodies.GetBodyState(primaryId, out px, out py, out pz, out pvx, out pvy, out pvz);

        craft.primaryBodyId = primaryId;
        craft.rx = px + rxS;
        craft.ry = py + ryS;
        craft.rz = pz + rzS;

        craft.vx = pvx + vxS;
        craft.vy = pvy + vyS;
        craft.vz = pvz + vzS;

        if (logInit)
        {
            Debug.Log($"[OrbitInit] Set state about primary={primaryId} a={aMeters:F0} e={e:F5} i={iRad*57.29578:F3}deg nu={nu*57.29578:F3}deg t0={t0Seconds:F2}");
        }

        // Populate conic state for propagation using your canonical fitter in solver ecliptic frame
        if (autoFitConicAfterSettingState && fitter != null)
            fitter.Fit(primaryId, t0Seconds);
    }

    private bool BuildEquatorialBasis(byte bodyId, out Vector3 I, out Vector3 J, out Vector3 K)
    {
        I = Vector3.right;
        J = Vector3.up;
        K = Vector3.forward;

        Quaternion qBI = bodies.GetBodyFixedToInertial(bodyId);
        Vector3 k = qBI * Vector3.forward; // body +Z in solver frame
        if (k.sqrMagnitude < 1e-12f) return false;
        k.Normalize();

        Vector3 refI = Vector3.right;
        float d = Mathf.Abs(Vector3.Dot(refI, k));
        if (d > 0.9f) refI = Vector3.up;

        Vector3 i = refI - Vector3.Dot(refI, k) * k;
        if (i.sqrMagnitude < 1e-12f) return false;
        i.Normalize();

        Vector3 j = Vector3.Cross(k, i);
        if (j.sqrMagnitude < 1e-12f) return false;
        j.Normalize();

        I = i; J = j; K = k;
        return true;
    }

    // PQW state from (a,e,nu). Returns planar components (z=0).
    private bool PQWStateFromAENu(double mu, double a, double e, double nu,
        out double r_x, out double r_y, out double v_x, out double v_y)
    {
        r_x = r_y = v_x = v_y = 0.0;

        // V1: reject near-parabolic (a becomes ill-defined)
        if (Math.Abs(1.0 - e) < 1e-10) return false;

        // semi-latus rectum p = a(1 - e^2)
        double p = a * (1.0 - e * e);

        // For elliptic: a>0, e<1 => p>0. For hyperbolic: a<0, e>1 => (1-e^2)<0 so p>0 as well.
        if (p <= 0.0) return false;

        double cosNu = Math.Cos(nu);
        double sinNu = Math.Sin(nu);

        double r = p / (1.0 + e * cosNu);

        r_x = r * cosNu;
        r_y = r * sinNu;

        double s = Math.Sqrt(mu / p);
        v_x = -s * sinNu;
        v_y =  s * (e + cosNu);
        return true;
    }

    // Rotation PQW -> inertial (standard) using Ω,i,ω.
    // Outputs are coordinates in the equatorial inertial basis (IJK components).
    private void PQWToInertial(double r_pf_x, double r_pf_y, double v_pf_x, double v_pf_y,
        double raan, double inc, double argp,
        out double rx, out double ry, out double rz,
        out double vx, out double vy, out double vz)
    {
        double cO = Math.Cos(raan);
        double sO = Math.Sin(raan);
        double ci = Math.Cos(inc);
        double si = Math.Sin(inc);
        double cw = Math.Cos(argp);
        double sw = Math.Sin(argp);

        // Standard inertial rotation matrix (+Z pole of this local equatorial frame)
        double m00 =  cO * cw - sO * sw * ci;
        double m01 = -cO * sw - sO * cw * ci;
        double m02 =  sO * si;

        double m10 =  sO * cw + cO * sw * ci;
        double m11 = -sO * sw + cO * cw * ci;
        double m12 = -cO * si;

        double m20 =  sw * si;
        double m21 =  cw * si;
        double m22 =  ci;

        rx = m00 * r_pf_x + m01 * r_pf_y;
        ry = m10 * r_pf_x + m11 * r_pf_y;
        rz = m20 * r_pf_x + m21 * r_pf_y;

        vx = m00 * v_pf_x + m01 * v_pf_y;
        vy = m10 * v_pf_x + m11 * v_pf_y;
        vz = m20 * v_pf_x + m21 * v_pf_y;
    }

    private static double MeanToTrueAnomaly(double M, double e)
    {
        M = WrapPi(M);

        if (e < 1.0)
        {
            // Elliptic: solve Kepler E - e sinE = M
            double E = SolveKeplerE(M, e);
            // nu from E
            double cosE = Math.Cos(E);
            double sinE = Math.Sin(E);
            double fac = Math.Sqrt((1.0 + e) / (1.0 - e));
            double nu = 2.0 * Math.Atan2(fac * sinE, 1.0 + cosE);
            return Wrap2Pi(nu);
        }
        else
        {
            // Hyperbolic: solve e sinhH - H = M
            double H = SolveKeplerH(M, e);
            double coshH = Math.Cosh(H);
            double sinhH = Math.Sinh(H);
            double nu = Math.Atan2(Math.Sqrt(e * e - 1.0) * sinhH, e - coshH);
            return Wrap2Pi(nu);
        }
    }

    private static double SolveKeplerE(double M, double e)
    {
        double E = M;
        for (int k = 0; k < 16; k++)
        {
            double f = E - e * Math.Sin(E) - M;
            double fp = 1.0 - e * Math.Cos(E);
            if (Math.Abs(fp) < 1e-14) break;
            double d = f / fp;
            E -= d;
            if (Math.Abs(d) < 1e-12) break;
        }
        return E;
    }

    private static double SolveKeplerH(double M, double e)
    {
        // Initial guess
        double H = Math.Log(2.0 * Math.Abs(M) / e + 1.8);
        if (M < 0.0) H = -H;

        for (int k = 0; k < 20; k++)
        {
            double sinhH = Math.Sinh(H);
            double coshH = Math.Cosh(H);
            double f = e * sinhH - H - M;
            double fp = e * coshH - 1.0;
            if (Math.Abs(fp) < 1e-14) break;
            double d = f / fp;
            H -= d;
            if (Math.Abs(d) < 1e-12) break;
        }
        return H;
    }

    private static double Wrap2Pi(double a)
    {
        double twoPi = 2.0 * Math.PI;
        a = a % twoPi;
        if (a < 0.0) a += twoPi;
        return a;
    }

    private static double WrapPi(double a)
    {
        double twoPi = 2.0 * Math.PI;
        a = a % twoPi;
        if (a <= -Math.PI) a += twoPi;
        else if (a > Math.PI) a -= twoPi;
        return a;
    }
}
