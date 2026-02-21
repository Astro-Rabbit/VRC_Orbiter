#ifndef SUN_CGINC_INCLUDED
#define SUN_CGINC_INCLUDED

#include "UnityCG.cginc"

// =============================
// Uniforms (match your shader)
// =============================

float _UseSceneLightForSun;
float4 _SunDirWS;
float _FlipSun;

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
float4 _SkyExtRGB;
float _SkyExtStrength;

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

// =============================
// Result type
// =============================
struct SunResult
{
    float3 addRGB;    // sky + disk combined (what you used to output)
    float  starMul;   // multiply stars by this
    float  moonMul;   // multiply moon contribution by this (optional)
    float  muV;       // optional: view up dot (debug/other systems)
    float  sunAltDeg; // optional
};

// =============================
// Helpers (local to this cginc)
// =============================





inline float3 Transmittance(float3 betaRGB, float airmass)
{
    return exp(-betaRGB * airmass);
}



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

// =============================
// Core: render sun sky + disk + return multipliers
//
// Inputs:
//   viewDir   : normalized sky direction (world-space for skybox mesh)
//   screenPos : ComputeScreenPos(o.pos) from the calling shader (for dithering)
// =============================
inline SunResult RenderSunSky(float3 viewDir, float3 sunDirWS)
{
    SunResult o;
    o.addRGB = 0;
    o.starMul = 1.0;
    o.moonMul = 1.0;
    o.muV = 0.0;
    o.sunAltDeg = 0.0;

    // Resolve sun direction

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
        // ground blocks stars entirely
        float tH = smoothstep(-_HorizonSoftness, _HorizonSoftness, (muV - _HorizonHeight));
        o.starMul *= tH;

        float h = (muV - _HorizonHeight);

        // softness in mu-space
        float wMu = max(sin(radians(_SunHorizonFadeDeg)), 1e-4);
        float allowMu = sin(radians(max(_DiskBelowHorizonDeg, 0.0)));

        diskHMask = smoothstep(-wMu, +wMu, h + allowMu);
    }

    // ----------------------
    // Early-out for disk math
    // ----------------------
    float diskMask = 0.0;

    float marginRad = 0.35 * r + radians(0.15);
    float cosCut = cos(r + marginRad);

    if (cosAngDisk > cosCut && diskHMask > 1e-5)
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

        float rr = sqrt(x*x + (y*y) / max(flatten*flatten, 1e-4));
        float rrNorm = rr / max(tan(r), 1e-6);

        // Keep fwidth (AA edge)
        float w = fwidth(rrNorm) * max(_SunEdgeSoftness, 1e-3);
        diskMask = 1.0 - smoothstep(1.0 - w, 1.0 + w, rrNorm);
        diskMask = saturate(diskMask);
    }

    // ----------------------
    // Sky scattering
    // ----------------------
    float3 sky = _SkyBase.rgb;

    float3 mieTerm_forLum = 0.0; // only valid when atmo enabled

    if (_AtmoEnable > 0.5 && _SkyEnable > 0.5)
    {
        float mV = AirMassKastenYoung(muV);
        float mS = AirMassKastenYoung(saturate(muS));

        float3 betaSky = _SkyExtStrength * _SkyExtRGB.rgb;

        float3 Tview = Transmittance(betaSky, mV);
        float3 Tsun  = Transmittance(betaSky, mS);
        float3 path  = (1.0 - Tview);

        float PR = PhaseRayleigh(mu);

        float3 betaMie = _MieExtStrength * _MieExtRGB.rgb;
        float3 TviewM = Transmittance(betaMie, mV);
        float3 TsunM  = Transmittance(betaMie, mS);
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
        float skyLum = Luminance(sky+(_SkyStrength *twilight* (rayTerm + mieTerm)));
        sky += _SkyStrength * illum * (rayTerm + mieTerm);

        // --- Dimming terms (only meaningful when atmo is enabled) ---
        
        float mieLum = Luminance(mieTerm_forLum);

        if (_StarSunDimEnable > 0.5)
        {            
            float day01 = smoothstep(-6.0, _StarSunDayAltKneeDeg, sunAltDiskDeg);
            float dayFloor = _StarSunDayFloor * day01;
            float baseLum = max(skyLum, dayFloor);
            float dimSignal = baseLum + _StarSunMieWeight * mieLum;

            float k = max(_StarSunDimStrength, 1e-4);
            // Soft knee: grows smoothly, never creates a hard threshold.
            // For small dimSignal: dim01 ~ k*dimSignal
            // For large dimSignal: dim01 -> 1
            float dim01 = (k * dimSignal) / (1.0 + k * dimSignal);
            dim01 = pow(saturate(dim01), _StarSunDimPower);

            o.starMul *= max(_StarSunDimFloor, 1.0 - dim01);
        }

        if (_MoonSunDimEnable > 0.5)
        {
            float k = _MoonSunDimStrength;
            float dim01 = saturate(1.0 - exp(-k * skyLum));
            dim01 = pow(dim01, _MoonSunDimPower);
            o.moonMul *= max(_MoonSunDimFloor, 1.0 - dim01);
        }
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
            disk *= Transmittance(beta, mSdisk);
        }
    }

    // Dither (keep as you had)
    float n = Hash12(viewDir.xy * 16384.0) - 0.5;

    // Scale: start around 1/255 in linear space; adjust by eye
    float ditherAmp = 0.2 / 255.0;
    sky += n * ditherAmp;
    sky = max(sky, 0.0);

    o.addRGB = clamp(sky, 0, 1) + disk;
    return o;
}

#endif
