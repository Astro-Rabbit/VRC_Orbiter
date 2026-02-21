using UdonSharp;
using UnityEngine;
using System;

public class OrbitRenderer : UdonSharpBehaviour
{
    [Header("References")]
    public OrbitDiagnostics diag;
    public OrreryRenderer orrery;
    public CraftStateModel craft;
    public LineRenderer line;
    public BodyCatalog bodies;
    public EphemSnapshot ephem;


    [Header("Display")]
    public bool show = true;
    [Range(32, 1024)]
    public int samples = 256;

    [Tooltip("Draw only this many seconds ahead/behind for hyperbolic or fallback cases.")]
    public double fallbackTimeSpanS = 7200.0; // (unused in this V1 fallback)

    [Header("Hyperbolic drawing")]
    public bool drawHyperbolic = true;

    [Tooltip("Max radius from primary (meters) to draw hyperbolic arc.")]
    public double hyperRMaxMeters = 2.0e8;

    [Tooltip("If true, set rMax to 2×Moon SOI when primary is Moon.")]
    public bool hyperUseSOIBasedRMax = true;

    [Header("Debug")]
    public bool logWarnings = false;

    public void Apply()
    {
        if (!show)
        {
            if (line != null) line.enabled = false;
            return;
        }

        if (line == null || diag == null || orrery == null || bodies == null)
        {
            if (logWarnings) Debug.Log("[OrbitRenderer] Missing refs (line/diag/orrery/bodies).");
            return;
        }

        line.enabled = true;

        if (craft == null)
        {
            line.positionCount = 0;
            return;
        }

        // Evaluate diag if caller didn’t do it this frame
        if (!diag.valid) diag.Evaluate();

        if (!diag.valid)
        {
            if (logWarnings) Debug.Log("[OrbitRenderer] Diagnostics invalid; skipping orbit line.");
            line.positionCount = 0;
            return;
        }

        byte primaryId = craft.primaryBodyId;

        // Primary body position in SOLVER world meters
        
        double px, py, pz;
        if (!TryGetPrimaryPosFromEphem(primaryId, out px, out py, out pz))
        {
            line.positionCount = 0;
            return;
        }

        int N = Math.Max(32, samples);
        line.positionCount = N;

        double a = diag.aMeters;
        double e = diag.e;

        bool isElliptic = (a > 0.0 && e < 1.0);

        if (!isElliptic)
        {
            if (drawHyperbolic && e > 1.0 && a < 0.0)
            {
                DrawHyperbola(px, py, pz, a, e, N, primaryId);
                return;
            }

            DrawFallbackArc(px, py, pz, N);
            return;
        }

        // Semi-latus rectum p = a(1-e^2)
        double p = a * (1.0 - e * e);

        // PQW -> inertial rotation using diag Ω,i,ω (STANDARD: +Z pole)
        double raan = diag.raanRad;
        double inc  = diag.iRad;
        double argp = diag.argpRad;

        double cO = Math.Cos(raan);
        double sO = Math.Sin(raan);
        double ci = Math.Cos(inc);
        double si = Math.Sin(inc);
        double cw = Math.Cos(argp);
        double sw = Math.Sin(argp);

        double m00 =  cO * cw - sO * sw * ci;
        double m01 = -cO * sw - sO * cw * ci;
        double m02 =  sO * si;

        double m10 =  sO * cw + cO * sw * ci;
        double m11 = -sO * sw + cO * cw * ci;
        double m12 = -cO * si;

        double m20 =  sw * si;
        double m21 =  cw * si;
        double m22 =  ci;

        for (int i = 0; i < N; i++)
        {
            double nu = (2.0 * Math.PI) * ((double)i / (double)(N - 1));

            double cosNu = Math.Cos(nu);
            double sinNu = Math.Sin(nu);
            double r = p / (1.0 + e * cosNu);

            // PQW
            double xpf = r * cosNu;
            double ypf = r * sinNu;

            // Inertial (solver)
            double xs = m00 * xpf + m01 * ypf;
            double ys = m10 * xpf + m11 * ypf;
            double zs = m20 * xpf + m21 * ypf;

            // Absolute solver world meters
            double wx = px + xs;
            double wy = py + ys;
            double wz = pz + zs;

            line.SetPosition(i, orrery.MapWorldMetersToUnity(wx, wy, wz));
        }
    }

