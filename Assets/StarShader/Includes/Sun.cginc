#ifndef SUN_CGINC_INCLUDED
#define SUN_CGINC_INCLUDED

#include "UnityCG.cginc"

// =============================
// Uniforms (match your shader)
// =============================

float _SunAngularRadiusDeg;
float _SunEdgeSoftness;
float4 _SunColor;
float _SunIntensity;

float _AtmoEnable;
float4 _AtmoUpWS;

float4 _SunExtRGB;
float _SunExtStrength;
float _SunHorizonFadeDeg;

float _SkyEnable;
float4 _SkyBase;
float _SkyStrength;
float4 _SkyExtRGBSun;
float _SkyExtStrengthSun;

float _RayleighStrength;
float _MieStrength;
float _MieG;
float4 _RayleighTint;
float4 _MieTint;

float4 _MieExtRGB;
float _MieExtStrength;

float _MieClampDeg;

float _TwilightStartDeg;
float _TwilightEndDeg;
float _EarthShadowAltDeg;
float _EarthShadowStrength, _EarthShadowMu0Max, _EarthShadowSoftness;

float _GroundEnable, _HorizonHeight, _HorizonSoftness;
float4 _GroundColorDay, _GroundColorNight;
float _GroundTwilightLift, _GroundDayAltDeg;

float _RefractionEnable, _RefractionStrength, _RefractionMaxDeg;
float _DiskBelowHorizonDeg, _FlattenMin;

float _DitherAmp;

// --- Visibility suppression driven by sun-lit sky brightness ---
float _StarSunDimEnable, _StarSunDimStrength, _StarSunDimPower, _StarSunDimFloor;
float _StarSunMieWeight;
float _StarSunDayFloor;
float _StarSunDayAltKneeDeg;
float _MoonSunDimEnable, _MoonSunDimStrength, _MoonSunDimPower, _MoonSunDimFloor;

// --- Sun perceptual optics (glare + spikes) ---
// Use a unique prefix to avoid collisions with other includes.
float _SunPercept_GlareEnable;
float _SunPercept_GlareStrength;
float _SunPercept_GlareRadiusDeg;
float _SunPercept_GlarePower;
float _SunPercept_GlareGlanceScale; // 0..1 (glare retained when not staring)
float _SunPercept_VeilStrength;     // how strongly glare suppresses disk detail

float _SunPercept_SpikeEnable;
float _SunPercept_SpikeStrength;
float _SunPercept_SpikeCount;       // lobes count
float _SunPercept_SpikeSharpness;   // higher = thinner spikes
float _SunPercept_SpikeFalloffPow;  // radial falloff
float _SunPercept_SpikeOuterRadii;  // extent in sun radii

float _SunPercept_StareStartDeg;    // center-in-FOV start
float _SunPercept_StareEndDeg;      // center-in-FOV end

// =============================
// Result type
// =============================
struct SunResult
{
    float3 addRGB;    // sky + disk + perceptual glare/spikes combined
    float  starMul;   // multiply stars by this
    float  moonMul;   // multiply moon contribution by this
    float  muV;       // view up dot
    float  sunAltDeg; // sun altitude (deg)
};

// =============================
// Helpers (local to this cginc)
// =============================

inline float Luminance(float3 rgb)
{
    return dot(rgb, float3(0.2126, 0.7152, 0.0722));
}

inline float RefractionDeg_Bennett(float altDeg)
{
    float a = max(altDeg, -1.0);
    float denomDeg = a + 10.3 / (a + 5.11);
    denomDeg = max(denomDeg, 0.1);

    float R_arcmin = 1.02 / tan(radians(denomDeg));
    float R_deg = R_arcmin / 60.0;

    return min(R_deg, _RefractionMaxDeg);
}

inline float3 RotateTowardUpByDeg(float3 dir, float3 up, float deltaDeg)
{
    float3 east = cross(dir, up);
    float e2 = dot(east, east);
    if (e2 < 1e-8) return dir;
    east *= rsqrt(e2);

    float a = radians(deltaDeg);
    return normalize(
        dir * cos(a) +
        cross(east, dir) * sin(a) +
        east * dot(east, dir) * (1.0 - cos(a))
    );
}

// Glare lobe in angular space (cosAng = dot(viewDir, sunDirDiskWS))
inline float SunGlareLobe(float cosAng, float innerCos, float power)
{
    // Maps [innerCos..1] -> [0..1], then sharpens.
    float t = saturate((cosAng - innerCos) / max(1e-4, (1.0 - innerCos)));
    return pow(t, power);
}

