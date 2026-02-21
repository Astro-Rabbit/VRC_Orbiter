using UdonSharp;
using UnityEngine;
using System;

public class EarthMoonEphemerisBarycentric : UdonSharpBehaviour
{
    [Header("Units / Constants")]
    public double AU_m = 149597870700.0;            // meters
    public double velDiffSeconds = 60.0;            // central-difference window (seconds)

    // Main entry point
    public void Evaluate(double jd,
        out double sun_rx, out double sun_ry, out double sun_rz,
        out double sun_vx, out double sun_vy, out double sun_vz,
        out double earth_rx, out double earth_ry, out double earth_rz,
        out double earth_vx, out double earth_vy, out double earth_vz,
        out double moon_rx, out double moon_ry, out double moon_rz,
        out double moon_vx, out double moon_vy, out double moon_vz)
    {
        // Sun at origin in this V1 heliocentric-ecliptic frame.
        sun_rx = sun_ry = sun_rz = 0.0;
        sun_vx = sun_vy = sun_vz = 0.0;

        // Positions at jd
        double ex, ey, ez, mx, my, mz;
        ComputePositionsOnly(jd, out ex, out ey, out ez, out mx, out my, out mz);

        earth_rx = ex; earth_ry = ey; earth_rz = ez;
        moon_rx  = mx; moon_ry  = my; moon_rz  = mz;

        // Velocities via central difference
        double dt = Math.Max(1.0, velDiffSeconds);
        double jd_dt = dt / 86400.0;

        double ex1, ey1, ez1, mx1, my1, mz1;
        double ex0, ey0, ez0, mx0, my0, mz0;

        ComputePositionsOnly(jd + jd_dt, out ex1, out ey1, out ez1, out mx1, out my1, out mz1);
        ComputePositionsOnly(jd - jd_dt, out ex0, out ey0, out ez0, out mx0, out my0, out mz0);

        double inv2dt = 1.0 / (2.0 * dt);

        earth_vx = (ex1 - ex0) * inv2dt;
        earth_vy = (ey1 - ey0) * inv2dt;
        earth_vz = (ez1 - ez0) * inv2dt;

        moon_vx = (mx1 - mx0) * inv2dt;
        moon_vy = (my1 - my0) * inv2dt;
        moon_vz = (mz1 - mz0) * inv2dt;
    }

