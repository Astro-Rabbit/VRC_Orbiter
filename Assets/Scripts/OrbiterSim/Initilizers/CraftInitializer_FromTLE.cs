using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using System;

public class CraftInitializer_FromTLE : UdonSharpBehaviour
{
    [Header("Core refs")]
    public EphemerisSystem ephemSystem;
    public BodyCatalog bodies;
    public SimManager simManager;

    [Header("Craft target")]
    public CraftStateModel craft;
    public CraftAttitudeState craftAtt;
    public ConicFitter fitter;
    public ConicState craftConic;
    public CraftNetState netCore;

    [Header("TLE input")]
    [TextArea(2, 4)] public string tleLine1;
    [TextArea(2, 4)] public string tleLine2;

    [Header("Mission time")]
    [Tooltip("Mission-relative time in seconds at which this scenario is initialized.")]
    public double t0Seconds = 0.0;

    [Header("Behavior")]
    public bool resetAttitudeState = true;
    public bool disableDockingOnInit = true;
    public bool setRailsModeOnInit = true;
    public bool autoFitConicAfterSettingState = true;

    [Header("Debug")]
    public bool logInit = false;

    [Header("Read-only preview")]
    public bool previewValid = false;
    public string previewError = "";

    public int tleSatNumber = 0;
    public int tleEpochYear = 0;
    public double tleEpochDay = 0.0;
    public double tleEpochJDPreview = 0.0;

    public double aMetersPreview = 0.0;
    public double ePreview = 0.0;
    public double iDegPreview = 0.0;
    public double raanDegPreview = 0.0;
    public double argpDegPreview = 0.0;
    public double meanAnomalyDegPreview = 0.0;
    public double trueAnomalyDegPreview = 0.0;
    public double meanMotionRevDayPreview = 0.0;
    public double periodSecondsPreview = 0.0;
    public double perigeeRadiusMetersPreview = 0.0;
    public double apogeeRadiusMetersPreview = 0.0;
    public double perigeeAltitudeMetersPreview = 0.0;
    public double apogeeAltitudeMetersPreview = 0.0;

    private const byte EarthId = 1;

    private const double MuEarth = 3.986004418e14;
    private const double EarthRadiusMeters = 6371000.0;

    private const double Pi = 3.14159265358979323846;
    private const double TwoPi = 6.28318530717958647692;
    private const double Deg2Rad = 0.01745329251994329577;
    private const double Rad2Deg = 57.2957795130823208768;
    private const double SecondsPerDay = 86400.0;

    private void OnValidate()
    {
        RefreshPreview();
    }

    public void RefreshPreview()
    {
        int satNumber, epochYear2, epochYearFull;
        double epochDay, epochJD;
        double aMeters, e, iRad, raanRad, argpRad, M0Rad, meanMotionRevDay, periodSeconds;
        string err;

        if (!TryParseTLE(
            out satNumber,
            out epochYear2,
            out epochYearFull,
            out epochDay,
            out epochJD,
            out aMeters,
            out e,
            out iRad,
            out raanRad,
            out argpRad,
            out M0Rad,
            out meanMotionRevDay,
            out periodSeconds,
            out err))
        {
            previewValid = false;
            previewError = err;

            tleSatNumber = 0;
            tleEpochYear = 0;
            tleEpochDay = 0.0;
            tleEpochJDPreview = 0.0;
            aMetersPreview = 0.0;
            ePreview = 0.0;
            iDegPreview = 0.0;
            raanDegPreview = 0.0;
            argpDegPreview = 0.0;
            meanAnomalyDegPreview = 0.0;
            trueAnomalyDegPreview = 0.0;
            meanMotionRevDayPreview = 0.0;
            periodSecondsPreview = 0.0;
            perigeeRadiusMetersPreview = 0.0;
            apogeeRadiusMetersPreview = 0.0;
            perigeeAltitudeMetersPreview = 0.0;
            apogeeAltitudeMetersPreview = 0.0;
            return;
        }

        previewValid = true;
        previewError = "";

        tleSatNumber = satNumber;
        tleEpochYear = epochYearFull;
        tleEpochDay = epochDay;
        tleEpochJDPreview = epochJD;

        aMetersPreview = aMeters;
        ePreview = e;
        iDegPreview = iRad * Rad2Deg;
        raanDegPreview = raanRad * Rad2Deg;
        argpDegPreview = argpRad * Rad2Deg;
        meanAnomalyDegPreview = M0Rad * Rad2Deg;
        trueAnomalyDegPreview = MeanToTrueAnomaly(M0Rad, e) * Rad2Deg;
        meanMotionRevDayPreview = meanMotionRevDay;
        periodSecondsPreview = periodSeconds;

        perigeeRadiusMetersPreview = aMeters * (1.0 - e);
        apogeeRadiusMetersPreview = aMeters * (1.0 + e);
        perigeeAltitudeMetersPreview = perigeeRadiusMetersPreview - EarthRadiusMeters;
        apogeeAltitudeMetersPreview = apogeeRadiusMetersPreview - EarthRadiusMeters;
    }

