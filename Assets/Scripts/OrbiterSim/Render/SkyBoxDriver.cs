using UdonSharp;
using UnityEngine;
using System;
using VRC.SDKBase;

/// <summary>
/// SkyBoxDriver
/// 
/// Drives two dedicated skybox materials:
/// - moon-dominant shader/material
/// - earth-dominant shader/material
///
/// Both materials are kept updated every tick so switching is seamless.
/// Active material is chosen by apparent angular dominance with hysteresis.
///
/// Coordinate contracts:
/// - Solver/ephemeris frame: SSB / heliocentric ECLIPTIC inertial
/// - craftAtt.qBE is BODY -> ECLIPTIC inertial
/// - Shader property names are intentionally shared between both materials
///   so this driver can write the same data into both.
/// </summary>
public class SkyBoxDriver : UdonSharpBehaviour
{
    [Header("Refs")]
    public CraftAttitudeState craftAtt;
    public CraftStateModel craft;
    public BodyCatalog bodies;

    public SimClock clock;
    public CraftNetState netCore;
    public CraftNetAttitude netAtt;
    public CraftNetKinematics netKin;

    [Header("Remote integrated translation presentation")]
    [Tooltip("If true, Sun/Moon/Earth body-relative vectors use CraftNetKinematics presented translation on remotes in integrated mode.")]
    public bool usePresentedRemoteTranslation = true;

    [Header("Skybox materials")]
    [Tooltip("Moon-dominant skybox material.")]
    public Material moonSkyboxMat;

    [Tooltip("Earth-dominant skybox material.")]
    public Material earthSkyboxMat;

    [Tooltip("If true, keep both materials updated every tick and switch active material as needed.")]
    public bool keepBothMaterialsHot = true;

    [Header("Selection")]
    [Tooltip("0 = Moon shader, 1 = Earth shader. Used for debug/readback.")]
    public byte activeSkyMode = 0;

    public const byte SKYMODE_MOON = 0;
    public const byte SKYMODE_EARTH = 1;

    [Tooltip("Angular-radius ratio hysteresis for switching to Earth shader. Example: 1.10 means Earth must be 10% larger to steal focus.")]
    public float earthSwitchInRatio = 1.10f;

    [Tooltip("Angular-radius ratio hysteresis for switching back to Moon shader. Example: 0.90 means Earth must fall below 90% of Moon size.")]
    public float earthSwitchOutRatio = 0.90f;

    [Tooltip("If true, initialize active skybox from current RenderSettings.skybox when possible.")]
    public bool inferInitialModeFromRenderSettings = true;

    [Header("Sun (shader + light)")]
    public bool driveSun = true;

    [Tooltip("Directional light to aim FROM the sun toward the craft (i.e., light direction = -sunDir).")]
    public Light sunLight;

    [Tooltip("If true, also updates the sun angular radius uniform each tick from distance.")]
    public bool driveSunAngularSize = true;

    [Tooltip("Solar radius in meters (for angular size).")]
    public double sunRadiusM = 6.9634e8;

    [Header("Sun eclipse lighting")]
    public bool driveSunEclipseLighting = true;

    [Tooltip("Base directional light intensity when the Sun is unobstructed.")]
    public float baseSunLightIntensity = 1.0f;

    [Tooltip("Minimum light fraction during full eclipse.")]
    public float minEclipsedLightFraction = 0.00f;

    [Header("Moon")]
    public bool driveMoon = true;

    [Tooltip("Override Moon radius (m). If <= 0, uses BodyCatalog moonRadiusM.")]
    public double moonRadiusOverrideM = 0.0;

    [Header("Earth")]
    public bool driveEarth = true;

    [Tooltip("Override Earth radius (m). If <= 0, uses BodyCatalog earthRadiusM.")]
    public double earthRadiusOverrideM = 0.0;

    [Header("Debug")]
    public bool writeEveryFrame = true;

    [Tooltip("Logs sky mode switches.")]
    public bool debugLogSwitches = false;

    private void Start()
    {
        if (inferInitialModeFromRenderSettings)
        {
            Material current = RenderSettings.skybox;
            if (current != null)
            {
                if (earthSkyboxMat != null && current == earthSkyboxMat)
                    activeSkyMode = SKYMODE_EARTH;
                else
                    activeSkyMode = SKYMODE_MOON;
            }
        }

        ApplyActiveSkyboxMaterial();
    }

