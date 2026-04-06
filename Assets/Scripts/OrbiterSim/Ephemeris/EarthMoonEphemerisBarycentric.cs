using UdonSharp;
using UnityEngine;
using System;

public class EarthMoonEphemerisBarycentric : UdonSharpBehaviour
{
    [Header("Units / Constants")]
    public double AU_m = 149597870700.0;     // meters
    public double velDiffSeconds = 60.0;     // kept only for inspector/backward compatibility; not used

    [Header("Earth-Moon scaling")]
    public double earthRadiusModelM = 6378137.0;   // legacy inspector compatibility
    public double moonMeanDistanceM = 385000560.0; // mean lunar distance

    // Main entry point
    public void Evaluate(double jd,
        out double sun_rx, out double sun_ry, out double sun_rz,
        out double sun_vx, out double sun_vy, out double sun_vz,
        out double earth_rx, out double earth_ry, out double earth_rz,
        out double earth_vx, out double earth_vy, out double earth_vz,
        out double moon_rx, out double moon_ry, out double moon_rz,
        out double moon_vx, out double moon_vy, out double moon_vz)
    {
        // Sun at origin in this V1 heliocentric-ecliptic inertial frame.
        sun_rx = sun_ry = sun_rz = 0.0;
        sun_vx = sun_vy = sun_vz = 0.0;

        ComputeEarthState(jd,
            out earth_rx, out earth_ry, out earth_rz,
            out earth_vx, out earth_vy, out earth_vz);

        double moon_geo_rx, moon_geo_ry, moon_geo_rz;
        double moon_geo_vx, moon_geo_vy, moon_geo_vz;

        ComputeMoonGeocentricState(jd,
            out moon_geo_rx, out moon_geo_ry, out moon_geo_rz,
            out moon_geo_vx, out moon_geo_vy, out moon_geo_vz);

        moon_rx = earth_rx + moon_geo_rx;
        moon_ry = earth_ry + moon_geo_ry;
        moon_rz = earth_rz + moon_geo_rz;

        moon_vx = earth_vx + moon_geo_vx;
        moon_vy = earth_vy + moon_geo_vy;
        moon_vz = earth_vz + moon_geo_vz;
    }

    // ------------------------------------------------------------------------
    // Earth heliocentric ecliptic state
    // Uses a simple Keplerian model with direct velocity instead of finite diff.
    // ------------------------------------------------------------------------
    private void ComputeEarthState(double jd,
        out double rx, out double ry, out double rz,
        out double vx, out double vy, out double vz)
    {
        double d = jd - 2451545.0; // days since J2000

        // Low-cost Earth orbit model (same class of approximation as before,
        // but evaluated as a direct state instead of position + finite diff)
        double a_AU = 1.00000011;
        double e = 0.01671022 - 0.00000000126 * d;

        // Longitude of perihelion (deg)
        double varpi_deg = 102.937348 + 0.0000470935 * d;

        // Mean anomaly (deg)
        double M_deg = 357.5291092 + 0.98560028 * d;

        double varpi = DegToRad(varpi_deg);
        double M = DegToRad(WrapDeg(M_deg));

        // Mean motion [rad/s]
        double Mdot = DegToRad(0.98560028) / 86400.0;

        double E = SolveKeplerE(M, e);
        double cosE = Math.Cos(E);
        double sinE = Math.Sin(E);

        double oneMinusECosE = 1.0 - e * cosE;
        double sqrt1me2 = Math.Sqrt(Math.Max(1e-15, 1.0 - e * e));

        // Orbital plane coordinates [AU]
        double x_orb = a_AU * (cosE - e);
        double y_orb = a_AU * (sqrt1me2 * sinE);

        // Direct derivatives in orbital plane [AU/s]
        double Edot = Mdot / Math.Max(1e-15, oneMinusECosE);
        double vx_orb = -a_AU * sinE * Edot;
        double vy_orb =  a_AU * sqrt1me2 * cosE * Edot;

        double cosw = Math.Cos(varpi);
        double sinw = Math.Sin(varpi);

        // Rotate from orbital plane into ecliptic plane
        double x_AU  = cosw * x_orb - sinw * y_orb;
        double y_AU  = sinw * x_orb + cosw * y_orb;
        double z_AU  = 0.0;

        double vx_AU = cosw * vx_orb - sinw * vy_orb;
        double vy_AU = sinw * vx_orb + cosw * vy_orb;
        double vz_AU = 0.0;

        rx = x_AU * AU_m;
        ry = y_AU * AU_m;
        rz = z_AU * AU_m;

        vx = vx_AU * AU_m;
        vy = vy_AU * AU_m;
        vz = vz_AU * AU_m;
    }