    public bool TryGetScenarioJd0(out double jd0)
    {
        jd0 = 0.0;

        int satNumber, epochYear2, epochYearFull;
        double epochDay, epochJD;
        double aMeters, e, iRad, raanRad, argpRad, M0Rad, meanMotionRevDay, periodSeconds;
        string err;

        if (!TryParseTLE(
            out satNumber,
            out epochYear2,
            out epochYearFull,
            out epochDay,
            out epochJD,
            out aMeters,
            out e,
            out iRad,
            out raanRad,
            out argpRad,
            out M0Rad,
            out meanMotionRevDay,
            out periodSeconds,
            out err))
            return false;

        jd0 = epochJD;
        return true;
    }

    public bool InitializeNow()
    {
        if (bodies == null || craft == null)
            return false;

        int satNumber, epochYear2, epochYearFull;
        double epochDay, epochJD;
        double aMeters, e, iRad, raanRad, argpRad, M0Rad, meanMotionRevDay, periodSeconds;
        string err;

        if (!TryParseTLE(
            out satNumber,
            out epochYear2,
            out epochYearFull,
            out epochDay,
            out epochJD,
            out aMeters,
            out e,
            out iRad,
            out raanRad,
            out argpRad,
            out M0Rad,
            out meanMotionRevDay,
            out periodSeconds,
            out err))
        {
            if (logInit)
                Debug.Log("[CraftInitializer_FromTLE] Parse failed: " + err);
            return false;
        }

        double T0 = t0Seconds;

        if (ephemSystem != null)
            ephemSystem.Evaluate(T0);

        Vector3 Ieq_E, Jeq_E, Keq_E;
        if (!BuildPrimaryEquatorialBasis(EarthId, out Ieq_E, out Jeq_E, out Keq_E))
            return false;

        double nu = MeanToTrueAnomaly(M0Rad, e);

        double r_pf_x, r_pf_y, v_pf_x, v_pf_y;
        if (!PQWStateFromAENu(MuEarth, aMeters, e, nu, out r_pf_x, out r_pf_y, out v_pf_x, out v_pf_y))
            return false;

        double rxEq, ryEq, rzEq;
        double vxEq, vyEq, vzEq;

        PQWToInertial(
            r_pf_x, r_pf_y, v_pf_x, v_pf_y,
            raanRad, iRad, argpRad,
            out rxEq, out ryEq, out rzEq,
            out vxEq, out vyEq, out vzEq
        );

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

        double px, py, pz, pvx, pvy, pvz;
        bodies.GetBodyState(EarthId, out px, out py, out pz, out pvx, out pvy, out pvz);

        craft.primaryBodyId = EarthId;
        craft.rx = px + rxRel_E;
        craft.ry = py + ryRel_E;
        craft.rz = pz + rzRel_E;

        craft.vx = pvx + vxRel_E;
        craft.vy = pvy + vyRel_E;
        craft.vz = pvz + vzRel_E;

        if (resetAttitudeState && craftAtt != null)
            craftAtt.ResetState();

        if (disableDockingOnInit && simManager != null)
            simManager.dockingAllowed = false;

        if (simManager != null && simManager.dock != null)
            simManager.dock.ResetState();

        if (autoFitConicAfterSettingState && fitter != null)
            fitter.Fit(EarthId, T0);

        if (craftConic != null)
            craftConic.primaryBodyId = EarthId;

        if (setRailsModeOnInit && netCore != null && Networking.IsOwner(netCore.gameObject))
        {
            netCore.SetMode(SimManager.MODE_RAILS, EarthId, true);
            netCore.ForcePublishCore();
        }

        if (logInit)
        {
            Debug.Log(
                "[CraftInitializer_FromTLE] Init " +
                "sat=" + satNumber +
                " T0=" + T0.ToString("F2") +
                " epochJD=" + epochJD.ToString("F8") +
                " a=" + aMeters.ToString("F1") +
                " e=" + e.ToString("F7") +
                " iDeg=" + (iRad * Rad2Deg).ToString("F4") +
                " raanDeg=" + (raanRad * Rad2Deg).ToString("F4") +
                " argpDeg=" + (argpRad * Rad2Deg).ToString("F4") +
                " MDeg=" + (M0Rad * Rad2Deg).ToString("F4")
            );
        }

        return true;
    }

