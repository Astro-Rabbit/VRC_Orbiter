using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using System;

/// <summary>
/// CraftInitializer_FromPrimaryOrbitElements
///
/// Generalized rails scenario initializer from classical orbital elements
/// defined in the PRIMARY BODY EQUATORIAL frame.
///
/// Intended use:
/// - scenario / restart system initializes craft at mission time t0
/// - craft starts in rails mode
/// - docking is forced OFF at start unless explicitly re-enabled later
///
/// Frame conventions:
/// - Solver inertial frame = heliocentric / SSB ecliptic inertial
/// - Input elements are defined relative to the selected primary body's EQUATORIAL frame
/// - Resulting craft state is written in solver inertial heliocentric coordinates
///
/// Notes:
/// - This is a rails initializer, not an integrated-state initializer
/// - Attitude can optionally be authored in the primary equatorial frame
/// </summary>
public class CraftInitializer_FromPrimaryOrbitElements : UdonSharpBehaviour
{
    [Header("Core refs")]
    public SimClock clock;
    public EphemerisSystem ephemSystem;
    public BodyCatalog bodies;
    public SimManager simManager;

    [Header("Craft target")]
    public CraftStateModel craft;
    public CraftAttitudeState craftAtt;
    public ConicFitter fitter;
    public ConicState craftConic;
    public CraftNetState netCore;

    [Header("Scenario time")]
    [Tooltip("Mission time in seconds at which this scenario is defined. In the new restart system this is usually 0.")]
    public double t0Seconds = 0.0;

    [Header("Primary body")]
    [Tooltip("Body the orbit is defined around. Example: 1=Earth, 2=Moon.")]
    public byte primaryId = 1;

    [Header("Primary-equatorial orbital elements")]
    public double aMeters = 7000e3;
    public double e = 0.0;
    public double iRad = 0.0;
    public double raanRad = 0.0;
    public double argpRad = 0.0;

    [Header("Anomaly input")]
    public bool useMeanAnomaly = true;
    public double M0Rad = 0.0;
    public double nu0Rad = 0.0;

    [Header("Initial attitude")]
    [Tooltip("If true, apply an authored initial attitude instead of resetting to identity.")]
    public bool useAuthoredInitialAttitude = false;

    [Tooltip("Authored body->primary-equatorial attitude as Unity Euler angles in degrees.")]
    public Vector3 initialAttitudeEulerDeg = Vector3.zero;

    [Tooltip("If true, zero the body rates on init.")]
    public bool zeroAngularRatesOnInit = true;

    [Header("Behavior")]
    public bool resetAttitudeState = true;
    public bool disableDockingOnInit = true;
    public bool setRailsModeOnInit = true;
    public bool autoFitConicAfterSettingState = true;

    [Header("Debug")]
    public bool logInit = false;