    public void Tick()
    {
        if (craftAtt == null || craft == null || bodies == null) return;

        Quaternion q = craftAtt.qBE;

        // Remote rendering: sample buffered attitude at presentation time
        if (netCore != null && netAtt != null && clock != null)
        {
            if (!Networking.IsOwner(netCore.gameObject))
            {
                double tRender = clock.GetCachedRemoteRenderTime();
                q = netAtt.SampleRenderQuaternion(tRender);
            }
        }

        // Gather all body/sun state once
        double drx, dry, drz;
        GetRemotePresentedCraftOffset(out drx, out dry, out drz);

        // craft -> sun
        double sx, sy, sz, sunDist;
        bool haveSun = GetCraftToBodyVectorAdjusted(bodies.sunId, drx, dry, drz, out sx, out sy, out sz, out sunDist);

        // craft -> earth
        double ex, ey, ez, earthDist;
        bool haveEarth = GetCraftToBodyVectorAdjusted(bodies.earthId, drx, dry, drz, out ex, out ey, out ez, out earthDist);

        // craft -> moon
        double mx, my, mz, moonDist;
        bool haveMoon = GetCraftToBodyVectorAdjusted(bodies.moonId, drx, dry, drz, out mx, out my, out mz, out moonDist);

        double earthRadiusM = (earthRadiusOverrideM > 0.0) ? earthRadiusOverrideM : bodies.earthRadiusM;
        double moonRadiusM  = (moonRadiusOverrideM > 0.0) ? moonRadiusOverrideM : bodies.moonRadiusM;

        float earthAngRad = 0f;
        float moonAngRad = 0f;

        if (haveEarth && earthDist > 1e-9)
            earthAngRad = (float)Math.Atan(earthRadiusM / earthDist);

        if (haveMoon && moonDist > 1e-9)
            moonAngRad = (float)Math.Atan(moonRadiusM / moonDist);

        UpdateActiveSkyMode(earthAngRad, moonAngRad);
        ApplyActiveSkyboxMaterial();

        if (keepBothMaterialsHot)
        {
            if (moonSkyboxMat != null)
                WriteSharedUniforms(
                    moonSkyboxMat, q,
                    haveSun, sx, sy, sz, sunDist,
                    haveEarth, ex, ey, ez, earthRadiusM,
                    haveMoon, mx, my, mz, moonRadiusM
                );

            if (earthSkyboxMat != null)
                WriteSharedUniforms(
                    earthSkyboxMat, q,
                    haveSun, sx, sy, sz, sunDist,
                    haveEarth, ex, ey, ez, earthRadiusM,
                    haveMoon, mx, my, mz, moonRadiusM
                );
        }
        else
        {
            Material activeMat = GetActiveSkyboxMaterial();
            if (activeMat != null)
            {
                WriteSharedUniforms(
                    activeMat, q,
                    haveSun, sx, sy, sz, sunDist,
                    haveEarth, ex, ey, ez, earthRadiusM,
                    haveMoon, mx, my, mz, moonRadiusM
                );
            }
        }

        // Sun light is independent of which skybox material is active
        if (driveSun && haveSun && sunLight != null)
        {
            double invSun = 1.0 / Math.Max(1e-18, sunDist);
            float ux = (float)(sx * invSun);
            float uy = (float)(sy * invSun);
            float uz = (float)(sz * invSun);

            Vector3 sunDirEcl = new Vector3(ux, uy, uz);

            Quaternion qEB = new Quaternion(-q.x, -q.y, -q.z, q.w);
            Vector3 sunDirBody = qEB * sunDirEcl;
            sunDirBody.x = -sunDirBody.x;
            if (sunDirBody.sqrMagnitude > 1e-10f)
                sunLight.transform.rotation = Quaternion.LookRotation(-sunDirBody, Vector3.up);

            float eclipse = 0f;
            if (driveSunEclipseLighting)
                eclipse = ComputeSunOcclusion01(sx, sy, sz, sunDist, drx, dry, drz);

            float litFrac = 1.0f - eclipse;
            litFrac = Mathf.Max(minEclipsedLightFraction, litFrac);
            sunLight.intensity = baseSunLightIntensity * litFrac;
        }
    }