    private bool TryParseTLE(
        out int satNumber,
        out int epochYear2,
        out int epochYearFull,
        out double epochDay,
        out double epochJD,
        out double aMeters,
        out double e,
        out double iRad,
        out double raanRad,
        out double argpRad,
        out double M0Rad,
        out double meanMotionRevDay,
        out double periodSeconds,
        out string err)
    {
        satNumber = 0;
        epochYear2 = 0;
        epochYearFull = 0;
        epochDay = 0.0;
        epochJD = 0.0;
        aMeters = 0.0;
        e = 0.0;
        iRad = 0.0;
        raanRad = 0.0;
        argpRad = 0.0;
        M0Rad = 0.0;
        meanMotionRevDay = 0.0;
        periodSeconds = 0.0;
        err = "";

        string l1 = SafeTrim(tleLine1);
        string l2 = SafeTrim(tleLine2);

        if (l1 == "" || l2 == "")
        {
            err = "Missing TLE line text.";
            return false;
        }
        if (!l1.StartsWith("1 "))
        {
            err = "Line 1 must begin with '1 '.";
            return false;
        }
        if (!l2.StartsWith("2 "))
        {
            err = "Line 2 must begin with '2 '.";
            return false;
        }
        if (l1.Length < 32)
        {
            err = "Line 1 too short.";
            return false;
        }
        if (l2.Length < 63)
        {
            err = "Line 2 too short.";
            return false;
        }

        bool ok = true;

        satNumber = ParseIntLoose(Sub(l1, 2, 5), ref ok);
        if (!ok)
        {
            err = "Failed parsing satellite number from line 1.";
            return false;
        }

        int satNumber2 = ParseIntLoose(Sub(l2, 2, 5), ref ok);
        if (!ok)
        {
            err = "Failed parsing satellite number from line 2.";
            return false;
        }

        if (satNumber != satNumber2)
        {
            err = "TLE satellite numbers do not match.";
            return false;
        }

        epochYear2 = ParseIntLoose(Sub(l1, 18, 2), ref ok);
        if (!ok)
        {
            err = "Failed parsing TLE epoch year.";
            return false;
        }

        epochDay = ParseDoubleLoose(Sub(l1, 20, 12), ref ok);
        if (!ok)
        {
            err = "Failed parsing TLE epoch day.";
            return false;
        }

        double incDeg = ParseDoubleLoose(Sub(l2, 8, 8), ref ok);
        if (!ok)
        {
            err = "Failed parsing inclination.";
            return false;
        }

        double raanDeg = ParseDoubleLoose(Sub(l2, 17, 8), ref ok);
        if (!ok)
        {
            err = "Failed parsing RAAN.";
            return false;
        }

        string eccText = "0." + SafeTrim(Sub(l2, 26, 7));
        e = ParseDoubleLoose(eccText, ref ok);
        if (!ok)
        {
            err = "Failed parsing eccentricity.";
            return false;
        }

        double argpDeg = ParseDoubleLoose(Sub(l2, 34, 8), ref ok);
        if (!ok)
        {
            err = "Failed parsing argument of perigee.";
            return false;
        }

        double meanAnDeg = ParseDoubleLoose(Sub(l2, 43, 8), ref ok);
        if (!ok)
        {
            err = "Failed parsing mean anomaly.";
            return false;
        }

        meanMotionRevDay = ParseDoubleLoose(Sub(l2, 52, 11), ref ok);
        if (!ok)
        {
            err = "Failed parsing mean motion.";
            return false;
        }

        if (meanMotionRevDay <= 0.0)
        {
            err = "Mean motion must be > 0.";
            return false;
        }

        epochYearFull = ExpandTLEYear(epochYear2);
        epochJD = TLEEpochToJulianDate(epochYear2, epochDay);

        double nRadSec = meanMotionRevDay * TwoPi / SecondsPerDay;
        aMeters = Math.Pow(MuEarth / (nRadSec * nRadSec), 1.0 / 3.0);
        periodSeconds = TwoPi / nRadSec;

        iRad = incDeg * Deg2Rad;
        raanRad = raanDeg * Deg2Rad;
        argpRad = argpDeg * Deg2Rad;
        M0Rad = Wrap2Pi(meanAnDeg * Deg2Rad);

        return true;
    }

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
        v_y = s * (eMag + cosNu);

