Shader "Skybox/MoonOrbit_M5_DisplacedMeters_Stars"
{
    Properties
    {
        // =========================
        // Background / Stars
        // =========================
        _BgColor      ("Background Color (fallback)", Color) = (0.02, 0.02, 0.03, 1)

        _StarData ("Mag Data (R16)", 2D) = "white" {}
        _TempData ("Temp Data (R16)", 2D) = "white" {}
        _XoffData ("X Data (R16)", 2D) = "white" {}
        _YoffData ("Y Data (R16)", 2D) = "white" {}

        _PixelSize ("pixelscale", Float) = 1024
        _maxMag ("Mag Limit", Float) = 10
        _sigma ("gaussSigma", Float)  = 60
        _scaleFactor ("Mag shift (changes mag zero point)", Float)  = 0
        _brightnessScale ("LinearBrightnessScale", Float)  = 10

        _SkyboxTex ("Milky Way", CUBE) = "" {}
        _MWbright ("MW brightness Scale", Float) = 1
        _MWDesat ("MW desaturation", Range(0,1)) = 0.6

        _CelestialNorthWS ("Celestial North Pole WS", Vector) = (0,1,0,0)
        _RARollDeg        ("RA Roll about Pole (deg)", Range(-180,180)) = 0

        // Optional static MW rotation (keep if you want)
        _MWRotY ("MW Rot Y (deg)", Float) = 300
        _MWRotX ("MW Rot X (deg)", Float) = 171
        _MWRotZ ("MW Rot Z (deg)", Float) = 156

        // =========================
        // Moon displaced sphere (meters)
        // =========================
        _MoonCenterWS ("Moon Center WS (m)", Vector) = (0, 0, 4000000, 1)
        _MoonRadiusWS ("Moon Base Radius (m)", Float)  = 1727400

        _MoonYawDeg   ("Moon Yaw (deg, about +Z body)", Float) = 0
        _MoonPitchDeg ("Moon Pitch (deg, about +X body)", Float) = 0
        _MoonRollDeg  ("Moon Roll (deg, about +Y body)", Float) = 0

        _MoonAlbedo   ("Moon Albedo (equirect)", 2D) = "gray" {}
        _MoonNormal   ("Moon Normal (tangent space)", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 3)) = 1.0

        _MoonHeight ("Moon Height (U16, 0.5 m units)", 2D) = "gray" {}

        _ReliefScale ("Relief Exaggeration", Range(0, 10)) = 1.0
        _HeightRefMeters ("Height Reference (m)", Float) = 0.0
        _MaxReliefMeters ("Max Relief Bound (m)", Float) = 12000.0
        _HitIters ("Hit Solve Iterations", Range(4, 16)) = 10

        _HeightNormalStrength ("Height Normal Strength", Range(0, 5)) = 1.0
        _HeightNormalDuV ("Height Normal Sample DuV", Range(0.00005, 0.002)) = 0.0003

        _SunDirWS      ("Sun Direction WS", Vector) = (0, 1, 0, 0)
        _UseSceneSun   ("Use Scene Directional Light", Range(0,1)) = 1
        _FlipSceneSun  ("Flip Scene Sun Direction", Range(0,1)) = 0
        _SunIntensity  ("Sun Intensity", Range(0, 5)) = 1.0
        _Ambient       ("Ambient", Range(0, 0.3)) = 0.03
        _TermSoft      ("Terminator Softness", Range(0.0, 0.2)) = 0.03
        _Wrap          ("Diffuse Wrap", Range(0, 0.5)) = 0.12

        _SunShadowEnable   ("Sun Shadows (World)", Range(0,1)) = 1
        _SunShadowSteps    ("Sun Shadow Steps", Range(1,24)) = 8
        _SunShadowStrength ("Sun Shadow Strength", Range(0, 50)) = 12
        _SunShadowBiasMeters ("Sun Shadow Bias (m)", Range(0, 200)) = 10

        _SunShadowTermBand ("Shadow Terminator Band", Range(0.01, 0.5)) = 0.20

        _LimbAA       ("Limb AA Strength", Range(0.5, 30.0)) = 1.0

        // Debug
        _DebugDisplacement ("Debug: Displacement View", Range(0,1)) = 0




        _SunAngularRadiusDeg ("Sun Angular Radius (deg)", Range(0.05, 5.0)) = 0.2666
        _SunEdgeSoftnessPx   ("Sun Edge Softness (px)", Range(0.0, 6.0)) = 2.0

        _SunColor ("Sun Color", Color) = (1, 0.98, 0.92, 1)
        _SunDiskIntensity ("Sun Disk Intensity", Range(0, 200)) = 25.0

        _SunGlareEnable    ("Sun Glare Enable", Range(0,1)) = 1
        _SunGlareStrength  ("Sun Glare Strength", Range(0, 10)) = 2.0
        _SunGlareRadiusDeg ("Sun Glare Radius (deg)", Range(0.1, 10)) = 3.0
        _SunGlarePower     ("Sun Glare Power", Range(0.5, 8)) = 2.5

        _SunSpikeEnable    ("Sun Spikes Enable", Range(0,1)) = 1
        _SunSpikeStrength  ("Sun Spike Strength", Range(0, 5)) = 0.6
        _SunSpikeCount     ("Sun Spike Count", Range(2, 12)) = 4
        _SunSpikeSharpness ("Sun Spike Sharpness", Range(1, 64)) = 14
        _SunSpikeLengthR   ("Sun Spike Length (radii)", Range(1, 8)) = 3.0
        _SunSpikeFalloff   ("Sun Spike Falloff Power", Range(0.5, 6)) = 2.0



        // =========================
        // Earth (simple disk, equirect textures)
        // =========================
        _EarthDirWS          ("Earth Direction WS (to Earth)", Vector) = (0, 0, -1, 0)
        _EarthAngularRadiusDeg ("Earth Angular Radius (deg)", Range(0.05, 10.0)) = 1.0
        _EarthEdgeSoftnessPx ("Earth Edge Softness (px)", Range(0.0, 6.0)) = 2.0

        _EarthDayTex         ("Earth Day (equirect)", 2D) = "white" {}
        _EarthNightTex       ("Earth Night (equirect)", 2D) = "black" {}

        _EarthNorthWS        ("Earth North Pole WS", Vector) = (0, 1, 0, 0)
        _EarthRARollDeg      ("Earth Prime Meridian Roll (deg)", Range(-180,180)) = 0

        _EarthAlbedoTint     ("Earth Tint", Color) = (1,1,1,1)
        _EarthDayIntensity   ("Earth Day Intensity", Range(0, 10)) = 1.0
        _EarthNightIntensity ("Earth Night Intensity", Range(0, 10)) = 1.0

        _EarthTermSoftDeg    ("Earth Terminator Soft (deg)", Range(0.0, 10.0)) = 1.0
        _EarthWrap           ("Earth Diffuse Wrap", Range(0, 0.5)) = 0.08

        _EarthLimbDark       ("Earth Limb Darkening", Range(0, 2)) = 0.35
        _EarthSpecEnable     ("Earth Spec Enable", Range(0,1)) = 0
        _EarthSpecStrength   ("Earth Spec Strength", Range(0, 2)) = 0.2
        _EarthSpecPower      ("Earth Spec Power", Range(1, 256)) = 64

        _EarthAtmoEnable        ("Earth Atmo Enable", Range(0,1)) = 1
        _EarthAtmoColor         ("Earth Atmo Color", Color) = (0.35, 0.55, 1.0, 1)
        _EarthAtmoStrength      ("Earth Atmo Strength", Range(0, 5)) = 1.0
        _EarthAtmoRimPower      ("Earth Atmo Rim Power", Range(0.5, 12)) = 4.0
        _EarthAtmoWidth         ("Earth Atmo Width (radii)", Range(0.0, 0.5)) = 0.08

        _EarthAtmoSunBoost      ("Earth Atmo Sun Boost", Range(0, 5)) = 1.5
        _EarthAtmoSunPower      ("Earth Atmo Sun Power", Range(0.5, 12)) = 4.0

    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Back
        ZWrite Off
        ZTest LEqual   // IMPORTANT: don’t overdraw scene geometry

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // ========= Background / Stars =========
            fixed4 _BgColor;

            sampler2D _StarData, _TempData, _XoffData, _YoffData;
            float _PixelSize, _maxMag, _sigma, _scaleFactor, _brightnessScale;
            samplerCUBE _SkyboxTex;
            float _MWbright, _MWDesat;
            float4 _CelestialNorthWS;
            float _RARollDeg;
            float _MWRotY, _MWRotX, _MWRotZ;

            // ========= Moon =========
            float4 _MoonCenterWS;
            float  _MoonRadiusWS;

            float _MoonYawDeg, _MoonPitchDeg, _MoonRollDeg;

            sampler2D _MoonAlbedo;
            sampler2D _MoonNormal;
            float _NormalStrength;

            sampler2D _MoonHeight;
            float _ReliefScale, _HeightRefMeters, _MaxReliefMeters, _HitIters;
            float _HeightNormalStrength, _HeightNormalDuV;

            float4 _SunDirWS;
            float _UseSceneSun, _FlipSceneSun, _SunIntensity, _Ambient, _TermSoft, _Wrap;

            float _SunShadowEnable, _SunShadowSteps, _SunShadowStrength, _SunShadowBiasMeters, _SunShadowTermBand;

            float _LimbAA;
            float _DebugDisplacement;

            float4 _SunColor;
            float _SunAngularRadiusDeg;
            float _SunEdgeSoftnessPx;
            float _SunDiskIntensity;

            float _SunGlareEnable, _SunGlareStrength, _SunGlareRadiusDeg, _SunGlarePower;
            float _SunSpikeEnable, _SunSpikeStrength, _SunSpikeCount, _SunSpikeSharpness;
            float _SunSpikeLengthR, _SunSpikeFalloff;

            float4 _EarthDirWS;
            float  _EarthAngularRadiusDeg;
            float  _EarthEdgeSoftnessPx;

            sampler2D _EarthDayTex;
            sampler2D _EarthNightTex;

            float4 _EarthNorthWS;
            float  _EarthRARollDeg;

            float4 _EarthAlbedoTint;
            float  _EarthDayIntensity;
            float  _EarthNightIntensity;

            float  _EarthTermSoftDeg;
            float  _EarthWrap;

            float  _EarthLimbDark;
            float  _EarthSpecEnable;
            float  _EarthSpecStrength;
            float  _EarthSpecPower;

            float _EarthAtmoEnable;
            float4 _EarthAtmoColor;
            float _EarthAtmoStrength;
            float _EarthAtmoRimPower;
            float _EarthAtmoWidth;

            float _EarthAtmoSunBoost;
            float _EarthAtmoSunPower;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dirWS : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 dirOS = v.vertex.xyz;
                float3 dirWS = mul((float3x3)unity_ObjectToWorld, dirOS);
                o.dirWS = normalize(dirWS);
                return o;
            }

            // -------------------------
            // Utility: safe normalize
            // -------------------------
            float3 SafeNormalize(float3 v)
            {
                float len2 = dot(v,v);
                if (len2 < 1e-12) return float3(0,0,1);
                return v * rsqrt(len2);
            }

            float2 WrapU(float2 uv) { uv.x = frac(uv.x); return uv; }

            // Blend across u seam within 'seamWidth' (in UV units, e.g. 1/2048)
            float4 SampleEquirectSeam(sampler2D tex, float2 uv, float seamWidth)
            {
                uv = WrapU(uv);

                // distance to nearest seam edge (0 at seam, 0.5 at center)
                float d = min(uv.x, 1.0 - uv.x);

                // 0 away from seam, 1 near seam
                float t = saturate(1.0 - d / max(seamWidth, 1e-6));

                // Sample both sides
                float2 uvL = uv; uvL.x = uv.x + 1.0;   // wraps to same, but different filter footprint
                float2 uvR = uv; uvR.x = uv.x - 1.0;

                float4 a = tex2D(tex, uv);
                float4 b = tex2D(tex, uvL);
                float4 c = tex2D(tex, uvR);

                // Blend to the “other side” near seam (average b/c is robust)
                float4 other = 0.5 * (b + c);
                return lerp(a, other, t);
            }


            // -------------------------
            // Star functions (from your SkyMaster)
            // -------------------------
            float decodeMagnitude(float encodedValue)
            {
                float maxMag = -1.46;
                float minMag = _maxMag;
                if (encodedValue == 0) return 40;
                return (minMag + (minMag - maxMag) * (encodedValue * -1));
            }

            float magnitudeToBrightness(float magnitude)
            {
                return exp2((-magnitude / 2.5) * 3.32192809489); // log2(10)
            }

            float drawStar(float distanceArcsec, float sigma)
            {
                return exp(- (distanceArcsec*distanceArcsec) / (2.0 * sigma * sigma));
            }

            float3 RotateAroundAxisRodrigues(float3 v, float3 axisUnit, float angleRad)
            {
                float s = sin(angleRad);
                float c = cos(angleRad);
                return v * c + cross(axisUnit, v) * s + axisUnit * dot(axisUnit, v) * (1.0 - c);
            }

            float3 ApplyCelestialOrientation(float3 ndir, float3 northInput, float raRollDeg)
            {
                float3 N = SafeNormalize(northInput);

                float3 ref0 = float3(0, -1, 0); // preserves your RA=0 default
                float3 E0 = ref0 - N * dot(ref0, N);
                if (dot(E0,E0) < 1e-8)
                {
                    float3 ref1 = float3(1,0,0);
                    E0 = ref1 - N * dot(ref1, N);
                }
                E0 = SafeNormalize(E0);

                float rollRad = radians(raRollDeg);
                E0 = RotateAroundAxisRodrigues(E0, N, rollRad);

                float3 E90 = SafeNormalize(cross(N, E0));

                float3 Xaxis = E90;
                float3 Yaxis = -E0;
                float3 Zaxis = N;

                float3 ndir_tex;
                ndir_tex.x = dot(ndir, Xaxis);
                ndir_tex.y = dot(ndir, Yaxis);
                ndir_tex.z = dot(ndir, Zaxis);
                return SafeNormalize(ndir_tex);
            }

            void OctaBaseFromDir(float3 dir, out float3 dFlip, out float2 baseTexel, out float2 uvBase)
            {
                dFlip = float3(-dir.x, dir.y, dir.z);

                float sumAbs = abs(dFlip.x) + abs(dFlip.y) + abs(dFlip.z);
                float3 p = dFlip / sumAbs;

                float2 coord = (p.z >= 0.0)
                    ? p.xy
                    : float2(sign(p.x) * (1.0 - abs(p.y)),
                             sign(p.y) * (1.0 - abs(p.x)));

                float2 uvOct = coord * 0.5 + 0.5;
                uvBase = uvOct;

                float2 pixelSpace = uvOct * _PixelSize;
                baseTexel = floor(pixelSpace);
            }

            float4 RetreivePixInfo(float3 ndir, float3 dFlip, float2 baseTexel, float2 pixelOff)
            {
                float2 pixelCenter = baseTexel + 0.5 + pixelOff;
                float2 uvCenter = pixelCenter / _PixelSize;

                float starData = tex2D(_StarData, uvCenter).r;
                half3 tempR    = tex2D(_TempData, uvCenter).rgb;
                float XData    = tex2D(_XoffData, uvCenter).r;
                float YData    = tex2D(_YoffData, uvCenter).r;

                float mag = decodeMagnitude(starData) - _scaleFactor;
                float starBrightness = magnitudeToBrightness(mag) * _brightnessScale;

                // Reconstruct direction of encoded star from UV + subpixel offsets (your method)
                float2 coord1;
                coord1.x = (uvCenter.x * 2.0 - 1.0) + (-((((YData-0.25)*2)*3)-1.5)/(_PixelSize));
                coord1.y = (uvCenter.y * 2.0 - 1.0) + ( ((((XData-0.25)*2)*3)-1.5)/(_PixelSize));

                float3 Pprime1;
                if (abs(coord1.x) + abs(coord1.y) <= 1.0)
                {
                    Pprime1.xy = coord1;
                    Pprime1.z = 1.0 - abs(coord1.x) - abs(coord1.y);
                }
                else
                {
                    Pprime1.x = sign(coord1.x) * (1.0 - abs(coord1.y));
                    Pprime1.y = sign(coord1.y) * (1.0 - abs(coord1.x));
                    Pprime1.z = -(1.0 - abs(Pprime1.x) - abs(Pprime1.y));
                }

                float3 pDir    = SafeNormalize(Pprime1);
                float3 baseDir = SafeNormalize(dFlip);

                // Angular distance proxy used by your gaussian (arcsec-ish scaling)
                float vecDist = length(pDir - baseDir) * 206265.0;

                float intensity = drawStar(vecDist, _sigma);

                return float4(tempR, 1.0) * (starBrightness * intensity);
            }

            float4x4 RotationMatrix(float yDeg, float xDeg, float zDeg)
            {
                float x = radians(xDeg), y = radians(yDeg), z = radians(zDeg);

                float sinX = sin(x), cosX = cos(x);
                float sinY = sin(y), cosY = cos(y);
                float sinZ = sin(z), cosZ = cos(z);

                return float4x4(
                    cosY * cosZ, cosZ * sinX * sinY - cosX * sinZ, cosX * cosZ * sinY + sinX * sinZ, 0,
                    cosY * sinZ, cosX * cosZ + sinX * sinY * sinZ, -cosZ * sinX + cosX * sinY * sinZ, 0,
                    -sinY,      cosY * sinX,                      cosX * cosY,                      0,
                    0,          0,                                0,                                1
                );
            }

            fixed3 Desaturate3(fixed3 rgb, float amt)
            {
                float gray = dot(rgb, fixed3(0.299, 0.587, 0.114));
                return lerp(rgb, gray.xxx, amt);
            }

            // -------------------------
            // Moon helpers
            // -------------------------
            float3x3 RotX(float a){ float s=sin(a), c=cos(a); return float3x3(1,0,0, 0,c,-s, 0,s,c); }
            float3x3 RotY(float a){ float s=sin(a), c=cos(a); return float3x3(c,0,s, 0,1,0, -s,0,c); }
            float3x3 RotZ(float a){ float s=sin(a), c=cos(a); return float3x3(c,-s,0, s,c,0, 0,0,1); }

            void BuildTBN_FromBodyNormal(float3 N_body, float3x3 bodyToWorld, out float3 T_ws, out float3 B_ws)
            {
                float3 up_body = (abs(N_body.z) < 0.999) ? float3(0,0,1) : float3(0,1,0);
                float3 T_body = normalize(cross(up_body, N_body));
                float3 B_body = cross(N_body, T_body);
                T_ws = mul(bodyToWorld, T_body);
                B_ws = mul(bodyToWorld, B_body);
            }

            float HeightMeters_FromU16(float height01)
            {
                float h16 = height01 * 65535.0;
                return 0.5 * h16;
            }

            float DisplacementMeters(float2 uv)
            {
                float h01 = tex2D(_MoonHeight, uv).r;
                float h_m = HeightMeters_FromU16(h01);
                return (h_m - _HeightRefMeters) * _ReliefScale;
            }

            float2 BodyDirToUV(float3 N_body)
            {
                float lon = atan2(N_body.y, N_body.x);
                float lat = asin(clamp(N_body.z, -1.0, 1.0));
                float u = lon * (1.0 / (2.0 * UNITY_PI)) + 0.5;
                float v = lat * (1.0 / UNITY_PI) + 0.5;
                return float2(u, v);
            }

            bool RaySphereNearFar(float3 O, float3 D, float3 C, float R, out float tNear, out float tFar)
            {
                float3 oc = O - C;
                float b = dot(oc, D);
                float c = dot(oc, oc) - R * R;
                float h = b*b - c;
                if (h < 0.0) { tNear = 0.0; tFar = 0.0; return false; }
                float s = sqrt(h);
                tNear = -b - s;
                tFar  = -b + s;
                return true;
            }

            float F_alongRay(float t, float3 O, float3 D, float3 C, float Rbase, float3x3 worldToBody)
            {
                float3 P = O + t * D;
                float3 N_ws = normalize(P - C);
                float3 N_body = mul(worldToBody, N_ws);
                float2 uv = BodyDirToUV(N_body);
                uv.x = frac(uv.x);
                uv.y = saturate(uv.y);

                float disp_m = DisplacementMeters(uv);
                return length(P - C) - (Rbase + disp_m);
            }
            float3 GetSunDirToSunWS()
            {
                // "To sun" on the sky
                float3 toSun = SafeNormalize(_SunDirWS.xyz);

                if (_UseSceneSun > 0.5 && abs(_WorldSpaceLightPos0.w) < 0.5)
                {
                    // For directional lights, _WorldSpaceLightPos0.xyz is the light direction (commonly "from light to scene")
                    // So "to sun" is usually the opposite.
                    float3 sceneLightDir = SafeNormalize(_WorldSpaceLightPos0.xyz);
                    if (_FlipSceneSun > 0.5) sceneLightDir = -sceneLightDir;

                    toSun = SafeNormalize(-sceneLightDir);
                }
                else
                {
                    if (_FlipSceneSun > 0.5) toSun = -toSun;
                }

                return toSun;
            }

            float SunShadow_World(
                float3 P, float3 N_ws, float3 L,
                float3 C, float Rbase,
                float3x3 worldToBody,
                float maxReliefMeters,
                float stepsF,
                float strength,
                float biasMeters,
                float sunHem)
            {
                if (sunHem <= 0.0) return 0.0;

                int steps = (int)clamp(stepsF, 1.0, 24.0);

                float3 P0 = P + N_ws * biasMeters;

                float nd = saturate(dot(N_ws, L));
                float grazing = max(1e-3, nd);
                float tMax = (maxReliefMeters * 8.0) / grazing;

                float dt = tMax / steps;

                float occ = 0.0;
                float t = dt;

                [unroll]
                for (int k = 0; k < 24; k++)
                {
                    if (k >= steps) break;

                    float3 Q = P0 + L * t;

                    float3 Nq_ws = normalize(Q - C);
                    float3 Nq_body = mul(worldToBody, Nq_ws);
                    float2 uvq = BodyDirToUV(Nq_body);
                    uvq.x = frac(uvq.x);
                    uvq.y = saturate(uvq.y);

                    float disp_q = DisplacementMeters(uvq);
                    float rq = length(Q - C);
                    float surfaceR = Rbase + disp_q;

                    float pen = surfaceR - rq;
                    occ = max(occ, pen);

                    t += dt;
                }

                float occ01 = saturate(occ / 50.0);
                float shadow = exp(-strength * occ01);
                return saturate(shadow);
            }


            float3 RenderSunPerceptual(float3 viewDir, float3 sunDirToSunWS)
            {
                float3 V = normalize(viewDir);
                float3 S = normalize(sunDirToSunWS);

                float cosA = saturate(dot(V, S));                 // 1 at sun center
                float angRad = radians(_SunAngularRadiusDeg);
                float cosR = cos(angRad);

                // Derivative-based AA around the edge in "cos space"
                float w = fwidth(cosA) * max(0.5, _SunEdgeSoftnessPx);

                // Disk mask (1 inside, 0 outside)
                float disk = smoothstep(cosR - w, cosR + w, cosA);

                // Sun disk radiance (keep unclamped until final)
                float3 diskRGB = _SunColor.rgb * (_SunDiskIntensity * disk);

                // Angular distance in degrees (cheap-ish; fine for a single sun)
                float theta = acos(cosA);
                float thetaDeg = degrees(theta);

                // Glare / veiling halo (perceptual)
                float glare = 0.0;
                if (_SunGlareEnable > 0.5)
                {
                    float t = saturate(1.0 - thetaDeg / max(1e-3, _SunGlareRadiusDeg));
                    glare = _SunGlareStrength * pow(t, _SunGlarePower);
                }

                // Diffraction / eye spikes (optional)
                float spikes = 0.0;
                if (_SunSpikeEnable > 0.5)
                {
                    // Build orthonormal basis around S
                    float3 up = (abs(S.y) < 0.99) ? float3(0,1,0) : float3(1,0,0);
                    float3 U = normalize(cross(up, S));
                    float3 W = cross(S, U);

                    // Azimuth around sun direction
                    float x = dot(V, U);
                    float y = dot(V, W);
                    float az = atan2(y, x);

                    // radial distance in "sun radii"
                    float rRadii = theta / max(1e-6, angRad);

                    // Spikes fade out beyond some length
                    float radial = saturate(1.0 - (rRadii - 1.0) / max(1e-3, (_SunSpikeLengthR - 1.0)));
                    radial = pow(radial, _SunSpikeFalloff);

                    // N-spike pattern
                    float n = max(2.0, _SunSpikeCount);
                    float pat = abs(cos(az * 0.5 * n));                // spikes
                    pat = pow(pat, _SunSpikeSharpness);

                    spikes = _SunSpikeStrength * pat * radial;

                    // Don’t double-count inside disk; spikes should “emanate” from disk edge outward
                    spikes *= (1.0 - disk);
                }

                float3 haloRGB = _SunColor.rgb * (glare + spikes);

                return diskRGB + haloRGB;
            }

            float2 DirToEquirectUV(float3 dirUnit)
            {
                // dirUnit in the planet's texture frame:
                // x = lon=0 at +X, y=lon=90 at +Y, z=+north
                float lon = atan2(dirUnit.y, dirUnit.x); // -pi..pi
                float lat = asin(clamp(dirUnit.z, -1.0, 1.0));
                float u = lon * (1.0 / (2.0 * UNITY_PI)) + 0.5;
                float v = lat * (1.0 / UNITY_PI) + 0.5;
                return float2(frac(u), saturate(v));
            }

            float3 ApplyAxisAndRoll(float3 vWS, float3 northWS, float rollDeg)
            {
                // Build a right-handed basis:
                // Z = north
                // X = "prime meridian direction" on equator (rolled)
                // Y = east
                float3 N = SafeNormalize(northWS);

                // pick a reference to define lon=0
                float3 ref0 = float3(0, 0, 1);
                float3 X0 = ref0 - N * dot(ref0, N);
                if (dot(X0, X0) < 1e-8)
                {
                    ref0 = float3(1, 0, 0);
                    X0 = ref0 - N * dot(ref0, N);
                }
                X0 = SafeNormalize(X0);

                // Apply roll about N
                float rollRad = radians(rollDeg);
                float3 X = RotateAroundAxisRodrigues(X0, N, rollRad);

                float3 Y = SafeNormalize(cross(N, X)); // eastward
                // Now convert vWS into this frame
                float3 v;
                v.x = dot(vWS, X);
                v.y = dot(vWS, Y);
                v.z = dot(vWS, N);
                return SafeNormalize(v);
            }

            // --- REPLACE your SkyDiskPointOnSphere with this version ---
            bool SkyDiskPointOnSphere(
                float3 viewDirWS, float3 toCenterWS, float angRadiusDeg,
                out float3 n_surfWS, out float diskMask, out float r2)
            {
                float3 V = SafeNormalize(viewDirWS);

                float3 C = SafeNormalize(toCenterWS);

                float cosA = dot(V, C); // 1 at center
                float angRad = radians(angRadiusDeg);
                float cosR = cos(angRad);

                // Defaults for a miss (IMPORTANT)
                n_surfWS = C;
                diskMask = 0.0;
                r2 = 2.0; // sentinel: safely outside disk+atmo ring

                // Edge AA (in cos-space)
                float wAA = fwidth(cosA) * max(0.5, _EarthEdgeSoftnessPx);
                diskMask = smoothstep(cosR - wAA, cosR + wAA, cosA);

                // Fully outside even AA -> miss, keep sentinel r2
                if (diskMask <= 0.0)
                    return false;

                // Build local basis around C
                float3 up = (abs(C.y) < 0.99) ? float3(0,1,0) : float3(1,0,0);
                float3 U = SafeNormalize(cross(up, C));
                float3 W = cross(C, U);

                float x = dot(V, U);
                float y = dot(V, W);

                float sinR = sin(angRad);
                float2 p = float2(x, y) / max(1e-6, sinR);

                r2 = dot(p, p);

                // Outside disk (AA may still be >0): keep normal as C
                if (r2 > 1.0)
                {
                    n_surfWS = C;
                    return true;
                }

                float zSurf = sqrt(max(0.0, 1.0 - r2));
                float3 nLocal = float3(p.x, p.y, zSurf);

                n_surfWS = SafeNormalize(nLocal.x * U + nLocal.y * W + nLocal.z * C);
                return true;
            }


            // --- REPLACE your RenderEarthSimple with this version ---
            // Key change: returns UNMASKED rgb, and provides mask/r2 as outputs so we blend once.
            float3 RenderEarthSimple(
                float3 viewDirWS, float3 toEarthWS, float3 toSunWS,
                out float earthMask, out float earthR2)
            {
                float3 N_ws;
                earthMask = 0.0;
                earthR2 = 2.0;

                if (!SkyDiskPointOnSphere(viewDirWS, toEarthWS, _EarthAngularRadiusDeg, N_ws, earthMask, earthR2))
                    return float3(0,0,0);

                // Convert surface normal into Earth texture frame
                float3 northWS = SafeNormalize(_EarthNorthWS.xyz);
                float3 nEarthFrame = ApplyAxisAndRoll(N_ws, northWS, _EarthRARollDeg);
                float2 uv = DirToEquirectUV(nEarthFrame);
                // seamWidth ~ 1 texel. If your earth map is 4096 wide: 1/4096.
                // Expose as a property if you want.
                float seamWidth = 1.0 / 1024.0;

                float3 day   = SampleEquirectSeam(_EarthDayTex,   uv, seamWidth).rgb * _EarthDayIntensity;
                float3 night = SampleEquirectSeam(_EarthNightTex, uv, seamWidth).rgb * _EarthNightIntensity;

                float3 L = SafeNormalize(float3(toSunWS.x,toSunWS.y,-toSunWS.z));
                float ndl = dot(N_ws, L);
                float ndWrap = saturate((ndl + _EarthWrap) / (1.0 + _EarthWrap));

                // Terminator softness
                float soft = sin(radians(max(0.001, _EarthTermSoftDeg)));
                float lit = smoothstep(-soft, soft, ndl) * ndWrap;

                float3 rgb = day * lit + night * (1.0 - lit);

                // Limb darkening
                float muC = saturate(dot(N_ws, SafeNormalize(toEarthWS)));
                float limb = lerp(1.0 - _EarthLimbDark, 1.0, muC);
                rgb *= limb;

                // Optional spec
                if (_EarthSpecEnable > 0.5)
                {
                    float3 V = SafeNormalize(-viewDirWS);
                    float3 H = SafeNormalize(V + L);
                    float spec = pow(saturate(dot(N_ws, H)), _EarthSpecPower) * _EarthSpecStrength;
                    rgb += spec.xxx;
                }

                // Atmosphere
                if (_EarthAtmoEnable > 0.5)
                {
                    float3 C = SafeNormalize(toEarthWS);

                    // Day/night gating for atmosphere (0 on night side, 1 on day side, soft terminator)
                    float ndl_atmo = dot(N_ws, L);
                    float soft_atmo = sin(radians(max(0.001, _EarthTermSoftDeg)));
                    float day01 = smoothstep(-soft_atmo, soft_atmo, ndl_atmo);

                    // Optional: keep a tiny floor so nightside isn't totally gone
                    // (set to 0.0 if you want hard physical "no glow" on nightside)
                    float nightFloor = 0.13;
                    day01 = max(day01, nightFloor);

                    float mu = saturate(dot(N_ws, C));
                    float rim = pow(saturate(1.0 - mu), _EarthAtmoRimPower);

                    float r = sqrt(max(earthR2, 0.0)); // r=1 at limb
                    float w = max(1e-4, _EarthAtmoWidth);
                    float ring = saturate(1.0 - (r - 1.0) / w);
                    ring = ring * ring;

                    float sunAlign = saturate(dot(C, L));
                    float sunBoost = _EarthAtmoSunBoost * pow(sunAlign, _EarthAtmoSunPower);

                    float atmo = _EarthAtmoStrength * rim * (1.0 + sunBoost)* day01;
                    float3 hazeRGB = _EarthAtmoColor.rgb * atmo;

                    // On-disk rim + slight off-disk ring
                    rgb += hazeRGB * (0.35 + 0.65 * earthMask);
                    rgb += hazeRGB * ring;
                }

                rgb *= _EarthAlbedoTint.rgb;

                return rgb; // <-- UNMASKED
            }


            fixed4 frag (v2f i) : SV_Target
            {
                float3 O = _WorldSpaceCameraPos;
                float3 D = normalize(i.dirWS);

                // =========================
                // 1) Star + Milky Way background (in “sky direction space”)
                // =========================
                float3 northOS = normalize(mul((float3x3)unity_WorldToObject, _CelestialNorthWS.xyz));
                float3 ndir = ApplyCelestialOrientation(D, northOS, _RARollDeg);

                float3 dFlip;
                float2 baseTexel, uvBase;
                OctaBaseFromDir(ndir, dFlip, baseTexel, uvBase);

                float4 starSum = 0;
                starSum += RetreivePixInfo(ndir, dFlip, baseTexel, float2( 0, 0));
                starSum += RetreivePixInfo(ndir, dFlip, baseTexel, float2( 1, 0));
                starSum += RetreivePixInfo(ndir, dFlip, baseTexel, float2( 1, 1));
                starSum += RetreivePixInfo(ndir, dFlip, baseTexel, float2( 0, 1));
                starSum += RetreivePixInfo(ndir, dFlip, baseTexel, float2(-1, 1));
                starSum += RetreivePixInfo(ndir, dFlip, baseTexel, float2(-1, 0));
                starSum += RetreivePixInfo(ndir, dFlip, baseTexel, float2(-1,-1));
                starSum += RetreivePixInfo(ndir, dFlip, baseTexel, float2( 0,-1));
                starSum += RetreivePixInfo(ndir, dFlip, baseTexel, float2( 1,-1));

                float4x4 mwRot = RotationMatrix(_MWRotY, _MWRotX, _MWRotZ);
                float3 mwDir = mul(mwRot, float4(ndir, 1.0)).xyz;

                half3 mw = texCUBE(_SkyboxTex, mwDir).rgb * _MWbright;
                mw = Desaturate3(mw, _MWDesat);

                half3 bg = mw + starSum.rgb;

                // Fallback background tint (optional)
                bg = max(bg, _BgColor.rgb);

                // =========================
                // 1.5) Sun on top of stars (must happen BEFORE any moon early-returns)
                // =========================
                float3 toSun = GetSunDirToSunWS();
                float3 sunRGB = RenderSunPerceptual(D, toSun);

                // Use float/half here to avoid fixed clamping hiding energy
                bg = (fixed3)((float3)bg + sunRGB);


                // Earth direction is ALSO "to Earth"
                float3 toEarth = SafeNormalize(_EarthDirWS.xyz);

                float earthMask, earthR2;
                half3 earthRGB = (half3)RenderEarthSimple(D, toEarth, toSun, earthMask, earthR2);

                // Earth occludes stars/sun behind it (apply mask ONCE)
                bg = lerp(bg, earthRGB, earthMask);

                // =========================
                // 2) Moon intersection (displaced)
                // =========================
                float3 C = _MoonCenterWS.xyz;
                float  Rbase = max(_MoonRadiusWS, 1e-3);

                float yaw   = radians(_MoonYawDeg);
                float pitch = radians(_MoonPitchDeg);
                float roll  = radians(_MoonRollDeg);
                float3x3 bodyToWorld = mul(RotY(roll), mul(RotX(pitch), RotZ(yaw)));
                float3x3 worldToBody = transpose(bodyToWorld);

                float Rbound = Rbase + max(_MaxReliefMeters * _ReliefScale, 0.0);
                float tNear, tFar;
                if (!RaySphereNearFar(O, D, C, Rbound, tNear, tFar))
                {
                    return fixed4(bg, 1.0);
                }
                if (tFar <= 0.0) return fixed4(bg, 1.0);
                if (tNear < 0.0) tNear = 0.0;

                // =========================
                // 3) Coverage AA from displaced surface function at closest approach (your proven fix)
                // =========================
                float3 oc = O - C;
                float b = dot(oc, D);
                float tC = clamp(-b, tNear, tFar);

                float Fc = F_alongRay(tC, O, D, C, Rbase, worldToBody);
                float w = fwidth(Fc) * _LimbAA;
                float coverage = saturate(0.5 - Fc / max(w, 1e-6));

                if (coverage <= 0.0)
                    return fixed4(bg, 1.0);

                // =========================
                // 4) Robust bracket scan + bisection (so limb depth matches terrain)
                // =========================
                float tHit = tNear;

                float fA = F_alongRay(tNear, O, D, C, Rbase, worldToBody);
                float fB = F_alongRay(tFar,  O, D, C, Rbase, worldToBody);

                if (fA * fB > 0.0)
                {
                    const int SCAN_STEPS = 10;
                    float tPrev = tNear;
                    float fPrev = fA;

                    bool found = false;
                    float tLo = tNear, fLo = fA;
                    float tHi = tFar,  fHi = fB;

                    [unroll]
                    for (int s = 1; s <= SCAN_STEPS; s++)
                    {
                        float u = (float)s / (float)SCAN_STEPS;
                        float tCur = lerp(tNear, tFar, u);
                        float fCur = F_alongRay(tCur, O, D, C, Rbase, worldToBody);

                        if (fPrev * fCur <= 0.0)
                        {
                            tLo = tPrev; fLo = fPrev;
                            tHi = tCur;  fHi = fCur;
                            found = true;
                            break;
                        }

                        tPrev = tCur;
                        fPrev = fCur;
                    }

                    if (found)
                    {
                        int iters = (int)clamp(_HitIters, 4.0, 16.0);
                        float a = tLo, fa = fLo;
                        float bT = tHi, fb2 = fHi;

                        [unroll]
                        for (int k = 0; k < 16; k++)
                        {
                            if (k >= iters) break;

                            float m = 0.5 * (a + bT);
                            float fm = F_alongRay(m, O, D, C, Rbase, worldToBody);

                            if (fa * fm <= 0.0) { bT = m; fb2 = fm; }
                            else                { a  = m; fa  = fm; }
                        }
                        tHit = 0.5 * (a + bT);
                    }
                    else
                    {
                        // No root found: keep safe (should be rare if coverage > 0)
                        tHit = tC;
                    }
                }
                else
                {
                    int iters = (int)clamp(_HitIters, 4.0, 16.0);
                    float a = tNear, fa = fA;
                    float bT = tFar,  fb2 = fB;

                    [unroll]
                    for (int k = 0; k < 16; k++)
                    {
                        if (k >= iters) break;

                        float m = 0.5 * (a + bT);
                        float fm = F_alongRay(m, O, D, C, Rbase, worldToBody);

                        if (fa * fm <= 0.0) { bT = m; fb2 = fm; }
                        else                { a  = m; fa  = fm; }
                    }
                    tHit = 0.5 * (a + bT);
                }

                float3 P = O + tHit * D;
                float3 N_ws = normalize(P - C);
                float3 N_body = mul(worldToBody, N_ws);

                float2 uv = BodyDirToUV(N_body);
                uv.x = frac(uv.x);
                uv.y = saturate(uv.y);

                if (_DebugDisplacement > 0.5)
                {
                    float h01 = tex2D(_MoonHeight, uv).r;
                    float h_m = HeightMeters_FromU16(h01);
                    float disp_m = (h_m - _HeightRefMeters) * _ReliefScale;
                    float vis = saturate(0.5 + disp_m / 3000.0);
                    fixed3 moonDbg = vis.xxx;
                    fixed3 outDbg = lerp(bg, moonDbg, coverage);
                    return fixed4(outDbg, 1.0);
                }

                // =========================
                // 5) Moon normals (macro slope + micro normal map)
                // =========================
                float3 T_ws, B_ws;
                BuildTBN_FromBodyNormal(N_body, bodyToWorld, T_ws, B_ws);

                float duv = _HeightNormalDuV;
                float2 uv_u1 = float2(frac(uv.x + duv), uv.y);
                float2 uv_u0 = float2(frac(uv.x - duv), uv.y);
                float2 uv_v1 = float2(uv.x, saturate(uv.y + duv));
                float2 uv_v0 = float2(uv.x, saturate(uv.y - duv));

                float h_u1 = HeightMeters_FromU16(tex2D(_MoonHeight, uv_u1).r);
                float h_u0 = HeightMeters_FromU16(tex2D(_MoonHeight, uv_u0).r);
                float h_v1 = HeightMeters_FromU16(tex2D(_MoonHeight, uv_v1).r);
                float h_v0 = HeightMeters_FromU16(tex2D(_MoonHeight, uv_v0).r);

                h_u1 = (h_u1 - _HeightRefMeters) * _ReliefScale;
                h_u0 = (h_u0 - _HeightRefMeters) * _ReliefScale;
                h_v1 = (h_v1 - _HeightRefMeters) * _ReliefScale;
                h_v0 = (h_v0 - _HeightRefMeters) * _ReliefScale;

                float dhdu = (h_u1 - h_u0) / max(2.0 * duv, 1e-6);
                float dhdv = (h_v1 - h_v0) / max(2.0 * duv, 1e-6);

                float invR = 1.0 / max(length(P - C), 1.0);
                float3 N_height = normalize(N_ws - _HeightNormalStrength * invR * (dhdu * T_ws + dhdv * B_ws));

                float3 n_ts = UnpackNormal(tex2D(_MoonNormal, uv));
                n_ts.xy *= _NormalStrength;
                n_ts = normalize(n_ts);

                float3 N_final = normalize(n_ts.x * T_ws + n_ts.y * B_ws + n_ts.z * N_height);

                // =========================
                // 6) Lighting (correct hemisphere gating)
                // =========================
                // float3 L = normalize(_SunDirWS.xyz);
                // if (_UseSceneSun > 0.5)
                // {
                //     float3 sceneL = _WorldSpaceLightPos0.xyz;
                //     if (_FlipSceneSun > 0.5) sceneL = -sceneL;
                //     if (abs(_WorldSpaceLightPos0.w) < 0.5)
                //         L = normalize(sceneL);
                // }

                float sunHem = dot(N_ws, toSun);
                float dayMask = step(0.0, sunHem);

                float sunHemWrap = saturate((sunHem + _Wrap) / (1.0 + _Wrap));
                float litGlobal = smoothstep(0.0, _TermSoft, sunHemWrap);

                float ndotl = dot(N_final, toSun);
                float ndWrap = saturate((ndotl + _Wrap) / (1.0 + _Wrap));
                float litLocal = smoothstep(0.0, _TermSoft, ndWrap);

                float lit = litGlobal * litLocal * dayMask;

                float sunShadow = 1.0;
                if (_SunShadowEnable > 0.5 && dayMask > 0.0)
                {
                    sunShadow = SunShadow_World(
                        P, N_ws, toSun,
                        C, Rbase,
                        worldToBody,
                        _MaxReliefMeters * _ReliefScale,
                        _SunShadowSteps,
                        _SunShadowStrength,
                        _SunShadowBiasMeters,
                        sunHem
                    );
                }

                float lightFactor = _Ambient + (_SunIntensity * lit * sunShadow);
                lightFactor = saturate(lightFactor);

                fixed3 albedo = tex2D(_MoonAlbedo, uv).rgb;
                fixed3 moonCol = albedo * lightFactor;

                // =========================
                // 7) Composite: stars first, then Moon over (terrain occludes stars)
                // =========================
                #if defined(UNITY_COLORSPACE_GAMMA)
                    bg      = GammaToLinearSpace(bg);
                    moonCol = GammaToLinearSpace(moonCol);
                #endif

                fixed3 outCol = lerp(bg, moonCol, coverage);

                #if defined(UNITY_COLORSPACE_GAMMA)
                    outCol = LinearToGammaSpace(outCol);
                #endif

                return fixed4(outCol, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