// Starburst (diffraction spike) pattern in sun tangent plane.
// p is dimensionless if you pass (p / Rs) where Rs = tan(sunAngularRadius).
inline float Starburst(float2 p, float spikes, float sharpness, float falloffPow)
{
    float r = max(length(p), 1e-4);
    float a = atan2(p.y, p.x);
    float s = abs(cos(a * spikes));      // lobes
    s = pow(s, sharpness);               // thinner spikes
    float atten = 1.0 / pow(r, falloffPow);
    return s * atten;
}

// =============================
// Core: render sun sky + disk + return multipliers
//
// Inputs:
//   viewDir   : normalized sky direction (world-space for skybox mesh)
//   sunDirWS  : normalized direction from viewer toward sun (WS)
// =============================
inline SunResult RenderSunSky(float3 viewDir, float3 sunDirWS)
{
    SunResult o;
    o.addRGB = 0;
    o.starMul = 1.0;
    o.moonMul = 1.0;
    o.muV = 0.0;
    o.sunAltDeg = 0.0;

    float3 upWS = normalize(_AtmoUpWS.xyz);
    if (dot(upWS, upWS) < 1e-4) upWS = float3(0,1,0);

    // Shared dots
    float muS = dot(sunDirWS, upWS);
    float muV = dot(viewDir, upWS);
    o.muV = muV;

    float sunAltDeg = degrees(asin(clamp(muS, -1.0, 1.0)));
    o.sunAltDeg = sunAltDeg;

    float twilight = smoothstep(_TwilightStartDeg, _TwilightEndDeg, sunAltDeg);

    // Earth-shadow / sunlit-atmosphere gating
    float mu = dot(viewDir, sunDirWS);
    float tLow = saturate((_EarthShadowAltDeg - sunAltDeg) / max(_EarthShadowAltDeg, 1e-3));
    float mu0 = lerp(-1.0, _EarthShadowMu0Max, tLow);

    float lo = mu0 - _EarthShadowSoftness;
    float hi = mu0 + _EarthShadowSoftness;
    float sunlitView = smoothstep(lo, hi, mu);
    sunlitView = lerp(1.0, sunlitView, _EarthShadowStrength);

    float r = radians(_SunAngularRadiusDeg);

    // ----------------------
    // Disk direction + appearance
    // ----------------------
    float3 sunDirDiskWS = sunDirWS;
    float sunAltDiskDeg = sunAltDeg;

    // Refraction only when atmosphere is enabled
    if (_AtmoEnable > 0.5 && _RefractionEnable > 0.5)
    {
        float refractDeg = RefractionDeg_Bennett(sunAltDeg) * _RefractionStrength;
        sunAltDiskDeg = sunAltDeg + refractDeg;
        sunDirDiskWS = RotateTowardUpByDeg(sunDirWS, upWS, refractDeg);
    }

    float cosAngDisk = dot(viewDir, sunDirDiskWS);

    // ----------------------
    // Horizon slicing (disk only) + ground star blocking
    // ----------------------
    float diskHMask = 1.0;

    if (_GroundEnable > 0.5)
    {
        // ground blocks stars & moon
        float tH = smoothstep(-_HorizonSoftness, _HorizonSoftness, (muV - _HorizonHeight));
        o.starMul *= tH;
        o.moonMul *= tH;

        float h = (muV - _HorizonHeight);

        // softness in mu-space
        float wMu = max(sin(radians(_SunHorizonFadeDeg)), 1e-4);
        float allowMu = sin(radians(max(_DiskBelowHorizonDeg, 0.0)));

        diskHMask = smoothstep(-wMu, +wMu, h + allowMu);
    }

    // ----------------------
    // "Staring at sun" factor (camera forward vs sun direction)
    // ----------------------
    float3 sp_camFwdWS = normalize(-UNITY_MATRIX_V[2].xyz);
    float sp_sunCenterMu = saturate(dot(sp_camFwdWS, sunDirDiskWS));
    float sp_sunCenterAngDeg = degrees(acos(sp_sunCenterMu));
    float sp_stare = 1.0 - smoothstep(_SunPercept_StareStartDeg, _SunPercept_StareEndDeg, sp_sunCenterAngDeg);
    float sp_glance = 1.0 - sp_stare;

    // ----------------------
    // Early-out for disk/spike tangent-plane math
    // ----------------------
    float diskMask = 0.0;
    float3 spikeRGB = 0.0;

    // We only need the tangent plane when we're close to the sun (disk or spikes).
    float outerR = max(_SunPercept_SpikeOuterRadii, 1.0) * r;       // radians
    float marginRad = radians(0.15);
    float cosCutAny = cos(outerR + marginRad);

    // We'll reuse these if we enter the tangent-plane block.
    float2 p_tan = 0.0;   // tangent-plane coords (x,y)
    float Rs = tan(r);    // sun radius in tangent-plane units

    if (cosAngDisk > cosCutAny && diskHMask > 1e-5)
    {
        // Build basis around disk direction
        float3 right = cross(upWS, sunDirDiskWS);
        float r2b = dot(right, right);
        if (r2b < 1e-8)
        {
            right = cross(float3(1,0,0), sunDirDiskWS);
            r2b = max(dot(right, right), 1e-8);
        }
        right *= rsqrt(r2b);
        float3 up = normalize(cross(sunDirDiskWS, right));

        // Tangent-plane projection
        float denom = max(cosAngDisk, 1e-5);
        float x = dot(viewDir, right) / denom;
        float y = dot(viewDir, up)    / denom;

        // Flattening only when atmosphere enabled
        float flatten = 1.0;
        if (_AtmoEnable > 0.5)
        {
            float flattenT = saturate((sunAltDiskDeg + 1.0) / 10.0);
            flattenT = smoothstep(0.0, 1.0, flattenT);
            flatten = lerp(_FlattenMin, 1.0, flattenT);
        }

        // Store tangent plane coords (with the same flattening used for the disk silhouette)
        p_tan = float2(x, y / max(flatten, 1e-4));

        // Disk mask (only compute if within ~disk radius + AA margin)
        float rr = length(p_tan);
        float rrNorm = rr / max(Rs, 1e-6);

        // Keep fwidth (AA edge)
        float w = fwidth(rrNorm) * max(_SunEdgeSoftness, 1e-3);
        diskMask = 1.0 - smoothstep(1.0 - w, 1.0 + w, rrNorm);
        diskMask = saturate(diskMask);

        // ----------------------
        // Diffraction spikes (optional)
        // ----------------------
        if (_SunPercept_SpikeEnable > 0.5 && _SunPercept_SpikeStrength > 0.0)
        {
            float sp_rr_tan = rr;
            float sp_inner = Rs * 0.95;
            float sp_outer = Rs * max(_SunPercept_SpikeOuterRadii, 1.0);

            float sp_ring = smoothstep(sp_inner, Rs * 1.05, sp_rr_tan) * (1.0 - smoothstep(sp_outer, sp_outer * 1.05, sp_rr_tan));

            float2 sp_pN = p_tan / max(Rs, 1e-6);

            float sp_spikesVal = Starburst(sp_pN, _SunPercept_SpikeCount, _SunPercept_SpikeSharpness, _SunPercept_SpikeFalloffPow);
            sp_spikesVal *= sp_ring;

            // Stronger when glancing; reduced when staring
            float sp_spikeGain = _SunPercept_SpikeStrength * sp_glance* smoothstep(-2.0, 8.0, sunAltDiskDeg);

            spikeRGB = sp_spikeGain * sp_spikesVal * _SunColor.rgb;
        }
    }

    // ----------------------
    // Sky scattering
    // ----------------------
    float3 sky = _SkyBase.rgb;

    float3 mieTerm_forLum = 0.0; // only valid when atmo enabled
    float skyLum = Luminance(sky);

    float3 Tsun = 0.0;

    if (_AtmoEnable > 0.5 && _SkyEnable > 0.5)
    {
        // NOTE: these helpers are assumed to exist in your shared include set:
        // AirMassKastenYoung, TransmittanceRGB, PhaseRayleigh, PhaseHG, Hash12
        float mV = AirMassKastenYoung(muV);
        float mS = AirMassKastenYoung(saturate(muS));

        float3 betaSky = _SkyExtStrengthSun * _SkyExtRGBSun.rgb;

        float3 Tview = TransmittanceRGB(betaSky, mV);
        Tsun  = TransmittanceRGB(betaSky, mS);
        float3 path  = (1.0 - Tview);

        float PR = PhaseRayleigh(mu);

        float3 betaMie = _MieExtStrength * _MieExtRGB.rgb;
        float3 TviewM = TransmittanceRGB(betaMie, mV);
        float3 TsunM  = TransmittanceRGB(betaMie, mS);
        float3 pathM  = (1.0 - TviewM);

        float muClamp = cos(radians(_MieClampDeg));
        float muMie = min(mu, muClamp);
        float PM = PhaseHG(muMie, _MieG);

        float3 ray = _RayleighStrength * PR * _RayleighTint.rgb;
        float3 mie = _MieStrength     * PM * _MieTint.rgb;

        float3 rayTerm = ray * (_SunColor.rgb * Tsun)  * path;
        float3 mieTerm = mie * (_SunColor.rgb * TsunM) * pathM;

        mieTerm_forLum = mieTerm;

        float illum = twilight * sunlitView;

        // Compute lum using the same quantity you're about to add (prevents “disconnect”)
        float3 skyAdd = (_SkyStrength * illum) * (rayTerm + mieTerm);
        skyLum = Luminance(sky + skyAdd);

        sky += skyAdd;

        float mieLum = Luminance(mieTerm_forLum);

        // Star dimming (day floor + soft knee), as you already tuned
        if (_StarSunDimEnable > 0.5)
        {
            float day01 = smoothstep(-6.0, _StarSunDayAltKneeDeg, sunAltDiskDeg);
            float dayFloor = _StarSunDayFloor * day01;

            float baseLum = max(skyLum, dayFloor);
            float dimSignal = baseLum + _StarSunMieWeight * mieLum;

            float k = max(_StarSunDimStrength, 1e-4);
            float dim01 = (k * dimSignal) / (1.0 + k * dimSignal);
            dim01 = pow(saturate(dim01), _StarSunDimPower);

            o.starMul *= max(_StarSunDimFloor, 1.0 - dim01);
        }

        if (_MoonSunDimEnable > 0.5)
        {
            float k = max(_MoonSunDimStrength, 1e-4);
            float dim01 = saturate(1.0 - exp(-k * skyLum * 2.0));
            dim01 = pow(dim01, _MoonSunDimPower);
            o.moonMul *= max(_MoonSunDimFloor, 1.0 - dim01);
        }
    }

    // Dither (keep as you had)
    {
        float n = Hash12(viewDir.xy * 16384.0) - 0.5;
        float ditherAmp = 0.2 / 255.0;
        sky += n * ditherAmp;
    }

    // Ground blend (defines visible horizon)
    if (_GroundEnable > 0.5)
    {
        float dayT = saturate(sunAltDeg / max(_GroundDayAltDeg, 1e-3));
        dayT = smoothstep(0.0, 1.0, dayT);

        float nightToTwilight = lerp(0.0, _GroundTwilightLift, twilight);
        float illumG = max(dayT, nightToTwilight);

        float3 groundCol = lerp(_GroundColorNight.rgb, _GroundColorDay.rgb, illumG);

        float tH = smoothstep(-_HorizonSoftness, _HorizonSoftness, (muV - _HorizonHeight));
        sky = lerp(groundCol, sky, tH);
    }

    // ----------------------
    // Disk (with extinction)
    // ----------------------
    float3 disk = 0.0;

    if (diskMask > 0.0 && diskHMask > 0.0)
    {
        disk = _SunIntensity * _SunColor.rgb * diskMask;
        disk *= diskHMask;

        if (_AtmoEnable > 0.5)
        {
            float mSdisk = AirMassKastenYoung(saturate(muS));
            float3 beta = _SunExtStrength * _SunExtRGB.rgb;
            disk *= TransmittanceRGB(beta, mSdisk);
        }
    }

    // ----------------------
    // Perceptual glare + veiling (optional)
    // ----------------------
    float3 sp_glareRGB = 0.0;

    if (_SunPercept_GlareEnable > 0.5 && _SunPercept_GlareStrength > 0.0)
    {
        float sp_innerCos = cos(radians(max(_SunPercept_GlareRadiusDeg, 0.01)));
        float sp_glare = SunGlareLobe(cosAngDisk, sp_innerCos, max(_SunPercept_GlarePower, 1e-3));

        // Stronger when staring, reduced when glancing
        sp_glare *= lerp(max(_SunPercept_GlareGlanceScale, 0.0), 1.0, sp_stare);

        sp_glareRGB = sp_glare * _SunPercept_GlareStrength * _SunColor.rgb;

        float TsunLum = Luminance(Tsun);
        // Optional: explicit altitude rolloff (art-directable)
        float alt01 = smoothstep(-2.0, 8.0, sunAltDiskDeg); // tweak range
        // Combine (diskHMask already exists; twilight already exists)
        float glareAtten = diskHMask * saturate(twilight) * TsunLum;
        // glareAtten = pow(glareAtten, 1.5);  // 1.2–2.5 range

        sp_glareRGB *= glareAtten;
        // Veil the disk when staring
        float sp_veil = saturate(sp_glare * _SunPercept_VeilStrength * sp_stare);
        disk *= (1.0 - sp_veil);
    }

    // Final combine
    sky = max(sky, 0.0);

    // Keep your existing sky clamp behavior, but add perceptual terms on top.
    // If you want bloom to actually fire, do NOT clamp this output in the calling shader.
    o.addRGB = clamp(sky, 0, 1) + disk + sp_glareRGB + spikeRGB;

    return o;
}

#endif
