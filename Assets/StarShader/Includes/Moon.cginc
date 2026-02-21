#ifndef MOON_CGINC_INCLUDED
#define MOON_CGINC_INCLUDED

#include "UnityCG.cginc"

// ---------- Moon resources ----------
sampler2D _MoonEquirect;
sampler2D _MoonNormal;

// ---------- Moon parameters ----------
float _MoonAngularRadiusDeg;
float _MoonEdgeSoftness;

float _MoonRotYawDeg;
float _MoonRotPitchDeg;
float _MoonRotRollDeg;

float4 _MoonDirWS;
float4 _LunarNorthPoleWS;


float _TerminatorPower;
float _Ambient;

float _NormalStrength;

// Moon disk extinction (if you added it)
float4 _MoonExtRGB;
float _MoonExtStrength;

// Moon sky params
float4 _SkyBaseNight;
float4 _SkyExtRGB;
float _SkyExtStrength;

float _MoonSkyEnable, _MoonSkyStrength, _MoonSkyPhasePower, _MoonSkyHorizonBoost, _MoonSkyWidthDeg;
float4 _MoonSkyColor;

float _RayleighStrengthMoon, _MieStrengthMoon, _MieGMoon;

// Halo / glare options (keep even if disabled)
float _HaloEnable, _HaloStrength, _HaloWidthDeg, _HaloPhasePower;
float4 _HaloTint;

// Optional "pop" controls (if you added them)
float _OppositionEnable, _OppositionBoost, _OppositionPower;
float _GlareEnable, _GlareStrength, _GlareWidthDeg, _GlarePhasePower;
float4 _GlareTint;

float _MoonHDRBoost;

float _StarDimEnable;
float _StarDimStrength;
float _StarDimWidthDeg;
float _StarDimPhasePower;
float _StarDimFloor;

// ---------- Lunar eclipse ----------
float _LunarEclipseEnable;
float _LunarUmbraRadiusDeg;
float _LunarPenumbraRadiusDeg;
float _LunarEclipseSoftnessPx;

float _LunarPenumbraMinLight;
float _LunarUmbraMinLight;

float4 _LunarUmbraTint;
float _LunarUmbraTintStrength;
float _LunarUmbraTintPower;

float _LunarEclipseGlowDamp;


struct MoonResult
{
    float3 addRGB;   // moon disk + moon sky (additive)
    float  starMul;  // multiply stars by this (<=1)
    half  phaseFull; // optional debug/other use
    float  moonUp;    // optional
    float mask;
};

// ---------- Helpers (copy from your moon shader) ----------

float3x3 RotX(float a)
{
    float s = sin(a), c = cos(a);
    return float3x3(
        1, 0, 0,
        0, c,-s,
        0, s, c
    );
}
float3x3 RotY(float a)
{
    float s = sin(a), c = cos(a);
    return float3x3(
        c, 0, s,
        0, 1, 0,
        -s, 0, c
    );
}
float3x3 RotZ(float a)
{
    float s = sin(a), c = cos(a);
    return float3x3(
        c,-s, 0,
        s, c, 0,
        0, 0, 1
    );
}

float3x3 MoonBodyRotation()
{
    float yaw   = radians(_MoonRotYawDeg);
    float pitch = radians(_MoonRotPitchDeg);
    float roll  = radians(_MoonRotRollDeg);
    return mul(RotZ(roll), mul(RotX(pitch), RotY(yaw)));
}

float2 SphereToEquirectUV(float3 nLocal)
{
    // nLocal: unit vector on sphere in "moon-local" coords
    // Choose a consistent convention:
    // lon wraps around Y axis, lat is Y
    float lon = atan2(nLocal.x, nLocal.z);             // [-pi, pi]
    float lat = asin(clamp(nLocal.y, -1.0, 1.0));      // [-pi/2, pi/2]

    float2 uv;
    uv.x = lon / (2.0 * UNITY_PI) + 0.5;
    uv.y = 0.5 - lat / UNITY_PI;
    return uv;
}

void SphereTangentBasis(float3 n, out float3 tLon, out float3 tLat)
{
    // Tangent along increasing longitude (around Y axis)
    float3 up = float3(0,1,0);
    tLon = cross(up, n);
    float len2 = dot(tLon, tLon);
    if (len2 < 1e-6) tLon = float3(1,0,0);
    else tLon *= rsqrt(len2);

    // Tangent along increasing latitude
    tLat = normalize(cross(n, tLon));
}