    private void DrawFallbackArc(double px, double py, double pz, int N)
    {
        // Simple fallback: draw a small circle in the primary's XY plane (solver ecliptic plane if primary is on it)
        double radius = 2000000.0;

        for (int i = 0; i < N; i++)
        {
            double ang = (2.0 * Math.PI) * ((double)i / (double)(N - 1));
            double c = Math.Cos(ang);
            double s = Math.Sin(ang);

            double wx = px + radius * c;
            double wy = py + radius * s;
            double wz = pz + 0.0;

            line.SetPosition(i, orrery.MapWorldMetersToUnity(wx, wy, wz));
        }
    }

    private void DrawHyperbola(double px, double py, double pz, double a, double e, int N, byte primaryId)
    {
        double rMax = hyperRMaxMeters;

        if (hyperUseSOIBasedRMax && bodies != null)
        {
            if (primaryId == bodies.moonId)
            {
                double rSOI = bodies.GetSOIRadius(bodies.moonId);
                if (rSOI > 0.0) rMax = 2.0 * rSOI;
            }
        }

        // p = a(1 - e^2) ; a < 0, (1-e^2) < 0 => p > 0
        double p = a * (1.0 - e * e);
        if (p <= 0.0) { line.positionCount = 0; return; }

        // r = p / (1 + e cos nu) = rMax  => cos nu = (p/rMax - 1)/e
        double cosNuMax = (p / rMax - 1.0) / e;
        cosNuMax = Clamp(cosNuMax, -0.999999, 0.999999);
        double nuMax = Math.Acos(cosNuMax);

        if (nuMax < 1e-6)
        {
            DrawFallbackArc(px, py, pz, N);
            return;
        }

        double raan = diag.raanRad;
        double inc  = diag.iRad;
        double argp = diag.argpRad;

        double cO = Math.Cos(raan);
        double sO = Math.Sin(raan);
        double ci = Math.Cos(inc);
        double si = Math.Sin(inc);
        double cw = Math.Cos(argp);
        double sw = Math.Sin(argp);

        double m00 =  cO * cw - sO * sw * ci;
        double m01 = -cO * sw - sO * cw * ci;

        double m10 =  sO * cw + cO * sw * ci;
        double m11 = -sO * sw + cO * cw * ci;

        double m20 =  sw * si;
        double m21 =  cw * si;

        for (int i = 0; i < N; i++)
        {
            double u = (double)i / (double)(N - 1);
            double nu = (-nuMax) + (2.0 * nuMax) * u;

            double cosNu = Math.Cos(nu);
            double sinNu = Math.Sin(nu);

            double r = p / (1.0 + e * cosNu);

            double xpf = r * cosNu;
            double ypf = r * sinNu;

            double xs = m00 * xpf + m01 * ypf;
            double ys = m10 * xpf + m11 * ypf;
            double zs = m20 * xpf + m21 * ypf;

            double wx = px + xs;
            double wy = py + ys;
            double wz = pz + zs;

            line.SetPosition(i, orrery.MapWorldMetersToUnity(wx, wy, wz));
        }
    }

    private bool TryGetPrimaryPosFromEphem(byte primaryId, out double px, out double py, out double pz)
    {
        px = py = pz = 0.0;
        if (ephem == null || bodies == null) return false;

        if (primaryId == bodies.sunId)   { px = ephem.sun_rx;   py = ephem.sun_ry;   pz = ephem.sun_rz;   return true; }
        if (primaryId == bodies.earthId) { px = ephem.earth_rx; py = ephem.earth_ry; pz = ephem.earth_rz; return true; }
        if (primaryId == bodies.moonId)  { px = ephem.moon_rx;  py = ephem.moon_ry;  pz = ephem.moon_rz;  return true; }

        bodies.GetBodyPos(primaryId, out px, out py, out pz);
        return true;
    }


    private static double Clamp(double x, double lo, double hi)
    {
        if (x < lo) return lo;
        if (x > hi) return hi;
        return x;
    }
}