    public bool InitializeNow()
    {
        if (bodies == null || craft == null)
            return false;

        double T0 = t0Seconds;

        // In restart flow clock has already been reset, but we still explicitly
        // evaluate ephemeris at the authored scenario time for consistency.
        if (ephemSystem != null)
            ephemSystem.Evaluate(T0);

        double mu = bodies.GetMu(primaryId);
        if (mu <= 0.0)
            return false;

        Vector3 Ieq_E, Jeq_E, Keq_E;
        if (!BuildPrimaryEquatorialBasis(primaryId, out Ieq_E, out Jeq_E, out Keq_E))
            return false;

        double nu = useMeanAnomaly ? MeanToTrueAnomaly(M0Rad, e) : Wrap2Pi(nu0Rad);

        double r_pf_x, r_pf_y, v_pf_x, v_pf_y;
        if (!PQWStateFromAENu(mu, aMeters, e, nu, out r_pf_x, out r_pf_y, out v_pf_x, out v_pf_y))
            return false;

        // PQW -> primary equatorial inertial coordinates
        double rxEq, ryEq, rzEq;
        double vxEq, vyEq, vzEq;

        PQWToInertial(
            r_pf_x, r_pf_y, v_pf_x, v_pf_y,
            raanRad, iRad, argpRad,
            out rxEq, out ryEq, out rzEq,
            out vxEq, out vyEq, out vzEq
        );

        // Primary equatorial inertial -> solver inertial
        double rxRel_E =
            rxEq * Ieq_E.x +
            ryEq * Jeq_E.x +
            rzEq * Keq_E.x;

        double ryRel_E =
            rxEq * Ieq_E.y +
            ryEq * Jeq_E.y +
            rzEq * Keq_E.y;

        double rzRel_E =
            rxEq * Ieq_E.z +
            ryEq * Jeq_E.z +
            rzEq * Keq_E.z;

        double vxRel_E =
            vxEq * Ieq_E.x +
            vyEq * Jeq_E.x +
            vzEq * Keq_E.x;

        double vyRel_E =
            vxEq * Ieq_E.y +
            vyEq * Jeq_E.y +
            vzEq * Keq_E.y;

        double vzRel_E =
            vxEq * Ieq_E.z +
            vyEq * Jeq_E.z +
            vzEq * Keq_E.z;

        // Compose with primary heliocentric state
        double px, py, pz, pvx, pvy, pvz;
        bodies.GetBodyState(primaryId, out px, out py, out pz, out pvx, out pvy, out pvz);

        craft.primaryBodyId = primaryId;
        craft.rx = px + rxRel_E;
        craft.ry = py + ryRel_E;
        craft.rz = pz + rzRel_E;

        craft.vx = pvx + vxRel_E;
        craft.vy = pvy + vyRel_E;
        craft.vz = pvz + vzRel_E;

        ApplyInitialAttitude(Ieq_E, Jeq_E, Keq_E);

        if (disableDockingOnInit && simManager != null)
            simManager.dockingAllowed = false;

        if (simManager != null && simManager.dock != null)
            simManager.dock.ResetState();

        if (autoFitConicAfterSettingState && fitter != null)
            fitter.Fit(primaryId, T0);

        if (craftConic != null)
            craftConic.primaryBodyId = primaryId;

        if (setRailsModeOnInit && netCore != null && Networking.IsOwner(netCore.gameObject))
        {
            netCore.SetMode(SimManager.MODE_RAILS, primaryId, true);
            netCore.ForcePublishCore();
        }

        if (logInit)
        {
            Debug.Log(
                "[CraftInitializer_FromPrimaryOrbitElements] Init " +
                "T0=" + T0.ToString("F2") +
                " primary=" + primaryId +
                " a=" + aMeters.ToString("F1") +
                " e=" + e.ToString("F6") +
                " iDeg=" + (iRad * 57.29577951308232).ToString("F3") +
                " raanDeg=" + (raanRad * 57.29577951308232).ToString("F3") +
                " argpDeg=" + (argpRad * 57.29577951308232).ToString("F3") +
                " nuDeg=" + (nu * 57.29577951308232).ToString("F3") +
                " useAuthoredInitialAttitude=" + useAuthoredInitialAttitude +
                " attEulerDeg=(" + initialAttitudeEulerDeg.x.ToString("F2") + ", " +
                                   initialAttitudeEulerDeg.y.ToString("F2") + ", " +
                                   initialAttitudeEulerDeg.z.ToString("F2") + ")"
            );
        }

        return true;
    }

    private void ApplyInitialAttitude(Vector3 Ieq_E, Vector3 Jeq_E, Vector3 Keq_E)
    {
        if (craftAtt == null)
            return;

        if (!resetAttitudeState && !useAuthoredInitialAttitude)
            return;

        if (useAuthoredInitialAttitude)
        {
            // Body -> primary-equatorial authored by inspector Euler angles
            Quaternion qBEq = Quaternion.Euler(initialAttitudeEulerDeg);

            // Primary-equatorial basis expressed in solver inertial frame.
            // Columns are the eq basis axes in E frame.
            Quaternion qEqToE = QuaternionFromBasis(Ieq_E, Jeq_E, Keq_E);

            // Body -> E
            craftAtt.qBE = Normalize(qEqToE * qBEq);

            if (zeroAngularRatesOnInit)
            {
                craftAtt.wx = 0.0;
                craftAtt.wy = 0.0;
                craftAtt.wz = 0.0;
            }
        }
        else
        {
            craftAtt.ResetState();
        }
    }

    /// <summary>
    /// Builds the primary body's equatorial basis in solver inertial coordinates.
    /// +K = body north pole
    /// +I = projected solver +X into the equatorial plane
    /// +J = K x I
    /// </summary>
    private bool BuildPrimaryEquatorialBasis(byte bodyId, out Vector3 Ieq_E, out Vector3 Jeq_E, out Vector3 Keq_E)
    {
        Ieq_E = Vector3.right;
        Jeq_E = Vector3.up;
        Keq_E = Vector3.forward;

        Quaternion qBodyToInertial = bodies.GetBodyFixedToInertial(bodyId);

        Vector3 k = qBodyToInertial * Vector3.forward;
        if (k.sqrMagnitude < 1e-12f)
            return false;
        k.Normalize();

        Vector3 refI = Vector3.right;
        float d = Mathf.Abs(Vector3.Dot(refI, k));
        if (d > 0.9f)
            refI = Vector3.up;

        Vector3 i = refI - Vector3.Dot(refI, k) * k;
        if (i.sqrMagnitude < 1e-12f)
            return false;
        i.Normalize();

        Vector3 j = Vector3.Cross(k, i);
        if (j.sqrMagnitude < 1e-12f)
            return false;
        j.Normalize();

        Ieq_E = i;
        Jeq_E = j;
        Keq_E = k;
        return true;
    }