    // ------------------------------------------------------------------------
    // Moon geocentric ecliptic state
    // Truncated lunar theory with direct derivatives.
    //
    // Angles:
    //   L'  mean longitude of Moon
    //   D   mean elongation
    //   M   Sun mean anomaly
    //   M'  Moon mean anomaly
    //   F   Moon argument of latitude
    //
    // This is still not a high-precision ephemeris, but it is materially
    // better than the old low-order orbital-plane approximation and keeps
    // runtime cost very modest.
    // ------------------------------------------------------------------------
    private void ComputeMoonGeocentricState(double jd,
        out double rx, out double ry, out double rz,
        out double vx, out double vy, out double vz)
    {
        double d = jd - 2451545.0; // days since J2000

        // Fundamental arguments [deg]
        double Lp_deg = 218.3164477 + 13.17639648 * d; // mean longitude
        double D_deg  = 297.8501921 + 12.19074912 * d; // mean elongation
        double M_deg  = 357.5291092 + 0.98560028 * d;  // Sun mean anomaly
        double Mp_deg = 134.9633964 + 13.06499295 * d; // Moon mean anomaly
        double F_deg  = 93.2720950  + 13.22935024 * d; // argument of latitude

        double Lp = DegToRad(WrapDeg(Lp_deg));
        double D  = DegToRad(WrapDeg(D_deg));
        double M  = DegToRad(WrapDeg(M_deg));
        double Mp = DegToRad(WrapDeg(Mp_deg));
        double F  = DegToRad(WrapDeg(F_deg));

        // Angular rates [rad/s]
        double Lp_dot = DegToRad(13.17639648) / 86400.0;
        double D_dot  = DegToRad(12.19074912) / 86400.0;
        double M_dot  = DegToRad(0.98560028)  / 86400.0;
        double Mp_dot = DegToRad(13.06499295) / 86400.0;
        double F_dot  = DegToRad(13.22935024) / 86400.0;

        // ---- Longitude correction [deg] ----
        // lambda = L' + dLambda
        double dLambda =
              6.289 * Math.Sin(Mp)
            + 1.274 * Math.Sin(2.0 * D - Mp)
            + 0.658 * Math.Sin(2.0 * D)
            + 0.214 * Math.Sin(2.0 * Mp)
            - 0.186 * Math.Sin(M)
            - 0.114 * Math.Sin(2.0 * F)
            + 0.059 * Math.Sin(2.0 * D - 2.0 * Mp)
            + 0.057 * Math.Sin(2.0 * D - M - Mp)
            + 0.053 * Math.Sin(2.0 * D + Mp)
            + 0.046 * Math.Sin(2.0 * D - M)
            + 0.041 * Math.Sin(M - Mp)
            - 0.035 * Math.Sin(D)
            - 0.031 * Math.Sin(M + Mp)
            - 0.015 * Math.Sin(2.0 * F - 2.0 * D)
            + 0.011 * Math.Sin(Mp - 4.0 * D);

        double dLambda_dt_deg =
              6.289 * Math.Cos(Mp) * (Mp_dot)
            + 1.274 * Math.Cos(2.0 * D - Mp) * (2.0 * D_dot - Mp_dot)
            + 0.658 * Math.Cos(2.0 * D) * (2.0 * D_dot)
            + 0.214 * Math.Cos(2.0 * Mp) * (2.0 * Mp_dot)
            - 0.186 * Math.Cos(M) * (M_dot)
            - 0.114 * Math.Cos(2.0 * F) * (2.0 * F_dot)
            + 0.059 * Math.Cos(2.0 * D - 2.0 * Mp) * (2.0 * D_dot - 2.0 * Mp_dot)
            + 0.057 * Math.Cos(2.0 * D - M - Mp) * (2.0 * D_dot - M_dot - Mp_dot)
            + 0.053 * Math.Cos(2.0 * D + Mp) * (2.0 * D_dot + Mp_dot)
            + 0.046 * Math.Cos(2.0 * D - M) * (2.0 * D_dot - M_dot)
            + 0.041 * Math.Cos(M - Mp) * (M_dot - Mp_dot)
            - 0.035 * Math.Cos(D) * (D_dot)
            - 0.031 * Math.Cos(M + Mp) * (M_dot + Mp_dot)
            - 0.015 * Math.Cos(2.0 * F - 2.0 * D) * (2.0 * F_dot - 2.0 * D_dot)
            + 0.011 * Math.Cos(Mp - 4.0 * D) * (Mp_dot - 4.0 * D_dot);

        // ---- Latitude correction [deg] ----
        double beta_deg =
              5.128 * Math.Sin(F)
            + 0.280 * Math.Sin(Mp + F)
            + 0.277 * Math.Sin(Mp - F)
            + 0.173 * Math.Sin(2.0 * D - F)
            + 0.055 * Math.Sin(2.0 * D + F - Mp)
            + 0.046 * Math.Sin(2.0 * D - F - Mp)
            + 0.033 * Math.Sin(2.0 * D + F)
            + 0.017 * Math.Sin(2.0 * Mp + F);

        double beta_dt_deg =
              5.128 * Math.Cos(F) * (F_dot)
            + 0.280 * Math.Cos(Mp + F) * (Mp_dot + F_dot)
            + 0.277 * Math.Cos(Mp - F) * (Mp_dot - F_dot)
            + 0.173 * Math.Cos(2.0 * D - F) * (2.0 * D_dot - F_dot)
            + 0.055 * Math.Cos(2.0 * D + F - Mp) * (2.0 * D_dot + F_dot - Mp_dot)
            + 0.046 * Math.Cos(2.0 * D - F - Mp) * (2.0 * D_dot - F_dot - Mp_dot)
            + 0.033 * Math.Cos(2.0 * D + F) * (2.0 * D_dot + F_dot)
            + 0.017 * Math.Cos(2.0 * Mp + F) * (2.0 * Mp_dot + F_dot);

        // ---- Distance [km] ----
        double r_km =
              385000.56
            - 20905.0 * Math.Cos(Mp)
            - 3699.0  * Math.Cos(2.0 * D - Mp)
            - 2956.0  * Math.Cos(2.0 * D)
            - 570.0   * Math.Cos(2.0 * Mp)
            + 246.0   * Math.Cos(2.0 * Mp - 2.0 * D)
            - 205.0   * Math.Cos(M - 2.0 * D)
            - 171.0   * Math.Cos(Mp + 2.0 * D)
            - 152.0   * Math.Cos(Mp + M - 2.0 * D);

        double r_dt_km =
              -20905.0 * Math.Sin(Mp) * (Mp_dot)
            - 3699.0  * Math.Sin(2.0 * D - Mp) * (2.0 * D_dot - Mp_dot)
            - 2956.0  * Math.Sin(2.0 * D) * (2.0 * D_dot)
            - 570.0   * Math.Sin(2.0 * Mp) * (2.0 * Mp_dot)
            + 246.0   * Math.Sin(2.0 * Mp - 2.0 * D) * (2.0 * Mp_dot - 2.0 * D_dot)
            - 205.0   * Math.Sin(M - 2.0 * D) * (M_dot - 2.0 * D_dot)
            - 171.0   * Math.Sin(Mp + 2.0 * D) * (Mp_dot + 2.0 * D_dot)
            - 152.0   * Math.Sin(Mp + M - 2.0 * D) * (Mp_dot + M_dot - 2.0 * D_dot);

        double lambda = Lp + DegToRad(dLambda);
        double beta   = DegToRad(beta_deg);
        double r_m    = r_km * 1000.0;

        double lambda_dot = Lp_dot + DegToRad(dLambda_dt_deg);
        double beta_dot   = DegToRad(beta_dt_deg);
        double r_dot_m    = r_dt_km * 1000.0;

        // Spherical -> rectangular
        double cosB = Math.Cos(beta);
        double sinB = Math.Sin(beta);
        double cosL = Math.Cos(lambda);
        double sinL = Math.Sin(lambda);

        rx = r_m * cosB * cosL;
        ry = r_m * cosB * sinL;
        rz = r_m * sinB;

        // Derivatives
        vx =
            r_dot_m * cosB * cosL
          + r_m * (-sinB * beta_dot * cosL - cosB * sinL * lambda_dot);

        vy =
            r_dot_m * cosB * sinL
          + r_m * (-sinB * beta_dot * sinL + cosB * cosL * lambda_dot);

        vz =
            r_dot_m * sinB
          + r_m * ( cosB * beta_dot );
    }

    private double SolveKeplerE(double M, double e)
    {
        double E = (e < 0.8) ? M : Math.PI;

        for (int k = 0; k < 12; k++)
        {
            double sE = Math.Sin(E);
            double cE = Math.Cos(E);
            double f  = E - e * sE - M;
            double fp = 1.0 - e * cE;

            if (Math.Abs(fp) < 1e-14) break;

            double dE = f / fp;
            E -= dE;

            if (Math.Abs(dE) < 1e-13) break;
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