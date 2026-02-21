using UdonSharp;
using UnityEngine;
using System;

public class ConicFitter : UdonSharpBehaviour
{
    [Header("References")]
    public BodyCatalog bodies;
    public CraftStateModel craft;
    public ConicState conic;

    [Header("Tolerances")]
    public double eTol = 1e-6;     // treat smaller as circular
    public double iTolRad = 1e-6;  // treat smaller as equatorial

    [Header("Debug")]
    public bool logFits = false;

    /// <summary>
    /// Fit osculating conic about newPrimaryId at time tNow (seconds).
    /// Elements are in the SOLVER inertial frame (heliocentric ecliptic inertial),
    /// and the state is primary-relative (craft - primary).
    /// </summary>
    public void Fit(byte newPrimaryId, double tNow)
    {
        if (bodies == null || craft == null || conic == null) return;

        // Relative state about primary in solver inertial frame
        double rx, ry, rz, vx, vy, vz;
        bodies.ToPrimaryRelative(newPrimaryId, craft, out rx, out ry, out rz, out vx, out vy, out vz);

        double mu = bodies.GetMu(newPrimaryId);
        if (mu <= 0.0) return;

        // Magnitudes
        double rMag = Math.Sqrt(rx * rx + ry * ry + rz * rz);
        double vMag = Math.Sqrt(vx * vx + vy * vy + vz * vz);
        if (rMag <= 0.0) return;

        // Specific angular momentum h = r x v
        double hx = ry * vz - rz * vy;
        double hy = rz * vx - rx * vz;
        double hz = rx * vy - ry * vx;
        double hMag = Math.Sqrt(hx * hx + hy * hy + hz * hz);
        if (hMag <= 0.0) return;

        // Inclination (about +Z pole of solver inertial frame)
        double iRad = Math.Acos(Clamp(hz / hMag, -1.0, 1.0));

        // Node vector n = k x h = (0,0,1) x h = (-hy, hx, 0)
        double nx = -hy;
        double ny =  hx;
        double nMag = Math.Sqrt(nx * nx + ny * ny);

        // Eccentricity vector evec = (v x h)/mu - r/|r|
        double vxh_x = vy * hz - vz * hy;
        double vxh_y = vz * hx - vx * hz;
        double vxh_z = vx * hy - vy * hx;

        double ex = (vxh_x / mu) - (rx / rMag);
        double ey = (vxh_y / mu) - (ry / rMag);
        double ez = (vxh_z / mu) - (rz / rMag);
        double e = Math.Sqrt(ex * ex + ey * ey + ez * ez);

        // Specific energy and semi-major axis
        double energy = 0.5 * vMag * vMag - mu / rMag;
        if (Math.Abs(energy) < 1e-14)
        {
            // Near-parabolic: skip for V1
            return;
        }
        double a = -mu / (2.0 * energy);

        // --- Handle singularities robustly ---
        bool equatorial = (iRad < iTolRad);
        bool circular   = (e < eTol);

        double raan = 0.0;
        if (!equatorial && nMag > 1e-12)
        {
            // Ω = atan2(ny, nx)
            raan = Math.Atan2(ny, nx);
            raan = Wrap2Pi(raan);
        }

        double argp = 0.0;
        if (!circular && !equatorial && nMag > 1e-12)
        {
            // ω from node to evec
            double ndote = nx * ex + ny * ey; // nz=0
            double cosw = Clamp(ndote / (nMag * e), -1.0, 1.0);

            // sign from (n x e) · h
            double c_x = ny * ez;
            double c_y = -nx * ez;
            double c_z = nx * ey - ny * ex;
            double sign = c_x * hx + c_y * hy + c_z * hz;

            double w = Math.Acos(cosw);
            argp = (sign >= 0.0) ? w : (2.0 * Math.PI - w);
            argp = Wrap2Pi(argp);
        }
        else if (!circular && equatorial)
        {
            // Equatorial but eccentric: longitude of periapsis ϖ = atan2(e_y, e_x)
            raan = 0.0;
            argp = Wrap2Pi(Math.Atan2(ey, ex));
        }

        // True anomaly ν (or substitute angle for circular cases)
        double nu = 0.0;

        if (!circular)
        {
            // ν from evec to r
            double edotr = ex * rx + ey * ry + ez * rz;
            double cosnu = Clamp(edotr / (e * rMag), -1.0, 1.0);

            // sign from (e x r) · h
            double er_x = ey * rz - ez * ry;
            double er_y = ez * rx - ex * rz;
            double er_z = ex * ry - ey * rx;
            double sign = er_x * hx + er_y * hy + er_z * hz;

            double nuth = Math.Acos(cosnu);
            nu = (sign >= 0.0) ? nuth : (2.0 * Math.PI - nuth);
            nu = Wrap2Pi(nu);
        }
        else
        {
            // Circular: use "true longitude" ℓ = atan2(y,x) in inertial XY plane if equatorial,
            // or "argument of latitude" u = angle from node to r for inclined circular.
            if (equatorial)
            {
                nu = Wrap2Pi(Math.Atan2(ry, rx));
                raan = 0.0;
                argp = 0.0;
            }
            else
            {
                if (nMag > 1e-12)
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
                    nu = Wrap2Pi(u);

                    argp = 0.0;
                }
                else
                {
                    nu = Wrap2Pi(Math.Atan2(ry, rx));
                }
            }
        }

        // Mean anomaly at epoch
        double M0 = 0.0;

        if (circular)
        {
            M0 = WrapPi(nu);
        }
        else if (e < 1.0)
        {
            // Elliptic: ν -> E -> M
            double E = TrueToEccentricAnomaly_Elliptic(nu, e);
            M0 = E - e * Math.Sin(E);
            M0 = WrapPi(M0);
        }
        else
        {
            // Hyperbolic: ν -> H -> M, with M = e*sinh(H) - H
            double cosNu = Math.Cos(nu);
            double sinNu = Math.Sin(nu);
            double denom = 1.0 + e * cosNu;

            if (Math.Abs(denom) < 1e-12)
                denom = (denom >= 0.0) ? 1e-12 : -1e-12;

            double fac = Math.Sqrt(e * e - 1.0);
            double sinhH = (fac * sinNu) / denom;
            double H = Asinh(sinhH);

            M0 = e * sinhH - H; // do NOT wrap for hyperbolic
        }

        // Write conic
        conic.primaryBodyId = newPrimaryId;
        conic.epochT0 = tNow;
        conic.M0Rad = M0;

        conic.aMeters = a;
        conic.e = e;
        conic.iRad = iRad;
        conic.raanRad = raan;
        conic.argpRad = argp;

        conic.valid = true;

        if (logFits)
        {
            Debug.Log($"[ConicFitter] Fit primary={newPrimaryId} a={a:F0} e={e:F6} i={iRad*57.29578:F3}deg M0={M0:F4} t0={tNow:F2}");
        }
    }

    private static double TrueToEccentricAnomaly_Elliptic(double nu, double e)
    {
        double t = Math.Tan(0.5 * nu);
        double s = Math.Sqrt((1.0 - e) / (1.0 + e));
        double E = 2.0 * Math.Atan(s * t);
        return Wrap2Pi(E);
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

    private static double Asinh(double x)
    {
        return Math.Log(x + Math.Sqrt(x * x + 1.0));
    }

    private static double Clamp(double x, double lo, double hi)
    {
        if (x < lo) return lo;
        if (x > hi) return hi;
        return x;
    }
}