        return true;
    }

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

        double m00 = cO * cw - sO * sw * ci;
        double m01 = -cO * sw - sO * cw * ci;

        double m10 = sO * cw + cO * sw * ci;
        double m11 = -sO * sw + cO * cw * ci;

        double m20 = sw * si;
        double m21 = cw * si;

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

    private static string SafeTrim(string s)
    {
        if (s == null) return "";
        return s.Trim();
    }

    private static string Sub(string s, int start, int len)
    {
        if (s == null) return "";
        if (start < 0) start = 0;
        if (start >= s.Length) return "";
        if (start + len > s.Length) len = s.Length - start;
        return s.Substring(start, len);
    }

    private static int ParseIntLoose(string s, ref bool ok)
    {
        s = SafeTrim(s);
        if (s == "")
        {
            ok = false;
            return 0;
        }

        int sign = 1;
        int i = 0;
        int value = 0;
        bool foundDigit = false;

        if (s.Length > 0)
        {
            char c0 = s[0];
            if (c0 == '-')
            {
                sign = -1;
                i = 1;
            }
            else if (c0 == '+')
            {
                i = 1;
            }
        }

        for (; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= '0' && c <= '9')
            {
                foundDigit = true;
                value = value * 10 + (c - '0');
            }
            else if (c == ' ')
            {
            }
            else
            {
                ok = false;
                return 0;
            }
        }

        if (!foundDigit)
        {
            ok = false;
            return 0;
        }

        ok = true;
        return value * sign;
    }

    private static double ParseDoubleLoose(string s, ref bool ok)
    {
        s = SafeTrim(s);
        if (s == "")
        {
            ok = false;
            return 0.0;
        }

        double sign = 1.0;
        int i = 0;

        if (s.Length > 0)
        {
            char c0 = s[0];
            if (c0 == '-')
            {
                sign = -1.0;
                i = 1;
            }
            else if (c0 == '+')
            {
                i = 1;
            }
        }

        double intPart = 0.0;
        double fracPart = 0.0;
        double fracScale = 1.0;
        bool seenDot = false;
        bool foundDigit = false;

        for (; i < s.Length; i++)
        {
            char c = s[i];

            if (c >= '0' && c <= '9')
            {
                foundDigit = true;
                int d = c - '0';

                if (!seenDot)
                {
                    intPart = intPart * 10.0 + d;
                }
                else
                {
                    fracScale *= 0.1;
                    fracPart += d * fracScale;
                }
            }
            else if (c == '.')
            {
                if (seenDot)
                {
                    ok = false;
                    return 0.0;
                }
                seenDot = true;
            }
            else if (c == ' ')
            {
            }
            else
            {
                ok = false;
                return 0.0;
            }
        }

        if (!foundDigit)
        {
            ok = false;
            return 0.0;
        }

        ok = true;
        return sign * (intPart + fracPart);
    }

    private static int ExpandTLEYear(int yy)
    {
        if (yy >= 57) return 1900 + yy;
        return 2000 + yy;
    }

    private static double TLEEpochToJulianDate(int epochYear2, double epochDay)
    {
        int year = ExpandTLEYear(epochYear2);

        int wholeDay = (int)Math.Floor(epochDay);
        double fracDay = epochDay - wholeDay;

        if (wholeDay < 1) wholeDay = 1;

        int month = 1;
        int dayOfMonth = 1;

        DayOfYearToMonthDay(year, wholeDay, out month, out dayOfMonth);

        double fracHours = fracDay * 24.0;
        int hour = (int)Math.Floor(fracHours);

        double fracMinutes = (fracHours - hour) * 60.0;
        int minute = (int)Math.Floor(fracMinutes);

        double fracSeconds = (fracMinutes - minute) * 60.0;
        double second = fracSeconds;

        return GregorianToJulianDate(year, month, dayOfMonth, hour, minute, second);
    }

    private static void DayOfYearToMonthDay(int year, int dayOfYear, out int month, out int dayOfMonth)
    {
        int[] dim = new int[12];
        dim[0] = 31;
        dim[1] = IsLeapYear(year) ? 29 : 28;
        dim[2] = 31;
        dim[3] = 30;
        dim[4] = 31;
        dim[5] = 30;
        dim[6] = 31;
        dim[7] = 31;
        dim[8] = 30;
        dim[9] = 31;
        dim[10] = 30;
        dim[11] = 31;

        int d = dayOfYear;
        month = 1;

        for (int m = 0; m < 12; m++)
        {
            if (d <= dim[m])
            {
                month = m + 1;
                dayOfMonth = d;
                return;
            }

            d -= dim[m];
        }

        month = 12;
        dayOfMonth = 31;
    }

    private static bool IsLeapYear(int year)
    {
        if ((year % 400) == 0) return true;
        if ((year % 100) == 0) return false;
        return (year % 4) == 0;
    }

    private static double GregorianToJulianDate(int year, int month, int day, int hour, int minute, double second)
    {
        int Y = year;
        int M = month;

        if (M <= 2)
        {
            Y -= 1;
            M += 12;
        }

        int A = Y / 100;
        int B = 2 - A + (A / 4);

        double D = day +
                   (hour / 24.0) +
                   (minute / 1440.0) +
                   (second / 86400.0);

        double jd =
            Math.Floor(365.25 * (Y + 4716)) +
            Math.Floor(30.6001 * (M + 1)) +
            D + B - 1524.5;

        return jd;
    }

    private static double Wrap2Pi(double a)
    {
        a = a % TwoPi;
        if (a < 0.0) a += TwoPi;
        return a;
    }

    private static double WrapPi(double a)
    {
        a = a % TwoPi;
        if (a <= -Pi) a += TwoPi;
        else if (a > Pi) a -= TwoPi;
        return a;
    }
}