    private void UpdateActiveSkyMode(float earthAngRad, float moonAngRad)
    {
        // Fallback if one material is missing
        if (earthSkyboxMat == null && moonSkyboxMat == null) return;
        if (earthSkyboxMat == null)
        {
            activeSkyMode = SKYMODE_MOON;
            return;
        }
        if (moonSkyboxMat == null)
        {
            activeSkyMode = SKYMODE_EARTH;
            return;
        }

        // If one body is unavailable, bias to the other
        if (earthAngRad <= 0f && moonAngRad > 0f)
        {
            SetSkyMode(SKYMODE_MOON);
            return;
        }

        if (moonAngRad <= 0f && earthAngRad > 0f)
        {
            SetSkyMode(SKYMODE_EARTH);
            return;
        }

        if (earthAngRad <= 0f && moonAngRad <= 0f)
            return;

        float ratio = earthAngRad / Mathf.Max(1e-8f, moonAngRad);

        if (activeSkyMode == SKYMODE_EARTH)
        {
            if (ratio < earthSwitchOutRatio)
                SetSkyMode(SKYMODE_MOON);
        }
        else
        {
            if (ratio > earthSwitchInRatio)
                SetSkyMode(SKYMODE_EARTH);
        }
    }

    private void SetSkyMode(byte newMode)
    {
        if (newMode == activeSkyMode) return;

        activeSkyMode = newMode;

        if (debugLogSwitches)
        {
            if (newMode == SKYMODE_EARTH)
                Debug.Log("[SkyBoxDriver] Switched to EARTH skybox material.");
            else
                Debug.Log("[SkyBoxDriver] Switched to MOON skybox material.");
        }
    }

    private Material GetActiveSkyboxMaterial()
    {
        if (activeSkyMode == SKYMODE_EARTH)
            return earthSkyboxMat != null ? earthSkyboxMat : moonSkyboxMat;

        return moonSkyboxMat != null ? moonSkyboxMat : earthSkyboxMat;
    }

    private void ApplyActiveSkyboxMaterial()
    {
        Material activeMat = GetActiveSkyboxMaterial();
        if (activeMat != null && RenderSettings.skybox != activeMat)
            RenderSettings.skybox = activeMat;
    }

    private void WriteSharedUniforms(
        Material mat,
        Quaternion q,
        bool haveSun,
        double sx, double sy, double sz, double sunDist,
        bool haveEarth,
        double ex, double ey, double ez, double earthRadiusM,
        bool haveMoon,
        double mx, double my, double mz, double moonRadiusM)
    {
        if (mat == null) return;

        // Sky orientation
        mat.SetVector("_CraftBodyToEq", new Vector4(q.x, q.y, q.z, q.w));

        // Sun
        if (driveSun && haveSun)
        {
            double invD = 1.0 / Math.Max(1e-18, sunDist);
            float ux = (float)(sx * invD);
            float uy = (float)(sy * invD);
            float uz = (float)(sz * invD);

            mat.SetVector("_SunDirEcl", new Vector4(ux, uy, uz, 0f));

            if (driveSunAngularSize && sunRadiusM > 0.0)
            {
                double angRad = Math.Atan(sunRadiusM / sunDist);
                mat.SetFloat("_SunAngRad", (float)angRad);
            }
        }

        // Earth
        if (driveEarth && haveEarth)
        {
            mat.SetVector("_EarthPosEcl", new Vector4((float)ex, (float)ey, (float)ez, 0f));
            mat.SetFloat("_EarthRadiusM", (float)earthRadiusM);

            Quaternion qEarth = bodies.GetBodyFixedToInertial(bodies.earthId);
            mat.SetVector("_EarthBodyToEcl", new Vector4(qEarth.x, qEarth.y, qEarth.z, qEarth.w));
        }

        // Moon
        if (driveMoon && haveMoon)
        {
            mat.SetVector("_MoonPosEcl", new Vector4((float)mx, (float)my, (float)mz, 0f));
            mat.SetFloat("_MoonRadiusM", (float)moonRadiusM);

            Quaternion qMoon = bodies.GetBodyFixedToInertial(bodies.moonId);
            mat.SetVector("_MoonBodyToEcl", new Vector4(qMoon.x, qMoon.y, qMoon.z, qMoon.w));
        }
    }

