using UdonSharp;
using UnityEngine;
using System;

public class OrbitDiagnostics : UdonSharpBehaviour
{
    [Header("References")]
    public EphemSnapshot ephem;     // (not strictly required anymore, but keep if you want)
    public CraftStateModel craft;
    public BodyCatalog bodies;

    [Header("Outputs (osculating elements, radians)")]
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
    public bool valid;
    public double rMeters;
    public double vMetersPerSec;
    public double specificEnergy;
    public double hMag;

    public void Evaluate()
    {
        valid = false;
        if (craft == null || bodies == null) return;

        byte primaryId = craft.primaryBodyId;

        double px, py, pz, pvx, pvy, pvz, mu;
        if (!TryGetPrimaryStateFromEphem(primaryId, out px, out py, out pz, out pvx, out pvy, out pvz, out mu))
            return;

        // double mu = bodies.GetMu(primaryId);
        if (mu <= 0.0) return;

        // Relative state in SOLVER INERTIAL (ecliptic) frame: r = craft - primary
        double rx = craft.rx - px;
        double ry = craft.ry - py;
        double rz = craft.rz - pz;

        double vx = craft.vx - pvx;
        double vy = craft.vy - pvy;
        double vz = craft.vz - pvz;

        double rMag = Math.Sqrt(rx*rx + ry*ry + rz*rz);
        double vMag = Math.Sqrt(vx*vx + vy*vy + vz*vz);
        rMeters = rMag;
        vMetersPerSec = vMag;

        if (rMag < 1e-9) return;

        // h = r x v
        double hx = ry*vz - rz*vy;
        double hy = rz*vx - rx*vz;
        double hz = rx*vy - ry*vx;
        hMag = Math.Sqrt(hx*hx + hy*hy + hz*hz);
        if (hMag < 1e-12) return;

        // Inclination (k = +Z)
        iRad = Math.Acos(Clamp(hz / hMag, -1.0, 1.0));

        // Node vector n = k x h = (-hy, hx, 0)
        double nx = -hy;
        double ny =  hx;
        double nz =  0.0;
        double nMag = Math.Sqrt(nx*nx + ny*ny);

        // evec = (v x h)/mu - r/|r|
        double vxh_x = vy*hz - vz*hy;
        double vxh_y = vz*hx - vx*hz;
        double vxh_z = vx*hy - vy*hx;

        double ex = (vxh_x / mu) - (rx / rMag);
        double ey = (vxh_y / mu) - (ry / rMag);
        double ez = (vxh_z / mu) - (rz / rMag);
        e = Math.Sqrt(ex*ex + ey*ey + ez*ez);

        // Specific energy
        specificEnergy = 0.5 * vMag * vMag - mu / rMag;
        if (Math.Abs(specificEnergy) < 1e-12) return; // near-parabolic: skip in V1

        // Semi-major axis
        aMeters = -mu / (2.0 * specificEnergy);

        // RAAN Ω
        if (nMag < 1e-12) raanRad = 0.0;
        else
        {
            raanRad = Math.Atan2(ny, nx);
            if (raanRad < 0.0) raanRad += 2.0*Math.PI;
        }

        // Argument of periapsis ω
        if (nMag < 1e-12 || e < 1e-12) argpRad = 0.0;
        else
        {
            double ndote = nx*ex + ny*ey + nz*ez;
            double cosw = Clamp(ndote / (nMag * e), -1.0, 1.0);

            // sign = (n x e) · h
            double c_x = ny*ez - nz*ey;
            double c_y = nz*ex - nx*ez;
            double c_z = nx*ey - ny*ex;
            double sign = c_x*hx + c_y*hy + c_z*hz;

            double w = Math.Acos(cosw);
            argpRad = (sign >= 0.0) ? w : (2.0*Math.PI - w);
        }

        // True anomaly ν
        if (e < 1e-12)
        {
            // Circular: measure from node if possible, otherwise from +X
            if (nMag < 1e-12)
            {
                nuRad = Math.Atan2(ry, rx);
                if (nuRad < 0.0) nuRad += 2.0*Math.PI;
            }
            else
            {
                double ndotr = nx*rx + ny*ry + nz*rz;
                double cosnu = Clamp(ndotr / (nMag * rMag), -1.0, 1.0);

                // sign = (n x r) · h
                double nr_x = ny*rz - nz*ry;
                double nr_y = nz*rx - nx*rz;
                double nr_z = nx*ry - ny*rx;
                double sign = nr_x*hx + nr_y*hy + nr_z*hz;

                double nu = Math.Acos(cosnu);
                nuRad = (sign >= 0.0) ? nu : (2.0*Math.PI - nu);
            }
        }
        else
        {
            double edotr = ex*rx + ey*ry + ez*rz;
            double cosnu = Clamp(edotr / (e * rMag), -1.0, 1.0);

            // sign = (e x r) · h
            double er_x = ey*rz - ez*ry;
            double er_y = ez*rx - ex*rz;
            double er_z = ex*ry - ey*rx;
            double sign = er_x*hx + er_y*hy + er_z*hz;

            double nu = Math.Acos(cosnu);
            nuRad = (sign >= 0.0) ? nu : (2.0*Math.PI - nu);
        }

        // Derived
        periapsisMeters = aMeters * (1.0 - e);
        apoapsisMeters  = aMeters * (1.0 + e);

        if (aMeters > 0.0)
            periodSeconds = 2.0 * Math.PI * Math.Sqrt(aMeters*aMeters*aMeters / mu);
        else
            periodSeconds = 0.0;

        valid = true;
    }


    private bool TryGetPrimaryStateFromEphem(byte primaryId,
        out double px, out double py, out double pz,
        out double pvx, out double pvy, out double pvz,
        out double mu)
    {
        px = py = pz = 0.0;
        pvx = pvy = pvz = 0.0;
        mu = 0.0;

        if (ephem == null || bodies == null) return false;

        // Prefer ephem for the Big 3 (match renderer)
        if (primaryId == bodies.sunId)
        {
            px = ephem.sun_rx; py = ephem.sun_ry; pz = ephem.sun_rz;
            pvx = ephem.sun_vx; pvy = ephem.sun_vy; pvz = ephem.sun_vz;
            mu = bodies.GetMu(primaryId);
            return true;
        }
        if (primaryId == bodies.earthId)
        {
            px = ephem.earth_rx; py = ephem.earth_ry; pz = ephem.earth_rz;
            pvx = ephem.earth_vx; pvy = ephem.earth_vy; pvz = ephem.earth_vz;
            mu = bodies.GetMu(primaryId);
            return true;
        }
        if (primaryId == bodies.moonId)
        {
            px = ephem.moon_rx; py = ephem.moon_ry; pz = ephem.moon_rz;
            pvx = ephem.moon_vx; pvy = ephem.moon_vy; pvz = ephem.moon_vz;
            mu = bodies.GetMu(primaryId);
            return true;
        }

        // Fallback for other bodies (if any)
        bodies.GetBodyPos(primaryId, out px, out py, out pz);
        bodies.GetBodyVel(primaryId, out pvx, out pvy, out pvz);
        mu = bodies.GetMu(primaryId);
        return (mu > 0.0);
    }

    private static double Clamp(double x, double lo, double hi)
    {
        if (x < lo) return lo;
        if (x > hi) return hi;
        return x;
    }
}
