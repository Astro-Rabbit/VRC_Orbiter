Shader "Skybox/SkySunMoonEarth"
{
    Properties
    {
        _StarData ("Mag Data (R16)", 2D) = "white" {}
        _TempData ("Temp Data (R16)", 2D) = "white" {}
        _XoffData ("X Data (R16)", 2D) = "white" {}
        _YoffData ("Y Data (R16)", 2D) = "white" {}

        _PixelSize ("pixelscale", float) = 1024
        _maxMag ("Mag Limit", float) = 10

        _sigma ("gaussSigma", float) = 60
        _scaleFactor ("Mag shift", float) = 0
        _brightnessScale ("LinearBrightnessScale", float) = 10

        _SkyboxTex ("Milky Way", CUBE) = "" {}
        _MWbright ("MW brightness Scale", float) = 1

        // Quaternion (x,y,z,w): BODY -> EQUATORIAL
        _CraftBodyToEq ("Craft BodyToEq quat (xyzw)", Vector) = (0,0,0,1)

        // Obliquity epsilon in degrees (equatorial -> ecliptic)
        _ObliquityDeg ("Obliquity (deg)", Float) = 23.439281

        // --- Sun ---
        _SunDirEcl   ("Sun Dir (ECL, unit)", Vector) = (1,0,0,0)
        _SunAngRad   ("Sun Angular Radius (rad)", Float) = 0.00463
        _SunColor    ("Sun Color (HDR)", Color) = (8,8,7.5,1)
        _SunEdgeSoft ("Sun Edge Softness", Float) = 0.00015

        _SunIntensity      ("Sun Intensity (HDR scalar)", Float) = 25.0
        _SunCoreFalloff    ("Sun Core Falloff", Float) = 2.2
        _SunGlowIntensity  ("Sun Glow Intensity", Float) = 2.0
        _SunGlowRadiusMul  ("Sun Glow Radius Multiplier", Float) = 3.0
        _SunGlowFalloff    ("Sun Glow Falloff", Float) = 3.5

        // --- Moon (primary body in this shader) ---
        _MoonPosEcl   ("Moon Pos (craft->moon, ECL, meters)", Vector) = (0,0,0,0)
        _MoonRadiusM  ("Moon Radius (m)", Float) = 1737400.0
        _MoonAmbient  ("Moon Ambient", Range(0,1)) = 0.02
        _MoonShadowPow("Moon Terminator Power", Float) = 1.0

        _MoonAlbedo ("Moon Albedo (equirect)", 2D) = "gray" {}
        _MoonBodyToEcl ("Moon BodyToEcl quat (xyzw)", Vector) = (0,0,0,1)
        _MoonAlbedoTint ("Moon Albedo Tint", Color) = (1,1,1,1)
        _MoonGamma ("Moon Albedo Gamma", Float) = 1.0

        _MoonLonOffsetDeg ("Moon Lon Offset (deg)", Float) = 180
        _MoonFlipU ("Moon Flip U (0/1)", Float) = 0
        _MoonFlipV ("Moon Flip V (0/1)", Float) = 1

        // --- Earth (secondary body; distant-view optimized) ---
        _EarthPosEcl   ("Earth Pos (craft->earth, ECL, meters)", Vector) = (0,0,0,0)
        _EarthRadiusM  ("Earth Radius (m)", Float) = 6371000.0
        _EarthScatterHeightM ("Earth Scatter Height (m)", Float) = 100000.0
        _EarthAmbient  ("Earth Ambient", Range(0,1)) = 0.03
        _EarthShadowPow("Earth Terminator Power", Float) = 1.0

        _RayleighAmount ("Rayleigh Scattering Coefficients", Vector) = (0.0000055, 0.000013, 0.0000224, 0)
        _RayleighScale ("Rayleigh Scattering Scale Height", Float) = 8000
        _MieAmount ("Mie Scattering Coefficient", Float) = 0.000021
        _MieScale ("Mie Scattering Scale Height", Float) = 1200
        _MieG ("Mie Scattering Direction Coefficient", Float) = -0.78

        _EarthAirglowColor ("Earth Airglow Color", Color) = (0.10, 0.22, 0.18, 1)
        _EarthAirglowIntensity ("Earth Airglow Intensity", Float) = 0.08
        _EarthAirglowHeightM ("Earth Airglow Height (m)", Float) = 95000.0
        _EarthAirglowThicknessM ("Earth Airglow Thickness (m)", Float) = 30000.0
        _EarthAirglowNightBias ("Earth Airglow Night Bias", Float) = 1.0

        _EarthAlbedo ("Earth Albedo (equirect)", 2D) = "white" {}
        _EarthMask ("Earth Mask (A=land/water, RGB=night lights)", 2D) = "black" {}
        _EarthClouds ("Earth Clouds (RGB/A)", 2D) = "white" {}

        _EarthBodyToEcl ("Earth BodyToEcl quat (xyzw)", Vector) = (0,0,0,1)
        _EarthAlbedoTint ("Earth Albedo Tint", Color) = (1,1,1,1)
        _EarthGamma ("Earth Albedo Gamma", Float) = 1.0

        _EarthLonOffsetDeg ("Earth Lon Offset (deg)", Float) = 180
        _EarthFlipU ("Earth Flip U (0/1)", Float) = 0
        _EarthFlipV ("Earth Flip V (0/1)", Float) = 1

        _EarthWaterSpecIntensity ("Earth Water Spec Intensity", Float) = 2.4
        _EarthWaterFresnel ("Earth Water Fresnel Strength", Float) = 1.0
        _EarthNightLightIntensity ("Earth Night Light Intensity", Float) = 1.5
        _EarthNightLightThreshold ("Earth Night Light Threshold", Float) = 0.15

        _EarthWaterDeepColor ("Earth Water Deep Color", Color) = (0.015, 0.05, 0.10, 1)
        _EarthWaterShallowColor ("Earth Water Shallow/Facing Color", Color) = (0.03, 0.09, 0.16, 1)
        _EarthWaterReflectColor ("Earth Water Reflection Tint", Color) = (0.12, 0.20, 0.28, 1)

        _EarthWaterSpecSharpPower ("Earth Water Sharp Spec Power", Float) = 300.0
        _EarthWaterSpecBroadPower ("Earth Water Broad Spec Power", Float) = 18.0
        _EarthWaterSpecBroadStrength ("Earth Water Broad Spec Strength", Float) = 0.65
        _EarthWaterSpecSharpStrength ("Earth Water Sharp Spec Strength", Float) = 0.4

        _EarthWaterEdgeReflect ("Earth Water Edge Reflectivity", Float) = 0.25
        _EarthWaterBaseReflect ("Earth Water Base Reflectivity", Float) = 0.015

        _EarthCloudHeightM ("Earth Cloud Height (m)", Float) = 12000.0
        _EarthCloudTint ("Earth Cloud Tint", Color) = (1,1,1,1)
        _EarthCloudAmbient ("Earth Cloud Ambient", Range(0,1)) = 0.01
        _EarthCloudShadowPow ("Earth Cloud Terminator Power", Float) = 1.2
        _EarthCloudOpacity ("Earth Cloud Opacity", Range(0,2)) = 1.0
        _EarthCloudLimbBrightening ("Earth Cloud Limb Brightening", Float) = 0.12
        _EarthCloudLimbPower ("Earth Cloud Limb Power", Float) = 3.5

        _StarClamp ("Star Clamp Max", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Background" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 dir    : TEXCOORD0;
            };

            sampler2D _StarData;
            sampler2D _TempData;
            sampler2D _XoffData;
            sampler2D _YoffData;

            float _PixelSize;
            float _maxMag;
            float _sigma;
            float _scaleFactor;
            float _brightnessScale;

            samplerCUBE _SkyboxTex;
            float _MWbright;

            float4 _CraftBodyToEq;
            float  _ObliquityDeg;

            float4 _SunDirEcl;
            float  _SunAngRad;
            float4 _SunColor;
            float  _SunEdgeSoft;
            float  _SunIntensity;
            float  _SunCoreFalloff;
            float  _SunGlowIntensity;
            float  _SunGlowRadiusMul;
            float  _SunGlowFalloff;

            float4 _MoonPosEcl;
            float  _MoonRadiusM;
            float  _MoonAmbient;
            float  _MoonShadowPow;
            sampler2D _MoonAlbedo;
            float4 _MoonBodyToEcl;
            float4 _MoonAlbedoTint;
            float  _MoonGamma;
            float _MoonLonOffsetDeg;
            float _MoonFlipU;
            float _MoonFlipV;

            float4 _EarthPosEcl;
            float  _EarthRadiusM;
            float  _EarthScatterHeightM;
            float  _EarthAmbient;
            float  _EarthShadowPow;

            float3 _RayleighAmount;
            float  _RayleighScale;
            float  _MieAmount;
            float  _MieScale;
            float  _MieG;

            float4 _EarthAirglowColor;
            float  _EarthAirglowIntensity;
            float  _EarthAirglowHeightM;
            float  _EarthAirglowThicknessM;
            float  _EarthAirglowNightBias;

            sampler2D _EarthAlbedo;
            sampler2D _EarthMask;
            sampler2D _EarthClouds;
            float4 _EarthBodyToEcl;
            float4 _EarthAlbedoTint;
            float  _EarthGamma;

            float _EarthLonOffsetDeg;
            float _EarthFlipU;
            float _EarthFlipV;

            float  _EarthWaterSpecIntensity;
            float  _EarthWaterFresnel;
            float  _EarthNightLightIntensity;
            float  _EarthNightLightThreshold;

            float4 _EarthWaterDeepColor;
            float4 _EarthWaterShallowColor;
            float4 _EarthWaterReflectColor;

            float  _EarthWaterSpecSharpPower;
            float  _EarthWaterSpecBroadPower;
            float  _EarthWaterSpecBroadStrength;
            float  _EarthWaterSpecSharpStrength;
            float  _EarthWaterEdgeReflect;
            float  _EarthWaterBaseReflect;

            float  _EarthCloudHeightM;
            float4 _EarthCloudTint;
            float  _EarthCloudAmbient;
            float  _EarthCloudShadowPow;
            float  _EarthCloudOpacity;
            float  _EarthCloudLimbBrightening;
            float  _EarthCloudLimbPower;

            float _StarClamp;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.dir = normalize(v.vertex.xyz);
                return o;
            }

            float3 SafeNormalize(float3 v)
            {
                float len2 = dot(v,v);
                if (len2 < 1e-12) return float3(0,0,0);
                return v * rsqrt(len2);
            }

            float4 QuatConjugate(float4 q)
            {
                return float4(-q.x, -q.y, -q.z, q.w);
            }

            float3 RotateByQuat(float3 v, float4 q)
            {
                float3 u = q.xyz;
                float  s = q.w;
                float3 uv  = cross(u, v);
                float3 uuv = cross(u, uv);
                return v + 2.0 * (s * uv + uuv);
            }

            float2 BodyUV_Equirect(float3 nB)
            {
                float lon = atan2(nB.x, nB.y);
                float lat = asin(clamp(nB.z, -1.0, 1.0));
                float u = lon * 0.15915494309189535 + 0.5;
                float v = lat * 0.3183098861837907 + 0.5;
                return float2(u, v);
            }

            float3 EclToMoonBody(float3 vEcl)
            {
                float4 qE2B = QuatConjugate(_MoonBodyToEcl);
                return SafeNormalize(RotateByQuat(vEcl, qE2B));
            }

            float3 EclToEarthBody(float3 vEcl)
            {
                float4 qE2B = QuatConjugate(_EarthBodyToEcl);
                return SafeNormalize(RotateByQuat(vEcl, qE2B));
            }

            float3 EqToEcl(float3 dEq, float obliquityDeg)
            {
                float eps = obliquityDeg * 0.017453292519943295;
                float c = cos(eps);
                float s = sin(eps);
                return float3(
                    dEq.x,
                    dEq.y * c + dEq.z * s,
                   -dEq.y * s + dEq.z * c
                );
            }

            float3 EclToStarTexFrame(float3 eclDir)
            {
                float3 dTex;
                dTex.x = -eclDir.y;
                dTex.y = -eclDir.x;
                dTex.z =  eclDir.z;
                return SafeNormalize(dTex);
            }

            float decodeMagnitude(float encodedValue)
            {
                float maxMag = -1.46;
                float minMag = _maxMag;
                if (encodedValue == 0) return 40.0;
                return (minMag + (minMag - maxMag) * (encodedValue * -1.0));
            }

            float magnitudeToBrightness(float magnitude)
            {
                return exp2((-magnitude / 2.5) * 3.32192809489);
            }

            float drawStar(float distanceArcsec, float sigmaArcsec)
            {
                return exp(-(distanceArcsec * distanceArcsec) / (2.0 * sigmaArcsec * sigmaArcsec));
            }

            void OctaBaseFromDir(float3 dir, out float3 dFlip, out float2 baseTexel)
            {
                dFlip = float3(-dir.x, dir.y, dir.z);

                float sumAbs = abs(dFlip.x) + abs(dFlip.y) + abs(dFlip.z);
                float3 p = dFlip / sumAbs;

                float2 coord;
                if (p.z >= 0.0)
                {
                    coord = p.xy;
                }
                else
                {
                    coord = float2(sign(p.x) * (1.0 - abs(p.y)),
                                   sign(p.y) * (1.0 - abs(p.x)));
                }

                float2 uvOct = coord * 0.5 + 0.5;
                float2 pixelSpace = uvOct * _PixelSize;
                baseTexel = floor(pixelSpace);
            }

            float3 RetrievePixInfo(float3 dFlip, float2 baseTexel, float2 pixelOff)
            {
                float2 pixelCenter = baseTexel + 0.5 + pixelOff;
                float2 uvCenter = pixelCenter / _PixelSize;

                float starData = tex2D(_StarData, uvCenter);
                half3 tempR   = tex2D(_TempData, uvCenter);
                float XData   = tex2D(_XoffData, uvCenter);
                float YData   = tex2D(_YoffData, uvCenter);

                float starBrightness = magnitudeToBrightness(decodeMagnitude(starData) - _scaleFactor);
                starBrightness *= _brightnessScale;

                float2 coord1;
                coord1.x = (uvCenter.x * 2.0 - 1.0) + (-((((YData - 0.25) * 2.0) * 3.0) - 1.5) / _PixelSize);
                coord1.y = (uvCenter.y * 2.0 - 1.0) + ( ((((XData - 0.25) * 2.0) * 3.0) - 1.5) / _PixelSize);

                float3 Pprime1;
                if (abs(coord1.x) + abs(coord1.y) <= 1.0)
                {
                    Pprime1.xy = coord1;
                    Pprime1.z  = 1.0 - abs(coord1.x) - abs(coord1.y);
                }
                else
                {
                    Pprime1.x = sign(coord1.x) * (1.0 - abs(coord1.y));
                    Pprime1.y = sign(coord1.y) * (1.0 - abs(coord1.x));
                    Pprime1.z = -(1.0 - abs(Pprime1.x) - abs(Pprime1.y));
                }

                float3 pDir = normalize(Pprime1);
                float3 baseDir = normalize(dFlip);
                float vecDistArcsec = length(pDir - baseDir) * 206265.0;

                float intensity = drawStar(vecDistArcsec, _sigma);
                return tempR * (starBrightness * intensity);
            }

            fixed4 Desaturate(fixed4 color, float amount)
            {
                float gray = dot(color.rgb, fixed3(0.299, 0.587, 0.114));
                fixed3 d = lerp(color.rgb, fixed3(gray, gray, gray), amount);
                return fixed4(d, color.a);
            }

            float4x4 RotationMatrix(float y, float x, float z)
            {
                x = radians(x);
                y = radians(y);
                z = radians(z);

                float sinX = sin(x);
                float cosX = cos(x);
                float sinY = sin(y);
                float cosY = cos(y);
                float sinZ = sin(z);
                float cosZ = cos(z);

                return float4x4(
                    cosY * cosZ, cosZ * sinX * sinY - cosX * sinZ, cosX * cosZ * sinY + sinX * sinZ, 0,
                    cosY * sinZ, cosX * cosZ + sinX * sinY * sinZ, -cosZ * sinX + cosX * sinY * sinZ, 0,
                    -sinY,      cosY * sinX,                      cosX * cosY,                      0,
                    0,          0,                                0,                                1
                );
            }

            float3 EvalSun(float3 rayEq, float3 sunDirEq)
            {
                rayEq = SafeNormalize(rayEq);
                sunDirEq = SafeNormalize(sunDirEq);

                float cosAng = clamp(dot(rayEq, sunDirEq), -1.0, 1.0);
                float ang = acos(cosAng);

                float sunR = max(1e-6, _SunAngRad);
                float soft = max(1e-6, _SunEdgeSoft);

                float disk = 1.0 - smoothstep(sunR - soft, sunR + soft, ang);

                float coreT = saturate(1.0 - ang / sunR);
                float core = pow(coreT, max(1.0, _SunCoreFalloff));

                float glowR = sunR * max(1.0, _SunGlowRadiusMul);
                float glowT = saturate(1.0 - ang / glowR);
                float glow = pow(glowT, max(1.0, _SunGlowFalloff));
                glow *= (1.0 - disk * 0.85);

                float3 diskCol = _SunColor.rgb * _SunIntensity * disk * lerp(0.92, 1.08, core);
                float3 glowCol = _SunColor.rgb * (_SunIntensity * _SunGlowIntensity) * glow;

                return diskCol + glowCol;
            }

            bool RaySphereHit(float3 D_unit, float3 C, float R, out float tHit)
            {
                float b = dot(D_unit, C);
                float c = dot(C, C) - R * R;
                float h = b * b - c;

                if (h < 0.0)
                {
                    tHit = 0.0;
                    return false;
                }

                float s = sqrt(h);
                float t0 = b - s;
                float t1 = b + s;
                tHit = (t0 > 0.0) ? t0 : ((t1 > 0.0) ? t1 : 0.0);
                return (tHit > 0.0);
            }

            bool RaySphereHit2(float3 D_unit, float3 C, float R, out float t0, out float t1)
            {
                float b = dot(D_unit, C);
                float c = dot(C, C) - R * R;
                float h = b * b - c;

                if (h < 0.0)
                {
                    t0 = 0.0;
                    t1 = 0.0;
                    return false;
                }

                float s = sqrt(h);
                t0 = b - s;
                t1 = b + s;

                return t0 >= 0.0 && t1 >= 0.0;
            }

            float ScatterDensity(float3 pos, float scale)
            {
                return exp(-max(0.0, (length(pos) - _EarthRadiusM) / scale));
            }

            #define PI 3.14159265

            float MiePhase(float c)
            {
                float g2 = _MieG * _MieG;
                float c2 = c * c;

                float num = 3.0 / 8.0 / PI * (1.0 - g2) * (1.0 - c2);
                float inner = 1.0 + g2 - 2.0 * _MieG * c;
                float denom = inner * sqrt(inner) * (2.0 + g2);
                return num / denom;
            }

            float RayleighPhase(float c)
            {
                return 3.0 / 16.0 / PI * (1.0 + c * c);
            }

            float ScatterSun(float3 origin, float dist, float scale)
            {
                const int SUN_SCATTER_STEPS = 6;

                float stepLen = dist / SUN_SCATTER_STEPS;
                float total = 0.0;

                for (int i = 0; i < SUN_SCATTER_STEPS; i++)
                {
                    float3 pos = origin + (0.5 + i) * stepLen;
                    total += stepLen * ScatterDensity(pos, scale);
                }

                return total;
            }

            bool PointSeesSun(float3 posFromEarthCenter, float3 sunDir_unit, float earthRadius)
            {
                float b = dot(sunDir_unit, -posFromEarthCenter);
                float c = dot(posFromEarthCenter, posFromEarthCenter) - earthRadius * earthRadius;
                float h = b * b - c;

                if (h < 0.0)
                    return true;

                float s = sqrt(h);
                float t0 = b - s;
                float t1 = b + s;

                return !(t0 > 0.0 || t1 > 0.0);
            }

            float3 EvalScattering(float3 dir, float dist, float3 background)
            {
                // Lower-cost distant-Earth version
                const int SCATTER_STEPS = 16;

                float nearT;
                float farT;
                float rAtmo = _EarthRadiusM + _EarthScatterHeightM;
                if (!RaySphereHit2(dir, _EarthPosEcl.xyz, rAtmo, nearT, farT))
                    return background;

                if (dist >= 0.0 && farT > dist)
                    farT = dist;

                if (farT <= nearT)
                    return background;

                float stepLen = (farT - nearT) / SCATTER_STEPS;

                float3 rayleighCam = 0.0;
                float mieCam = 0.0;
                float3 rayleighTotal = 0.0;
                float3 mieTotal = 0.0;

                float3 sunDir = SafeNormalize(_SunDirEcl.xyz);

                for (int i = 0; i < SCATTER_STEPS; i++)
                {
                    float t = nearT + (i + 0.5) * stepLen;
                    float3 pos = t * dir - _EarthPosEcl.xyz;

                    float3 rayleighLocal = _RayleighAmount * stepLen * ScatterDensity(pos, _RayleighScale);
                    float mieLocal = _MieAmount * stepLen * ScatterDensity(pos, _MieScale);

                    rayleighCam += rayleighLocal;
                    mieCam += mieLocal;

                    float sunVisible = 0.0;
                    float3 rayleighSun = 0.0;
                    float mieSun = 0.0;

                    if (PointSeesSun(pos, sunDir, _EarthRadiusM))
                    {
                        float lightNear, lightFar;
                        if (RaySphereHit2(sunDir, -pos, rAtmo, lightNear, lightFar))
                        {
                            rayleighSun = _RayleighAmount * ScatterSun(pos, lightFar, _RayleighScale);
                            mieSun = _MieAmount * ScatterSun(pos, lightFar, _MieScale);
                            sunVisible = 1.0;
                        }
                    }

                    float3 transmission = exp(-(rayleighCam + rayleighSun + mieCam + mieSun));

                    rayleighTotal += rayleighLocal * transmission * sunVisible;
                    mieTotal += mieLocal * transmission * sunVisible;
                }

                float3 groundTransmission = exp(-(rayleighCam + mieCam));
                float c = dot(dir, -sunDir);

                return rayleighTotal * RayleighPhase(c)
                     + mieTotal * MiePhase(c)
                     + background * groundTransmission;
            }

            float3 EvalAtmosphereTransmissionToDist(float3 dir, float dist)
            {
                const int SCATTER_STEPS = 12;

                float nearT;
                float farT;
                float r = _EarthRadiusM + _EarthScatterHeightM;
                if (!RaySphereHit2(dir, _EarthPosEcl.xyz, r, nearT, farT))
                    return float3(1.0, 1.0, 1.0);

                if (dist >= 0.0 && farT > dist)
                    farT = dist;

                if (farT <= nearT)
                    return float3(1.0, 1.0, 1.0);

                float stepLen = (farT - nearT) / SCATTER_STEPS;

                float3 rayleighCam = 0.0;
                float mieCam = 0.0;

                for (int i = 0; i < SCATTER_STEPS; i++)
                {
                    float t = nearT + (i + 0.5) * stepLen;
                    float3 pos = t * dir - _EarthPosEcl.xyz;

                    rayleighCam += _RayleighAmount * stepLen * ScatterDensity(pos, _RayleighScale);
                    mieCam += _MieAmount * stepLen * ScatterDensity(pos, _MieScale);
                }

                return exp(-(rayleighCam + mieCam));
            }

            float4 EvalMoon(float3 rayEcl_unit, float3 sunDirEcl_unit)
            {
                float3 C = _MoonPosEcl.xyz;
                float  R = _MoonRadiusM;

                float t;
                if (!RaySphereHit(rayEcl_unit, C, R, t))
                    return float4(0,0,0,0);

                float3 P = rayEcl_unit * t;
                float3 N = SafeNormalize(P - C);

                float nl = saturate(dot(N, sunDirEcl_unit));
                nl = pow(nl, max(1e-3, _MoonShadowPow));
                float shade = max(_MoonAmbient, nl);

                float3 nB = EclToMoonBody(N);
                float2 uv = BodyUV_Equirect(nB);

                uv.x = frac(uv.x + (_MoonLonOffsetDeg / 360.0));
                if (_MoonFlipU > 0.5) uv.x = 1.0 - uv.x;
                if (_MoonFlipV > 0.5) uv.y = 1.0 - uv.y;

                float3 albedo = tex2D(_MoonAlbedo, uv).rgb;
                albedo = pow(max(albedo, 0.0), _MoonGamma);
                albedo *= _MoonAlbedoTint.rgb;

                return float4(albedo * shade, 1.0);
            }

            float4 EvalEarthSurface(float3 rayEcl_unit, float3 sunDirEcl_unit, out float dist)
            {
                float3 C = _EarthPosEcl.xyz;
                float  R = _EarthRadiusM;

                float t;
                if (!RaySphereHit(rayEcl_unit, C, R, t))
                {
                    dist = 0.0;
                    return float4(0,0,0,0);
                }

                dist = t;

                float3 P = rayEcl_unit * t;
                float3 N = SafeNormalize(P - C);

                float nl_raw = dot(N, sunDirEcl_unit);
                float nl = saturate(nl_raw);
                nl = pow(nl, max(1e-3, _EarthShadowPow));
                float shade = max(_EarthAmbient, nl);

                float3 nB = EclToEarthBody(N);
                float2 uv = BodyUV_Equirect(nB);

                uv.x = frac(uv.x + (_EarthLonOffsetDeg / 360.0));
                if (_EarthFlipU > 0.5) uv.x = 1.0 - uv.x;
                if (_EarthFlipV > 0.5) uv.y = 1.0 - uv.y;

                float3 albedo = tex2D(_EarthAlbedo, uv).rgb;
                albedo = pow(max(albedo, 0.0), _EarthGamma);
                albedo *= _EarthAlbedoTint.rgb;

                float4 maskSample = tex2D(_EarthMask, uv);

                float landMask = saturate(maskSample.a);
                float waterMask = 1.0 - landMask;
                float3 nightLightsTex = maskSample.rgb;

                float3 V = SafeNormalize(-rayEcl_unit);
                float3 L = SafeNormalize(sunDirEcl_unit);
                float3 H = SafeNormalize(V + L);

                float NdotL = saturate(dot(N, L));
                float NdotV = saturate(dot(N, V));
                float NdotH = saturate(dot(N, H));

                float3 landCol = albedo * shade;

                float waterFacing = saturate(NdotL * 0.65 + NdotV * 0.35);
                float3 waterBase = lerp(_EarthWaterDeepColor.rgb, _EarthWaterShallowColor.rgb, waterFacing);

                float fresnel = pow(1.0 - NdotV, 5.0);
                float reflectivity = lerp(_EarthWaterBaseReflect, _EarthWaterEdgeReflect, saturate(fresnel * _EarthWaterFresnel));

                float specSharp = pow(NdotH, max(1.0, _EarthWaterSpecSharpPower)) * _EarthWaterSpecSharpStrength;
                float specBroad = pow(NdotH, max(1.0, _EarthWaterSpecBroadPower)) * _EarthWaterSpecBroadStrength;
                float waterSpec = (specSharp + specBroad) * NdotL;

                float3 reflectedTint = _EarthWaterReflectColor.rgb * reflectivity;
                float3 waterLit = waterBase * lerp(_EarthAmbient, 1.0, NdotL);

                float3 waterCol = waterLit;
                waterCol += reflectedTint;
                waterCol += _SunColor.rgb * (_EarthWaterSpecIntensity * waterSpec);

                float3 surfaceCol = landCol * landMask + waterCol * waterMask;

                float nightFactor = saturate((-nl_raw - _EarthNightLightThreshold) / (1.0 - _EarthNightLightThreshold));
                float3 nightLightCol = nightLightsTex * (_EarthNightLightIntensity * nightFactor) * landMask;

                return float4(surfaceCol + nightLightCol, 1.0);
            }

            float4 EvalEarthClouds(float3 rayEcl_unit, float3 sunDirEcl_unit, out float dist)
            {
                float3 C = _EarthPosEcl.xyz;
                float  R = _EarthRadiusM + _EarthCloudHeightM;

                float t;
                if (!RaySphereHit(rayEcl_unit, C, R, t))
                {
                    dist = 0.0;
                    return float4(0,0,0,0);
                }

                dist = t;

                float3 P = rayEcl_unit * t;
                float3 N = SafeNormalize(P - C);

                float nlRaw = dot(N, sunDirEcl_unit);
                float nl = saturate(nlRaw);
                nl = pow(nl, max(1e-3, _EarthCloudShadowPow));

                float twilight = saturate((nlRaw + 0.08) / 0.16);
                float ambient = _EarthCloudAmbient * twilight;
                float shade = max(ambient, nl);

                float3 V = SafeNormalize(-rayEcl_unit);
                float NdotV = saturate(dot(N, V));
                float limb = pow(1.0 - NdotV, max(1.0, _EarthCloudLimbPower));
                float limbBright = limb * nl * _EarthCloudLimbBrightening;

                float3 nB = EclToEarthBody(N);
                float2 uv = BodyUV_Equirect(nB);

                uv.x = frac(uv.x + (_EarthLonOffsetDeg / 360.0));
                if (_EarthFlipU > 0.5) uv.x = 1.0 - uv.x;
                if (_EarthFlipV > 0.5) uv.y = 1.0 - uv.y;

                float4 cloudTex = tex2D(_EarthClouds, uv);
                float alpha = cloudTex.a * _EarthCloudOpacity;
                alpha = saturate(alpha);

                float3 cloudRgb = cloudTex.rgb * _EarthCloudTint.rgb;
                float3 col = cloudRgb * (shade + limbBright);

                return float4(col, alpha);
            }

            float3 EvalEarthAirglow(float3 rayEcl_unit, float3 sunDirEcl_unit)
            {
                float3 C = _EarthPosEcl.xyz;

                float innerR = _EarthRadiusM + _EarthAirglowHeightM;
                float outerR = innerR + max(1.0, _EarthAirglowThicknessM);

                float t0o, t1o;
                if (!RaySphereHit2(rayEcl_unit, C, outerR, t0o, t1o))
                    return 0.0;

                float t0i, t1i;
                bool hitInner = RaySphereHit2(rayEcl_unit, C, innerR, t0i, t1i);

                float shellLen = 0.0;
                if (hitInner)
                    shellLen = max(0.0, (t1o - t0o) - (t1i - t0i));
                else
                    shellLen = max(0.0, t1o - t0o);

                if (shellLen <= 0.0)
                    return 0.0;

                float shellNorm = shellLen / max(1.0, _EarthAirglowThicknessM);
                float limbBoost = 1.0 - exp(-shellNorm * 0.35);

                float tMid = 0.5 * (t0o + t1o);
                float3 P = rayEcl_unit * tMid;
                float3 N = SafeNormalize(P - C);

                float nlRaw = dot(N, sunDirEcl_unit);
                float nightFactor = saturate((-nlRaw * 0.5 + 0.5) * _EarthAirglowNightBias);
                nightFactor = pow(nightFactor, 1.5);

                return _EarthAirglowColor.rgb * (_EarthAirglowIntensity * limbBoost * nightFactor);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dirB = SafeNormalize(float3(-i.dir.x, i.dir.y, i.dir.z));
                float3 dirEq = SafeNormalize(RotateByQuat(dirB, _CraftBodyToEq));
                float3 sunDirEcl = SafeNormalize(_SunDirEcl.xyz);

                // Primary body: Moon
                float4 moonCol = EvalMoon(dirEq, sunDirEcl);
                if (moonCol.a > 0.5)
                    return float4(moonCol.rgb, 1.0);

                // Secondary body: Earth with distant-view effects
                float cloudDist;
                float4 earthCloudCol = EvalEarthClouds(dirEq, sunDirEcl, cloudDist);

                float earthDist;
                float4 earthSurfCol = EvalEarthSurface(dirEq, sunDirEcl, earthDist);

                bool earthVisible = (earthSurfCol.a > 0.5);
                bool cloudInFront = (earthCloudCol.a > 0.0 && (earthDist <= 0.0 || cloudDist < earthDist));

                if (earthVisible || cloudInFront)
                {
                    float3 earthCol = earthSurfCol.rgb;
                    float scatterDist = earthDist;

                    if (earthVisible)
                    {
                        earthCol = EvalScattering(dirEq, earthDist, earthCol);
                    }

                    earthCol += EvalEarthAirglow(dirEq, sunDirEcl);

                    if (cloudInFront)
                    {
                        float3 cloudTrans = EvalAtmosphereTransmissionToDist(dirEq, cloudDist);
                        float3 cloudRgbScattered = earthCloudCol.rgb * cloudTrans;
                        earthCol = lerp(earthCol, cloudRgbScattered, earthCloudCol.a);
                    }

                    return float4(earthCol, 1.0);
                }

                float3 sunCol = EvalSun(dirEq, sunDirEcl);

                float3 dirEcl = SafeNormalize(EqToEcl(dirEq, _ObliquityDeg));
                float3 ndir = EclToStarTexFrame(dirEcl);

                float3 dFlip;
                float2 baseTexel;
                OctaBaseFromDir(ndir, dFlip, baseTexel);

                float3 s0 = RetrievePixInfo(dFlip, baseTexel, float2(0,0));
                float3 s1 = RetrievePixInfo(dFlip, baseTexel, float2(1,0));
                float3 s2 = RetrievePixInfo(dFlip, baseTexel, float2(1,1));
                float3 s3 = RetrievePixInfo(dFlip, baseTexel, float2(0,1));
                float3 s4 = RetrievePixInfo(dFlip, baseTexel, float2(-1,1));
                float3 s5 = RetrievePixInfo(dFlip, baseTexel, float2(-1,0));
                float3 s6 = RetrievePixInfo(dFlip, baseTexel, float2(-1,-1));
                float3 s7 = RetrievePixInfo(dFlip, baseTexel, float2(0,-1));
                float3 s8 = RetrievePixInfo(dFlip, baseTexel, float2(1,-1));

                float3 starsCol = s0+s1+s2+s3+s4+s5+s6+s7+s8;
                float starPeak = max(starsCol.r, max(starsCol.g, starsCol.b));
                if (starPeak > _StarClamp)
                    starsCol *= (_StarClamp / max(1e-6, starPeak));

                float4x4 rotMatrix = RotationMatrix(300, 171, 156);
                float3 rotatedDir = mul(rotMatrix, float4(ndir, 1.0)).xyz;
                fixed4 mw = Desaturate(texCUBE(_SkyboxTex, rotatedDir) * _MWbright, 0.6);

                return float4(mw.rgb + starsCol + sunCol, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}