    private bool GetCraftToBodyVectorAdjusted(
        byte bodyId,
        double drx, double dry, double drz,
        out double tx, out double ty, out double tz,
        out double dist)
    {
        tx = ty = tz = 0.0;
        dist = 0.0;

        if (bodies == null || craft == null) return false;

        // BodyCatalog helper returns (craft - body)
        double dx, dy, dz;
        bodies.GetCraftToBodyVector(bodyId, craft, out dx, out dy, out dz);

        dx += drx;
        dy += dry;
        dz += drz;

        // Convert to (craft -> body)
        tx = -dx;
        ty = -dy;
        tz = -dz;

        dist = Math.Sqrt(tx * tx + ty * ty + tz * tz);
        return dist > 1e-18;
    }

    private void GetRemotePresentedCraftOffset(out double drx, out double dry, out double drz)
    {
        drx = 0.0;
        dry = 0.0;
        drz = 0.0;

        if (craft == null || netCore == null || netKin == null) return;
        if (Networking.IsOwner(netCore.gameObject)) return;
        if (!usePresentedRemoteTranslation) return;
        if (netCore.GetMode() != CraftNetState.MODE_INTEGRATED) return;
        if (!netKin.presentedValid) return;

        drx = netKin.presentedRx - craft.rx;
        dry = netKin.presentedRy - craft.ry;
        drz = netKin.presentedRz - craft.rz;
    }

    private float ComputeSunOcclusion01(
        double sx, double sy, double sz,
        double sunDist,
        double drx, double dry, double drz)
    {
        if (bodies == null || craft == null) return 0f;
        if (sunDist <= 1e-9) return 0f;

        double sunAngRad = Math.Asin(sunRadiusM / sunDist);

        float occEarth = ComputeBodyOcclusion01(
            bodies.earthId,
            (earthRadiusOverrideM > 0.0) ? earthRadiusOverrideM : bodies.earthRadiusM,
            sx, sy, sz,
            sunDist,
            sunAngRad,
            drx, dry, drz
        );

        float occMoon = ComputeBodyOcclusion01(
            bodies.moonId,
            (moonRadiusOverrideM > 0.0) ? moonRadiusOverrideM : bodies.moonRadiusM,
            sx, sy, sz,
            sunDist,
            sunAngRad,
            drx, dry, drz
        );

        return Mathf.Max(occEarth, occMoon);
    }

    private float ComputeBodyOcclusion01(
        byte bodyId,
        double bodyRadiusM,
        double sx, double sy, double sz,
        double sunDist,
        double sunAngRad,
        double drx, double dry, double drz)
    {
        double bx, by, bz, bodyDist;
        if (!GetCraftToBodyVectorAdjusted(bodyId, drx, dry, drz, out bx, out by, out bz, out bodyDist))
            return 0f;

        if (bodyDist >= sunDist) return 0f;

        double invBody = 1.0 / bodyDist;
        double invSun  = 1.0 / sunDist;

        double bux = bx * invBody;
        double buy = by * invBody;
        double buz = bz * invBody;

        double sux = sx * invSun;
        double suy = sy * invSun;
        double suz = sz * invSun;

        double cosSep = bux * sux + buy * suy + buz * suz;
        cosSep = Math.Max(-1.0, Math.Min(1.0, cosSep));
        double sep = Math.Acos(cosSep);

        double bodyAngRad = Math.Asin(bodyRadiusM / bodyDist);

        return EstimateDiskOverlap01((float)sunAngRad, (float)bodyAngRad, (float)sep);
    }

    private float EstimateDiskOverlap01(float sunRad, float occRad, float sep)
    {
        if (sep >= sunRad + occRad) return 0f;

        if (occRad >= sunRad && sep <= occRad - sunRad) return 1f;

        if (sunRad > occRad && sep <= sunRad - occRad)
        {
            float areaRatio = (occRad * occRad) / (sunRad * sunRad);
            return Mathf.Clamp01(areaRatio);
        }

        float x = 1f - Mathf.InverseLerp(sunRad + occRad, Mathf.Abs(sunRad - occRad), sep);
        return Mathf.Clamp01(x * x);
    }
}