float3 PerturbNormalFromNormalMap(float2 uv, float3 nLocal)
{
    float3 tLon, tLat;
    SphereTangentBasis(nLocal, tLon, tLat);
    float3 nTS = UnpackNormal(tex2D(_MoonNormal, uv));
    nTS.xy *= _NormalStrength;
    return normalize(nTS.x * tLon + nTS.y * tLat + nTS.z * nLocal);
}

// ---------- Core function ----------
// Returns additive contribution (moon disk + moonlit sky) to be added on top of stars.
MoonResult RenderMoon(float3 viewDir, float3 sunDirWS)
{
    // IMPORTANT: this function should NOT depend on v2f, screenPos, etc.
    // It should only use viewDir and globals.

    MoonResult o;
    o.addRGB = 0;
    o.starMul = 1;
    o.phaseFull = 0;
    o.moonUp = 0;
    o.mask = 0;

    float3 moonDir = normalize(_MoonDirWS.xyz);

    // Disk angular mask
    float r = radians(_MoonAngularRadiusDeg);
    float cosR = cos(r);
    float cosAng = dot(viewDir, moonDir);

    float wAng = fwidth(cosAng);
    float maskAng = smoothstep(cosR - wAng, cosR + wAng, cosAng);

    // Projection basis: worldUp-derived (matches your optimized moon shader)
    float3 worldUp = float3(0,1,0);
    float3 rightP = cross(worldUp, moonDir);
    float len2r = dot(rightP, rightP);
    if (len2r < 1e-6)
    {
        rightP = cross(float3(1,0,0), moonDir);
        len2r = max(dot(rightP, rightP), 1e-6);
    }
    rightP *= rsqrt(len2r);
    float3 upP = normalize(cross(moonDir, rightP));

    // Tangent-plane projection
    float denom = max(cosAng, 1e-5);
    float x = dot(viewDir, rightP) / denom;
    float y = dot(viewDir, upP)    / denom;

    float scale = 1.0 / tan(r);
    float2 uvDisk = float2(x, y) * (0.5 * scale) + 0.5;

    float2 p = uvDisk * 2.0 - 1.0;
    float r2 = dot(p, p);
    float rDisk = sqrt(max(r2, 0.0));
    float w = fwidth(rDisk) * _MoonEdgeSoftness;
    float maskDisk = 1.0 - smoothstep(1.0 - w, 1.0 + w, rDisk);

    float mask = saturate(maskAng * maskDisk);


    // -----------------------------
    // Lunar eclipse masks (computed in sky direction space)
    // Shadow center is anti-solar direction (good first-order).
    // -----------------------------
    float penMask = 0.0;   // 1 = inside penumbra
    float umbMask = 0.0;   // 1 = inside umbra
    float eclipseSep = 0.0;

    if (_LunarEclipseEnable > 0.5)
    {
        float3 shadowDirWS = sunDirWS;

        // Separation test in cosine space (avoid acos)
        float eclipseSep = dot(viewDir, shadowDirWS);

        float cosPen = cos(radians(_LunarPenumbraRadiusDeg));
        float cosUmb = cos(radians(_LunarUmbraRadiusDeg));

        // AA width in cosine space + artist softness in pixels
        float wCos = fwidth(eclipseSep) * max(_LunarEclipseSoftnessPx, 1e-3);

        // inside means cosSep > cos(radius)
        penMask = smoothstep(cosPen - wCos, cosPen + wCos, eclipseSep);
        umbMask = smoothstep(cosUmb - wCos, cosUmb + wCos, eclipseSep);

        // Ensure umbra is a subset of penumbra
        umbMask = min(umbMask, penMask);

        // Only apply where the moon exists (optional but helps avoid weird interaction)
        penMask *= mask;
        umbMask *= mask;
        
    }


    // ---------- Sun direction ----------
    // Expect caller to pass sunDirWS already resolved from scene light/override/flip.
    // Convert to local projection frame:
    float3 sunLocal = normalize(float3(
        dot(sunDirWS, -rightP),
        dot(sunDirWS, -upP),
        dot(sunDirWS, moonDir)
    ));

    float muM = dot(moonDir, worldUp);
    float moonUp = smoothstep(-0.035, 0.017, muM);

    half cosEl = clamp(dot(moonDir, -sunDirWS), -1.0, 1.0);
    half phase = 0.5 * (1.0 - cosEl); // 0=new, 1=full   

    // ---------- Moonlit sky contribution (even when not in disk) ----------
    float3 sky = _SkyBaseNight.rgb;
    float3 TmoonDisk = 1;
    float starMul = 1.0;

    if (_MoonSkyEnable > 0.5)
    {
        float muV = dot(viewDir, worldUp);
        float mV = AirMassKastenYoung(muV);
        float mM = AirMassKastenYoung(muM);
        float horizon = saturate((mV - 1.0) / 6.0);
        horizon = pow(horizon, 1.5);
        TmoonDisk = TransmittanceRGB((_MoonExtStrength * _MoonExtRGB.rgb), mM);

        if (moonUp > 1e-3)
        {
            

            float3 betaSky = _SkyExtStrength * _SkyExtRGB.rgb;
            float3 Tview = TransmittanceRGB(betaSky, mV);
            float3 Tmoon = TransmittanceRGB(betaSky, mM);
            float3 path = (1.0 - Tview);

            // Phase proxy (use your updated smooth phase mapping if you changed it)
            float phaseScale = pow(phase, _MoonSkyPhasePower);

            float cs = clamp(dot(viewDir, moonDir), -1.0, 1.0);
            float oneMinusCos = 1.0 - cs;

            float wRad = radians(_MoonSkyWidthDeg);
            float invSigma2 = 1.0 / max(wRad * wRad, 1e-6);
            float ang = exp(-oneMinusCos * invSigma2);

            float lowAltBoost = saturate((mM - 1.0) / 6.0);
            float boost = 1.0 + _MoonSkyHorizonBoost * horizon;

            float I = _MoonSkyStrength * phaseScale * boost * moonUp;
            I *= (1.0 + 0.6 * lowAltBoost);

            float PR = PhaseRayleigh(cs);
            float PM = PhaseHG(cs, _MieGMoon);

            sky += I * ang * Tmoon * path * (_RayleighStrengthMoon * PR + _MieStrengthMoon * PM) * _MoonSkyColor.rgb;

            if (_HaloEnable > 0.5)
            {
                float wH = max(radians(_HaloWidthDeg), 1e-4);
                float invWH2 = 1.0 / (wH * wH);
                float haloShape = exp(-oneMinusCos * invWH2);
                float haloPhase = pow(phase, _HaloPhasePower);

                sky += _HaloTint.rgb * (_HaloStrength * haloShape * haloPhase) * Tmoon * path * moonUp;
            }


            if (_GlareEnable > 0.5)
            {

            float wRad = radians(max(_GlareWidthDeg, 1e-3));
            float invSigma2 = 1.0 / max(wRad * wRad, 1e-6);

            // Broad gaussian in angle^2 using small-angle proxy: theta^2 ~ 2(1-cos)
            float glareShape = exp(-oneMinusCos * 1.0 * invSigma2);

            float glarePhase = pow(saturate(phase), _GlarePhasePower);

            // Keep it altitude-aware: moonUp already computed in your sky section
            float3 glare = _GlareTint.rgb * (_GlareStrength * glareShape * glarePhase) * moonUp;

            // Add to sky (preferred) so it affects area around moon
            sky += glare;
            }

            // Triangular dither in roughly [-0.5, +0.5]
            float n = Hash12(viewDir.xy * 16384.0) - 0.5;

            // Scale: start around 1/255 in linear space; adjust by eye
            float ditherAmp = 0.2 / 255.0;
            sky += n * ditherAmp;

            sky = max(sky, 0.0);
            // If the Moon is eclipsed, reduce its scattered/halo contributions too.
            // This is optional but usually looks more correct.
            float eclipseGlowMul = 1.0;
            if (_LunarEclipseEnable > 0.5)
            {
                // Penumbra dims a bit, umbra dims a lot
                float penMul = lerp(1.0, _LunarPenumbraMinLight, penMask);
                float umbMul = lerp(1.0, _LunarUmbraMinLight, umbMask);

                // Keep some glow even in umbra if you want; controlled by _LunarEclipseGlowDamp
                eclipseGlowMul = lerp(1.0, min(penMul, umbMul), _LunarEclipseGlowDamp);
            }

            // Apply glow multiplier to moon-sky / halo / glare output
            sky *= eclipseGlowMul;

            if (_StarDimEnable > 0.5)
            {

                float wRad_star = radians(max(_StarDimWidthDeg, 1e-3));
                float invSigma2_star = 1.0 / max(wRad_star * wRad_star, 1e-6);

                // Broad “near moon” factor (0 far, ~1 near)
                float nearMoon = exp(-(1.0 - cs) * invSigma2_star);

                // Full-moon emphasis
                float phaseGate = pow(saturate(phase), _StarDimPhasePower);

                float dim = _StarDimStrength * nearMoon * phaseGate * moonUp;

                // Convert to multiplicative factor, with a floor
                starMul = max(_StarDimFloor, 1.0 - dim);
            }

        }
    }

    // If not in disk, return sky only
    if (mask <= 0.0){
        o.addRGB = (sky);
        o.starMul = starMul;
        o.phaseFull = phase;
        o.moonUp = moonUp;
        o.mask = mask;
        return o;
        }

    // ---------- Disk shading ----------
    float z = sqrt(max(1.0 - r2, 0.0));
    float3 nLocal = normalize(float3(p.x, p.y, z));

    // Texture roll from lunar pole (texture only)
    float3 moonDirN = moonDir;
    float3 northWS = normalize(_LunarNorthPoleWS.xyz);
    float3 upPoleWS = northWS - moonDirN * dot(northWS, moonDirN);
    float upPoleLen2 = dot(upPoleWS, upPoleWS);
    float3 upPole = (upPoleLen2 > 1e-6) ? (upPoleWS * rsqrt(upPoleLen2)) : upP;

    float a = dot(upPole, rightP);
    float b = dot(upPole, upP);
    float roll = atan2(a, b);

    float s = sin(roll);
    float c = cos(roll);

    float2 pTex = float2(c * p.x - s * p.y,
                         s * p.x + c * p.y);
    float3 nTexLocal = normalize(float3(pTex.x, pTex.y, z));

    float3x3 Rbody = MoonBodyRotation();
    float3 nBody = normalize(mul(Rbody, nTexLocal));

    float2 uv = SphereToEquirectUV(nBody);
    uv.y = 1.0 - uv.y;
    uv.x = frac(uv.x);
    uv.y = saturate(uv.y);

    float3 albedo = tex2D(_MoonEquirect, uv).rgb;

    float3 nBumpedTexLocal = PerturbNormalFromNormalMap(uv, nTexLocal);

    // Rotate bumped normal back by -roll to projection local frame
    float3 nBumpedLocal = float3(
        c * nBumpedTexLocal.x + s * nBumpedTexLocal.y,
       -s * nBumpedTexLocal.x + c * nBumpedTexLocal.y,
        nBumpedTexLocal.z
    );
    nBumpedLocal = normalize(nBumpedLocal);

    float ndl = saturate(dot(nBumpedLocal, sunLocal));
    float lit = pow(ndl, max(_TerminatorPower, 1e-3));
    lit = max(lit, _Ambient);





    // -----------------------------
    // Apply eclipse to disk lighting
    // -----------------------------
    if (_LunarEclipseEnable > 0.5)
    {
        // Dimming: interpolate toward minimum light levels
        float penMul = lerp(1.0, _LunarPenumbraMinLight, penMask);
        float umbMul = lerp(1.0, _LunarUmbraMinLight, umbMask);

        float eclipseLitMul = min(penMul, umbMul);
        lit *= eclipseLitMul;

        // Umbra tint (reddening) — stronger deeper in umbra
        float umbT = pow(saturate(umbMask), _LunarUmbraTintPower) * _LunarUmbraTintStrength;
        albedo = lerp(albedo, albedo * _LunarUmbraTint.rgb, umbT);
    }
    
    float3 rgbMoon = albedo * lit * mask;

    // Optional disk extinction (if you’ve included these properties)
    // (Use worldUp as your atmosphere "up")

    rgbMoon *= lerp(1.0, TmoonDisk, moonUp);

    // Optional opposition boost
    if (_OppositionEnable > 0.5)
    {
        float opp = pow(saturate(phase), _OppositionPower);
        rgbMoon *= lerp(1.0, _OppositionBoost, opp);
    }
    // rgbMoon *= _MoonHDRBoost;
    o.addRGB = (sky+rgbMoon);
    o.starMul = starMul;
    o.phaseFull = phase;
    o.moonUp = moonUp;
    o.mask = mask;
    return o;
}

#endif
