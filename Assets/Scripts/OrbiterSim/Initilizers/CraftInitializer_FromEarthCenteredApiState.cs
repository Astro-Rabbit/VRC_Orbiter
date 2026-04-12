using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using System;

/// <summary>
/// CraftInitializer_FromEarthCenteredApiState
///
/// Quick rails/conic initializer for API-provided Earth-centered inertial
/// state vectors, such as Artemis tracking feeds.
///
/// Input units:
/// - position: km
/// - velocity: km/s
///
/// Time input:
/// - UTC ISO timestamp string, e.g. 2026-04-06T21:56:05Z
///
/// Intended frame handling:
/// - API input is assumed Earth-centered inertial, usually equatorial/J2000-like
/// - Sim solver frame is heliocentric ecliptic inertial
/// - If rotateEquatorialToEcliptic=true, input is rotated about +X by -obliquity
///   before being translated into the heliocentric frame
/// </summary>
public class CraftInitializer_FromEarthCenteredApiState : UdonSharpBehaviour
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

    [Header("API timestamp (UTC ISO)")]
    [Tooltip("Example: 2026-04-06T21:56:05Z")]
    public string isoTimestamp = "2026-04-06T21:56:05Z";

    [Header("Mission time")]
    [Tooltip("Mission-relative time in seconds at which this scenario is initialized.")]
    public double t0Seconds = 0.0;

    [Header("API Earth-centered state (km / km/s)")]
    public double posX_km = -130272.1055856897;
    public double posY_km = -389750.5661777776;
    public double posZ_km = -36285.76042866125;

    public double velX_kmps = -0.4014804888406006;
    public double velY_kmps = -0.1438821650011895;
    public double velZ_kmps = -0.05536216850314146;

    [Header("Optional Moon debug vector from same API frame (km)")]
    public bool useMoonDebugVector = true;
    public double moonPosX_km = -132902.6753893179;
    public double moonPosY_km = -380692.9130518995;
    public double moonPosZ_km = -36359.78338376147;

    [Header("Frame conversion")]
    [Tooltip("If true, rotate API Earth-centered vector from equatorial to ecliptic before adding Earth heliocentric state.")]
    public bool rotateEquatorialToEcliptic = true;

    [Tooltip("Obliquity used for equatorial->ecliptic conversion, in degrees.")]
    public double obliquityDeg = 23.4392911;

    [Header("Behavior")]
    public bool resetAttitudeState = true;
    public bool disableDockingOnInit = true;
    public bool setRailsModeOnInit = true;
    public bool autoFitConicAfterSettingState = true;

    [Header("Debug")]
    public bool logInit = true;

    [Header("Read-only preview")]
    public bool previewValid = false;
    public string previewError = "";
    public double timestampJDPreview = 0.0;

    [Header("Read-only debug")]
    public double craftMoonRange_km = 0.0;
    public double rawRelRadius_km = 0.0;
    public double rawRelSpeed_kmps = 0.0;

    public double rotatedPosX_m = 0.0;
    public double rotatedPosY_m = 0.0;
    public double rotatedPosZ_m = 0.0;

    public double rotatedVelX_mps = 0.0;
    public double rotatedVelY_mps = 0.0;
    public double rotatedVelZ_mps = 0.0;

    private const byte EarthId = 1;

    private const double Pi = 3.14159265358979323846;
    private const double TwoPi = 6.28318530717958647692;
    private const double Deg2Rad = 0.01745329251994329577;

    private void OnValidate()
    {
        RefreshPreview();
    }

    public void RefreshPreview()
    {
        double jd;
        string err;

        if (!TryParseIsoTimestampToJulianDate(isoTimestamp, out jd, out err))
        {
            previewValid = false;
            previewError = err;
            timestampJDPreview = 0.0;
            return;
        }

        previewValid = true;
        previewError = "";
        timestampJDPreview = jd;
    }

    public bool TryGetScenarioJd0(out double jd0)
    {
        string err;
        return TryParseIsoTimestampToJulianDate(isoTimestamp, out jd0, out err);
    }

    public bool InitializeNow()
    {
        if (bodies == null || craft == null)
            return false;

        double jd0;
        string timeErr;
        if (!TryParseIsoTimestampToJulianDate(isoTimestamp, out jd0, out timeErr))
        {
            if (logInit)
                Debug.Log("[CraftInitializer_FromEarthCenteredApiState] Time parse failed: " + timeErr);
            return false;
        }

        double T0 = t0Seconds;

        if (ephemSystem != null)
            ephemSystem.Evaluate(T0);

        // -----------------------------------------------------------------
        // Earth heliocentric state in sim solver frame
        // -----------------------------------------------------------------
        double ex, ey, ez, evx, evy, evz;
        bodies.GetBodyState(EarthId, out ex, out ey, out ez, out evx, out evy, out evz);

        // -----------------------------------------------------------------
        // Raw API state: Earth-centered, km / km/s
        // -----------------------------------------------------------------
        double rxRel_m = posX_km * 1000.0;
        double ryRel_m = posY_km * 1000.0;
        double rzRel_m = posZ_km * 1000.0;

        double vxRel_mps = velX_kmps * 1000.0;
        double vyRel_mps = velY_kmps * 1000.0;
        double vzRel_mps = velZ_kmps * 1000.0;

        rawRelRadius_km = System.Math.Sqrt(
            posX_km * posX_km +
            posY_km * posY_km +
            posZ_km * posZ_km
        );

        rawRelSpeed_kmps = System.Math.Sqrt(
            velX_kmps * velX_kmps +
            velY_kmps * velY_kmps +
            velZ_kmps * velZ_kmps
        );

        // -----------------------------------------------------------------
        // Optional frame rotation: Earth equatorial -> ecliptic
        // Rotation is about +X by -epsilon
        // -----------------------------------------------------------------
        double rxE_m = rxRel_m;
        double ryE_m = ryRel_m;
        double rzE_m = rzRel_m;

        double vxE_mps = vxRel_mps;
        double vyE_mps = vyRel_mps;
        double vzE_mps = vzRel_mps;

        if (rotateEquatorialToEcliptic)
        {
            double epsRad = obliquityDeg * Deg2Rad;
            double c = System.Math.Cos(-epsRad);
            double s = System.Math.Sin(-epsRad);

            // Position
            rxE_m = rxRel_m;
            ryE_m = ryRel_m * c - rzRel_m * s;
            rzE_m = ryRel_m * s + rzRel_m * c;

            // Velocity
            vxE_mps = vxRel_mps;
            vyE_mps = vyRel_mps * c - vzRel_mps * s;
            vzE_mps = vyRel_mps * s + vzRel_mps * c;
        }

        rotatedPosX_m = rxE_m;
        rotatedPosY_m = ryE_m;
        rotatedPosZ_m = rzE_m;

        rotatedVelX_mps = vxE_mps;
        rotatedVelY_mps = vyE_mps;
        rotatedVelZ_mps = vzE_mps;

        // -----------------------------------------------------------------
        // Earth-centered inertial -> heliocentric inertial
        // -----------------------------------------------------------------
        craft.primaryBodyId = EarthId;

        craft.rx = ex + rxE_m;
        craft.ry = ey + ryE_m;
        craft.rz = ez + rzE_m;

        craft.vx = evx + vxE_mps;
        craft.vy = evy + vyE_mps;
        craft.vz = evz + vzE_mps;

        // -----------------------------------------------------------------
        // Attitude
        // -----------------------------------------------------------------
        if (resetAttitudeState && craftAtt != null)
            craftAtt.ResetState();

        // -----------------------------------------------------------------
        // Docking / runtime cleanup
        // -----------------------------------------------------------------
        if (disableDockingOnInit && simManager != null)
            simManager.dockingAllowed = false;

        if (simManager != null && simManager.dock != null)
            simManager.dock.ResetState();

        // -----------------------------------------------------------------
        // Fit conic from resulting state
        // -----------------------------------------------------------------
        if (autoFitConicAfterSettingState && fitter != null)
            fitter.Fit(EarthId, T0);

        if (craftConic != null)
            craftConic.primaryBodyId = EarthId;

        // -----------------------------------------------------------------
        // Force rails mode for startup
        // -----------------------------------------------------------------
        if (setRailsModeOnInit && netCore != null && Networking.IsOwner(netCore.gameObject))
        {
            netCore.SetMode(SimManager.MODE_RAILS, EarthId, true);
            netCore.ForcePublishCore();
        }

        // -----------------------------------------------------------------
        // Optional Moon sanity check
        // -----------------------------------------------------------------
        if (useMoonDebugVector)
        {
            double dx = moonPosX_km - posX_km;
            double dy = moonPosY_km - posY_km;
            double dz = moonPosZ_km - posZ_km;
            craftMoonRange_km = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        else
        {
            craftMoonRange_km = 0.0;
        }

        if (logInit)
        {
            Debug.Log(
                "[CraftInitializer_FromEarthCenteredApiState] Init " +
                "T0=" + T0.ToString("F2") +
                " jd0=" + jd0.ToString("F8") +
                " rotateEqToEcl=" + rotateEquatorialToEcliptic +
                " obliqDeg=" + obliquityDeg.ToString("F6") +
                " rawRelR_km=" + rawRelRadius_km.ToString("F3") +
                " rawRelV_kmps=" + rawRelSpeed_kmps.ToString("F6") +
                " moonRange_km=" + craftMoonRange_km.ToString("F3")
            );

            Debug.Log(
                "[CraftInitializer_FromEarthCenteredApiState] Rotated rel state (m / m/s) " +
                "r=(" +
                rotatedPosX_m.ToString("F3") + ", " +
                rotatedPosY_m.ToString("F3") + ", " +
                rotatedPosZ_m.ToString("F3") + ") " +
                "v=(" +
                rotatedVelX_mps.ToString("F6") + ", " +
                rotatedVelY_mps.ToString("F6") + ", " +
                rotatedVelZ_mps.ToString("F6") + ")"
            );

            Debug.Log(
                "[CraftInitializer_FromEarthCenteredApiState] Final heliocentric state " +
                "r=(" +
                craft.rx.ToString("F3") + ", " +
                craft.ry.ToString("F3") + ", " +
                craft.rz.ToString("F3") + ") " +
                "v=(" +
                craft.vx.ToString("F6") + ", " +
                craft.vy.ToString("F6") + ", " +
                craft.vz.ToString("F6") + ")"
            );
        }

        return true;
    }

    private static bool TryParseIsoTimestampToJulianDate(string iso, out double jd, out string err)
    {
        jd = 0.0;
        err = "";

        string s = SafeTrim(iso);
        if (s == "")
        {
            err = "Missing ISO timestamp.";
            return false;
        }

        // Expected quick-format support:
        // YYYY-MM-DDTHH:MM:SSZ
        // Also accepts space instead of T, and optional fractional seconds before Z
        if (s.Length < 19)
        {
            err = "Timestamp too short.";
            return false;
        }

        bool ok = true;

        int year = ParseIntLoose(Sub(s, 0, 4), ref ok);
        if (!ok) { err = "Failed parsing year."; return false; }

        if (CharAt(s, 4) != '-') { err = "Expected '-' after year."; return false; }

        int month = ParseIntLoose(Sub(s, 5, 2), ref ok);
        if (!ok) { err = "Failed parsing month."; return false; }

        if (CharAt(s, 7) != '-') { err = "Expected '-' after month."; return false; }

        int day = ParseIntLoose(Sub(s, 8, 2), ref ok);
        if (!ok) { err = "Failed parsing day."; return false; }

        char sep = CharAt(s, 10);
        if (sep != 'T' && sep != 't' && sep != ' ')
        {
            err = "Expected 'T' between date and time.";
            return false;
        }

        int hour = ParseIntLoose(Sub(s, 11, 2), ref ok);
        if (!ok) { err = "Failed parsing hour."; return false; }

        if (CharAt(s, 13) != ':') { err = "Expected ':' after hour."; return false; }

        int minute = ParseIntLoose(Sub(s, 14, 2), ref ok);
        if (!ok) { err = "Failed parsing minute."; return false; }

        if (CharAt(s, 16) != ':') { err = "Expected ':' after minute."; return false; }

        // Parse seconds and optional fraction
        int i = 17;

        int secInt = 0;
        bool foundSecDigit = false;
        while (i < s.Length)
        {
            char c = s[i];
            if (c >= '0' && c <= '9')
            {
                foundSecDigit = true;
                secInt = secInt * 10 + (c - '0');
                i++;
            }
            else
            {
                break;
            }
        }

        if (!foundSecDigit)
        {
            err = "Failed parsing seconds.";
            return false;
        }

        double fracSec = 0.0;
        if (i < s.Length && s[i] == '.')
        {
            i++;

            double place = 0.1;
            bool foundFracDigit = false;

            while (i < s.Length)
            {
                char c = s[i];
                if (c >= '0' && c <= '9')
                {
                    foundFracDigit = true;
                    fracSec += (c - '0') * place;
                    place *= 0.1;
                    i++;
                }
                else
                {
                    break;
                }
            }

            if (!foundFracDigit)
            {
                err = "Invalid fractional seconds.";
                return false;
            }
        }

        // Optional Z suffix. For this quick initializer, require UTC/Z if suffix present.
        if (i < s.Length)
        {
            char tail = s[i];
            if (tail == 'Z' || tail == 'z')
            {
                i++;
            }
        }

        // Remaining chars must be whitespace only
        for (; i < s.Length; i++)
        {
            char c = s[i];
            if (c != ' ' && c != '\t' && c != '\r' && c != '\n')
            {
                err = "Unexpected trailing characters in timestamp.";
                return false;
            }
        }

        if (month < 1 || month > 12)
        {
            err = "Month out of range.";
            return false;
        }

        int dim = DaysInMonth(year, month);
        if (day < 1 || day > dim)
        {
            err = "Day out of range.";
            return false;
        }

        if (hour < 0 || hour > 23)
        {
            err = "Hour out of range.";
            return false;
        }

        if (minute < 0 || minute > 59)
        {
            err = "Minute out of range.";
            return false;
        }

        if (secInt < 0 || secInt > 59)
        {
            err = "Second out of range.";
            return false;
        }

        double second = secInt + fracSec;
        jd = GregorianToJulianDate(year, month, day, hour, minute, second);
        return true;
    }

    private static int DaysInMonth(int year, int month)
    {
        switch (month)
        {
            case 1: return 31;
            case 2: return IsLeapYear(year) ? 29 : 28;
            case 3: return 31;
            case 4: return 30;
            case 5: return 31;
            case 6: return 30;
            case 7: return 31;
            case 8: return 31;
            case 9: return 30;
            case 10: return 31;
            case 11: return 30;
            case 12: return 31;
        }

        return 31;
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
            System.Math.Floor(365.25 * (Y + 4716)) +
            System.Math.Floor(30.6001 * (M + 1)) +
            D + B - 1524.5;

        return jd;
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

    private static char CharAt(string s, int idx)
    {
        if (s == null) return '\0';
        if (idx < 0 || idx >= s.Length) return '\0';
        return s[idx];
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
}