    private static Quaternion QuaternionFromBasis(Vector3 xAxis, Vector3 yAxis, Vector3 zAxis)
    {
        xAxis.Normalize();
        yAxis.Normalize();
        zAxis.Normalize();

        Matrix4x4 m = new Matrix4x4();
        m.SetColumn(0, new Vector4(xAxis.x, yAxis.x, zAxis.x, 0f));
        m.SetColumn(1, new Vector4(xAxis.y, yAxis.y, zAxis.y, 0f));
        m.SetColumn(2, new Vector4(xAxis.z, yAxis.z, zAxis.z, 0f));
        m.SetColumn(3, new Vector4(0f, 0f, 0f, 1f));

        return Normalize(m.rotation);
    }

    private static Quaternion Normalize(Quaternion q)
    {
        float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        if (m < 1e-8f) return Quaternion.identity;

        float inv = 1.0f / m;
        q.x *= inv;
        q.y *= inv;
        q.z *= inv;
        q.w *= inv;
        return q;
    }

    /// <summary>
    /// Planar PQW state from (a,e,nu).
    /// Supports ellipse and hyperbola. Parabolic excluded.
    /// </summary>
    private bool PQWStateFromAENu(
        double mu, double a, double eMag, double nu,
        out double r_x, out double r_y,
        out double v_x, out double v_y)
    {
        r_x = 0.0;
        r_y = 0.0;
        v_x = 0.0;
        v_y = 0.0;

        if (Math.Abs(1.0 - eMag) < 1e-10)
            return false;

        double p = a * (1.0 - eMag * eMag);
        if (p <= 0.0)
            return false;

        double cosNu = Math.Cos(nu);
        double sinNu = Math.Sin(nu);

        double r = p / (1.0 + eMag * cosNu);

        r_x = r * cosNu;
        r_y = r * sinNu;

        double s = Math.Sqrt(mu / p);
        v_x = -s * sinNu;
        v_y =  s * (eMag + cosNu);

        return true;
    }

    /// <summary>
    /// Standard PQW -> inertial rotation using Ω, i, ω.
    /// Outputs are in the primary-equatorial inertial basis coordinates.
    /// </summary>
    private void PQWToInertial(
        double r_pf_x, double r_pf_y,
        double v_pf_x, double v_pf_y,
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

        double m00 =  cO * cw - sO * sw * ci;
        double m01 = -cO * sw - sO * cw * ci;

        double m10 =  sO * cw + cO * sw * ci;
        double m11 = -sO * sw + cO * cw * ci;

        double m20 =  sw * si;
        double m21 =  cw * si;

        rx = m00 * r_pf_x + m01 * r_pf_y;
        ry = m10 * r_pf_x + m11 * r_pf_y;
        rz = m20 * r_pf_x + m21 * r_pf_y;

        vx = m00 * v_pf_x + m01 * v_pf_y;
        vy = m10 * v_pf_x + m11 * v_pf_y;
        vz = m20 * v_pf_x + m21 * v_pf_y;
    }

    private static double MeanToTrueAnomaly(double M, double eMag)
    {
        M = WrapPi(M);

        if (eMag < 1.0)
        {
            double E = SolveKeplerE(M, eMag);
            double cosE = Math.Cos(E);
            double sinE = Math.Sin(E);

            double denom = 1.0 - eMag * cosE;
            if (Math.Abs(denom) < 1e-14)
                return 0.0;

            double cosNu = (cosE - eMag) / denom;
            double sinNu = (Math.Sqrt(1.0 - eMag * eMag) * sinE) / denom;

            return Wrap2Pi(Math.Atan2(sinNu, cosNu));
        }
        else
        {
            double H = SolveKeplerH(M, eMag);
            double coshH = Math.Cosh(H);
            double sinhH = Math.Sinh(H);

            double cosNu = (eMag - coshH) / (eMag * coshH - 1.0);
            double sinNu = (Math.Sqrt(eMag * eMag - 1.0) * sinhH) / (eMag * coshH - 1.0);

            return Wrap2Pi(Math.Atan2(sinNu, cosNu));
        }
    }

    private static double SolveKeplerE(double M, double eMag)
    {
        double E = M;

        for (int k = 0; k < 16; k++)
        {
            double f = E - eMag * Math.Sin(E) - M;
            double fp = 1.0 - eMag * Math.Cos(E);

            if (Math.Abs(fp) < 1e-14)
                break;

            double d = f / fp;
            E -= d;

            if (Math.Abs(d) < 1e-12)
                break;
        }

        return E;
    }

    private static double SolveKeplerH(double M, double eMag)
    {
        double H = Math.Log(2.0 * Math.Abs(M) / eMag + 1.8);
        if (M < 0.0)
            H = -H;

        for (int k = 0; k < 20; k++)
        {
            double sinhH = Math.Sinh(H);
            double coshH = Math.Cosh(H);

            double f = eMag * sinhH - H - M;
            double fp = eMag * coshH - 1.0;

            if (Math.Abs(fp) < 1e-14)
                break;

            double d = f / fp;
            H -= d;

            if (Math.Abs(d) < 1e-12)
                break;
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