    // --- Positions only (meters), ecliptic inertial ---
    private void ComputePositionsOnly(double jd,
        out double earth_rx, out double earth_ry, out double earth_rz,
        out double moon_rx, out double moon_ry, out double moon_rz)
    {
        // Julian days since J2000.0
        double d = jd - 2451545.0;

        // ---- Sun/Earth heliocentric (Earth is opposite Sun geocentric) ----
        // Stjarnhimlen-style:
        // L = mean longitude, g = mean anomaly, e = eccentricity
        // lambda = ecliptic longitude, R = distance (AU)
        double L = DegToRad(WrapDeg(280.460 + 0.9856474 * d));
        double g = DegToRad(WrapDeg(357.528 + 0.9856003 * d));

        // Ecliptic longitude of the Sun (geocentric) in radians
        double lambda = L + DegToRad(1.915) * Math.Sin(g) + DegToRad(0.020) * Math.Sin(2.0 * g);

        // Distance in AU
        double R_au = 1.00014 - 0.01671 * Math.Cos(g) - 0.00014 * Math.Cos(2.0 * g);

        // Sun geocentric ecliptic coordinates (Earth->Sun). Earth heliocentric is negative of that.
        double sun_geo_x = R_au * Math.Cos(lambda);
        double sun_geo_y = R_au * Math.Sin(lambda);
        double sun_geo_z = 0.0;

        // Earth heliocentric in AU
        double earth_au_x = -sun_geo_x;
        double earth_au_y = -sun_geo_y;
        double earth_au_z = -sun_geo_z;

        // Convert to meters
        earth_rx = earth_au_x * AU_m;
        earth_ry = earth_au_y * AU_m;
        earth_rz = earth_au_z * AU_m;

        // ---- Moon geocentric ecliptic (low-order series) ----
        // This is the classic Stjarnhimlen approximation for longitude/latitude/distance.
        // Output: Moon position relative to Earth in ecliptic coordinates.
        double Nm = DegToRad(WrapDeg(125.1228 - 0.0529538083 * d)); // longitude of ascending node
        double im = DegToRad(5.1454);                                // inclination
        double wm = DegToRad(WrapDeg(318.0634 + 0.1643573223 * d));   // argument of perigee
        double am = 60.2666;                                         // Earth radii
        double em = 0.054900;                                        // eccentricity
        double Mm = DegToRad(WrapDeg(115.3654 + 13.0649929509 * d));  // mean anomaly

        // Solve eccentric anomaly Em for Moon
        double Em = SolveKeplerE(Mm, em);

        // Moon position in its orbital plane (units: Earth radii)
        double xv = am * (Math.Cos(Em) - em);
        double yv = am * (Math.Sqrt(1.0 - em * em) * Math.Sin(Em));

        double vm = Math.Atan2(yv, xv);               // true anomaly
        double rm = Math.Sqrt(xv * xv + yv * yv);     // distance (Earth radii)

        // Convert to ecliptic rectangular (Earth-centered)
        double xh = rm * (Math.Cos(Nm) * Math.Cos(vm + wm) - Math.Sin(Nm) * Math.Sin(vm + wm) * Math.Cos(im));
        double yh = rm * (Math.Sin(Nm) * Math.Cos(vm + wm) + Math.Cos(Nm) * Math.Sin(vm + wm) * Math.Cos(im));
        double zh = rm * (Math.Sin(vm + wm) * Math.Sin(im));

        // Ecliptic lon/lat from that (for perturbation series)
        double lon = Math.Atan2(yh, xh);
        double lat = Math.Atan2(zh, Math.Sqrt(xh * xh + yh * yh));

        // Sun mean anomaly g already computed above.
        // Moon mean elongation D, argument of latitude F (standard approximations)
        double D  = DegToRad(WrapDeg(297.8501921 + 12.19074912 * d)); // mean elongation
        double F  = DegToRad(WrapDeg(93.2720950  + 13.22935024 * d)); // argument of latitude

        // Perturbations (arc degrees)
        // These terms are the commonly used “good enough” subset from the same family of approximations.
        lon += DegToRad(
              -1.274 * Math.Sin(Mm - 2.0 * D)
              +0.658 * Math.Sin(2.0 * D)
              -0.186 * Math.Sin(g)
              -0.059 * Math.Sin(2.0 * Mm - 2.0 * D)
              -0.057 * Math.Sin(Mm - 2.0 * D + g)
              +0.053 * Math.Sin(Mm + 2.0 * D)
              +0.046 * Math.Sin(2.0 * D - g)
              +0.041 * Math.Sin(Mm - g)
              -0.035 * Math.Sin(D)
              -0.031 * Math.Sin(Mm + g)
              -0.015 * Math.Sin(2.0 * F - 2.0 * D)
              +0.011 * Math.Sin(Mm - 4.0 * D)
        );

        lat += DegToRad(
              -0.173 * Math.Sin(F - 2.0 * D)
              -0.055 * Math.Sin(Mm - F - 2.0 * D)
              -0.046 * Math.Sin(Mm + F - 2.0 * D)
              +0.033 * Math.Sin(F + 2.0 * D)
              +0.017 * Math.Sin(2.0 * Mm + F)
        );

        // Distance perturbations (Earth radii)
        rm += (-0.58 * Math.Cos(Mm - 2.0 * D)
               -0.46 * Math.Cos(2.0 * D));

        // Convert corrected lon/lat/rm to rectangular (Earth radii)
        double rmCosLat = rm * Math.Cos(lat);
        double mx_er = rmCosLat * Math.Cos(lon);
        double my_er = rmCosLat * Math.Sin(lon);
        double mz_er = rm * Math.Sin(lat);

        // Earth radius to meters
        double Re_m = 6378137.0; // use WGS84-ish for consistent scaling

        // Moon geocentric in meters (ecliptic)
        double moon_geo_x = mx_er * Re_m;
        double moon_geo_y = my_er * Re_m;
        double moon_geo_z = mz_er * Re_m;

        // Moon heliocentric = Earth heliocentric + Moon geocentric
        moon_rx = earth_rx + moon_geo_x;
        moon_ry = earth_ry + moon_geo_y;
        moon_rz = earth_rz + moon_geo_z;
    }

    private double SolveKeplerE(double M, double e)
    {
        double E = (e < 0.8) ? M : Math.PI;
        for (int k = 0; k < 12; k++)
        {
            double sE = Math.Sin(E);
            double cE = Math.Cos(E);
            double f = E - e * sE - M;
            double fp = 1.0 - e * cE;
            if (Math.Abs(fp) < 1e-12) break;
            double d = f / fp;
            E -= d;
            if (Math.Abs(d) < 1e-12) break;
        }
        return E;
    }

    private double WrapDeg(double deg)
    {
        deg = deg % 360.0;
        if (deg < 0.0) deg += 360.0;
        return deg;
    }

    private double DegToRad(double deg)
    {
        return deg * Math.PI / 180.0;
    }
}
