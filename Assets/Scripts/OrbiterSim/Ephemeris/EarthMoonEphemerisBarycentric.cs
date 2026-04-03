using UdonSharp;
using UnityEngine;
using System;

public class EarthMoonEphemerisBarycentric : UdonSharpBehaviour
{
    [Header("Units / Constants")]
    public double AU_m = 149597870700.0;
    public double velDiffSeconds = 60.0; // kept for inspector compatibility; not used

    [Header("Earth/Moon scales")]
    public double moonMeanDistanceM = 385000560.0;

    // ------------------------------------------------------------------------
    // Public API expected by EphemerisSystem
    // ------------------------------------------------------------------------
    public void Evaluate(double jd,
        out double sun_rx, out double sun_ry, out double sun_rz,
        out double sun_vx, out double sun_vy, out double sun_vz,
        out double earth_rx, out double earth_ry, out double earth_rz,
        out double earth_vx, out double earth_vy, out double earth_vz,
        out double moon_rx, out double moon_ry, out double moon_rz,
        out double moon_vx, out double moon_vy, out double moon_vz)
    {
        sun_rx = sun_ry = sun_rz = 0.0;
        sun_vx = sun_vy = sun_vz = 0.0;

        ComputeEarthState(jd,
            out earth_rx, out earth_ry, out earth_rz,
            out earth_vx, out earth_vy, out earth_vz);

        double moon_geo_rx, moon_geo_ry, moon_geo_rz;
        double moon_geo_vx, moon_geo_vy, moon_geo_vz;

        ComputeMoonGeocentricState_LargerSeries(jd,
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
    // Direct Kepler evaluation with direct velocity.
    // ------------------------------------------------------------------------
    private void ComputeEarthState(double jd,
        out double rx, out double ry, out double rz,
        out double vx, out double vy, out double vz)
    {
        double d = jd - 2451545.0;

        double a_AU = 1.00000011;
        double e = 0.01671022 - 0.00000000126 * d;

        double varpi_deg = 102.937348 + 0.0000470935 * d;
        double M_deg = 357.5291092 + 0.98560028 * d;

        double varpi = DegToRad(varpi_deg);
        double M = DegToRad(WrapDeg(M_deg));

        double Mdot = DegToRad(0.98560028) / 86400.0;

        double E = SolveKeplerE(M, e);
        double cosE = Math.Cos(E);
        double sinE = Math.Sin(E);

        double oneMinusECosE = 1.0 - e * cosE;
        double sqrt1me2 = Math.Sqrt(Math.Max(1e-15, 1.0 - e * e));

        double x_orb = a_AU * (cosE - e);
        double y_orb = a_AU * (sqrt1me2 * sinE);

        double Edot = Mdot / Math.Max(1e-15, oneMinusECosE);
        double vx_orb = -a_AU * sinE * Edot;
        double vy_orb =  a_AU * sqrt1me2 * cosE * Edot;

        double cosw = Math.Cos(varpi);
        double sinw = Math.Sin(varpi);

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
    // Larger truncated lunar series
    //
    // Output:
    //   geocentric Moon state in ecliptic inertial frame, meters and m/s
    //
    // Fundamental arguments:
    //   Lp = mean longitude of Moon
    //   D  = mean elongation of Moon from Sun
    //   M  = Sun mean anomaly
    //   Mp = Moon mean anomaly
    //   F  = Moon argument of latitude
    //
    // This is still a practical truncated theory, not a full DE ephemeris,
    // but materially better than the tiny series.
    // ------------------------------------------------------------------------
    private void ComputeMoonGeocentricState_LargerSeries(double jd,
        out double rx, out double ry, out double rz,
        out double vx, out double vy, out double vz)
    {
        double d = jd - 2451545.0;

        // Fundamental arguments [deg]
        double Lp_deg = 218.3164477 + 13.17639648 * d;
        double D_deg  = 297.8501921 + 12.19074912 * d;
        double M_deg  = 357.5291092 + 0.98560028 * d;
        double Mp_deg = 134.9633964 + 13.06499295 * d;
        double F_deg  = 93.2720950  + 13.22935024 * d;

        double Lp = DegToRad(WrapDeg(Lp_deg));
        double D  = DegToRad(WrapDeg(D_deg));
        double M  = DegToRad(WrapDeg(M_deg));
        double Mp = DegToRad(WrapDeg(Mp_deg));
        double F  = DegToRad(WrapDeg(F_deg));

        // Rates [rad/s]
        double Lp_dot = DegToRad(13.17639648) / 86400.0;
        double D_dot  = DegToRad(12.19074912) / 86400.0;
        double M_dot  = DegToRad(0.98560028)  / 86400.0;
        double Mp_dot = DegToRad(13.06499295) / 86400.0;
        double F_dot  = DegToRad(13.22935024) / 86400.0;

        // ---------------------------
        // Longitude series [deg]
        // lambda = Lp + dLambda
        // ---------------------------
        double dLambdaDeg = 0.0;
        double dLambdaDotDeg = 0.0;

        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  6.289, 0, 0, 1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  1.274, 2, 0,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.658, 2, 0, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.214, 0, 0, 2, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg, -0.186, 0, 1, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg, -0.114, 0, 0, 0, 2, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.059, 2, 0,-2, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.057, 2,-1,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.053, 2, 0, 1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.046, 2,-1, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.041, 0, 1,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg, -0.035, 1, 0, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg, -0.031, 0, 1, 1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg, -0.015, 2, 0, 0,-2, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.011, 4, 0,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);

        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg, -0.009, 4, 0,-2, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.009, 2, 1,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.008, 2, 1, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg, -0.008, 1, 0,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg, -0.007, 1, 1, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg, -0.007, 0, 1,-2, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.007, 2, 0,-1, 2, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.006, 2, 0, 0, 2, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.005, 2,-1, 1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.005, 2,-1,-2, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg, -0.004, 0, 0, 1, 2, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.004, 4, 0, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.004, 4,-1,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.004, 1, 0, 1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg, -0.003, 4,-1,-2, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.003, 2, 0, 2, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.003, 2, 0,-3, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.003, 2, 1,-2, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.003, 0, 1, 2, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref dLambdaDeg, ref dLambdaDotDeg,  0.003, 0, 2, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);

        // ---------------------------
        // Latitude series [deg]
        // beta = dBeta
        // ---------------------------
        double betaDeg = 0.0;
        double betaDotDeg = 0.0;

        AddSinTerm(ref betaDeg, ref betaDotDeg,  5.128, 0, 0, 0, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.280, 0, 0, 1, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.277, 0, 0, 1,-1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.173, 2, 0, 0,-1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.055, 2, 0,-1, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.046, 2, 0,-1,-1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.033, 2, 0, 0, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.017, 0, 0, 2, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);

        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.009, 2, 0, 1,-1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.009, 0, 0, 2,-1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.008, 2,-1, 0,-1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.008, 0, 0, 0, 3, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.007, 2,-1, 1, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.007, 2,-1, 1,-1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.007, 2,-1, 0, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.006, 2,-1,-1, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.006, 2,-1, 0,-3, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.005, 0, 1,-1,-1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.005, 0, 1, 0, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.004, 0, 1,-1, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.004, 0, 1, 0,-1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.004, 0, 0, 3, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.003, 4, 0, 0,-1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.003, 4, 0,-1,-1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.003, 0, 0, 1,-3, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.003, 4, 0,-1, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddSinTerm(ref betaDeg, ref betaDotDeg,  0.003, 1, 0, 0, 1, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);

        // ---------------------------
        // Distance series [km]
        // r = r0 + dR
        // ---------------------------
        double r_km = 385000.56;
        double rDot_km = 0.0;

        AddCosTerm(ref r_km, ref rDot_km, -20905.0, 0, 0, 1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km, -3699.0,  2, 0,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km, -2956.0,  2, 0, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,  -570.0,  0, 0, 2, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,   246.0,  2, 0,-2, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,  -205.0,  2,-1, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,  -171.0,  2, 0, 1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,  -152.0,  2,-1,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);

        AddCosTerm(ref r_km, ref rDot_km,  -129.0,  1, 0,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,   108.0,  1, 0, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,   104.0,  0, 0, 0, 2, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,    79.0,  0, 1, 1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,    48.0,  0, 1,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,   -34.0,  4, 0,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,   -26.0,  4, 0,-2, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,    23.0,  2, 1,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,    19.0,  2, 1, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,    17.0,  0, 0, 1, 2, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,   -14.0,  0, 0, 2, 2, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,    13.0,  4, 0, 0, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,    12.0,  4,-1,-1, 0, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,   -10.0,  2, 0, 1, 2, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);
        AddCosTerm(ref r_km, ref rDot_km,   -10.0,  2, 0,-1, 2, D, M, Mp, F, D_dot, M_dot, Mp_dot, F_dot);

        double lambda = Lp + DegToRad(dLambdaDeg);
        double beta   = DegToRad(betaDeg);
        double lambda_dot = Lp_dot + DegToRad(dLambdaDotDeg);
        double beta_dot   = DegToRad(betaDotDeg);

        double r_m = r_km * 1000.0;
        double r_dot_m = rDot_km * 1000.0;

        double cosB = Math.Cos(beta);
        double sinB = Math.Sin(beta);
        double cosL = Math.Cos(lambda);
        double sinL = Math.Sin(lambda);

        rx = r_m * cosB * cosL;
        ry = r_m * cosB * sinL;
        rz = r_m * sinB;

        vx =
            r_dot_m * cosB * cosL
          + r_m * (-sinB * beta_dot * cosL - cosB * sinL * lambda_dot);

        vy =
            r_dot_m * cosB * sinL
          + r_m * (-sinB * beta_dot * sinL + cosB * cosL * lambda_dot);

        vz =
            r_dot_m * sinB
          + r_m * (cosB * beta_dot);
    }

    // ------------------------------------------------------------------------
    // Helpers for harmonic terms
    // phase = aD*D + aM*M + aMp*Mp + aF*F
    // ------------------------------------------------------------------------
    private void AddSinTerm(
        ref double sum, ref double sumDot,
        double amp,
        int aD, int aM, int aMp, int aF,
        double D, double M, double Mp, double F,
        double D_dot, double M_dot, double Mp_dot, double F_dot)
    {
        double phase =
            aD * D +
            aM * M +
            aMp * Mp +
            aF * F;

        double phaseDot =
            aD * D_dot +
            aM * M_dot +
            aMp * Mp_dot +
            aF * F_dot;

        sum += amp * Math.Sin(phase);
        sumDot += amp * Math.Cos(phase) * phaseDot;
    }

    private void AddCosTerm(
        ref double sum, ref double sumDot,
        double amp,
        int aD, int aM, int aMp, int aF,
        double D, double M, double Mp, double F,
        double D_dot, double M_dot, double Mp_dot, double F_dot)
    {
        double phase =
            aD * D +
            aM * M +
            aMp * Mp +
            aF * F;

        double phaseDot =
            aD * D_dot +
            aM * M_dot +
            aMp * Mp_dot +
            aF * F_dot;

        sum += amp * Math.Cos(phase);
        sumDot += -amp * Math.Sin(phase) * phaseDot;
    }

    // ------------------------------------------------------------------------
    // Basic math helpers
    // ------------------------------------------------------------------------
    private double SolveKeplerE(double M, double e)
    {
        double E = (e < 0.8) ? M : Math.PI;

        for (int k = 0; k < 12; k++)
        {
            double sE = Math.Sin(E);
            double cE = Math.Cos(E);
            double f = E - e * sE - M;
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