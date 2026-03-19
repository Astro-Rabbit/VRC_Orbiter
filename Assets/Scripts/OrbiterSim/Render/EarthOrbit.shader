Shader "Skybox/SkySunEarthTest"
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

        // Quaternion (x,y,z,w): BODY -> sky frame used by shader
        _CraftBodyToEq ("Craft BodyToEq quat (xyzw)", Vector) = (0,0,0,1)

        // Obliquity epsilon in degrees (equatorial -> ecliptic)
        _ObliquityDeg ("Obliquity (deg)", Float) = 23.439281

        // --- Sun ---
        _SunDirEcl   ("Sun Dir (ECL, unit)", Vector) = (1,0,0,0)
        _SunAngRad   ("Sun Angular Radius (rad)", Float) = 0.00463
        _SunColor    ("Sun Color (HDR)", Color) = (8,8,7.5,1)
        _SunEdgeSoft ("Sun Edge Softness", Float) = 0.00015

        _SunIntensity   ("Sun Intensity (HDR scalar)", Float) = 25.0
        _SpikeStrength  ("Spike Strength", Float) = 0.6
        _SpikeSharpness ("Spike Sharpness", Float) = 80.0
        _SpikeWidth     ("Spike Width (rad)", Float) = 0.0006
        _SpikeLength    ("Spike Length (rad)", Float) = 0.02
        _SpikeRotateDeg ("Spike Rotation (deg)", Float) = 0.0

        // --- Earth (primary body in this shader) ---
        _EarthPosEcl   ("Earth Pos (craft->earth, ECL, meters)", Vector) = (0,0,0,0)
        _EarthRadiusM  ("Earth Radius (m)", Float) = 6371000.0
        _EarthScatterHeightM ("Height Scattering Height (m)", Float) = 100000.0
        _EarthAmbient  ("Earth Ambient", Range(0,1)) = 0.03
        _EarthShadowPow("Earth Terminator Power", Float) = 1.0

        _RayleighAmount ("Rayleigh Scattering Coefficients", Vector) = (0.0000055, 0.000013, 0.0000224, 0)
        _RayleighScale ("Rayleigh Scattering Scale Height", Float) = 8000
        _MieAmount ("Mie Scattering Coefficient", Float) = 0.000021
        _MieScale ("Mie Scattering Scale Height", Float) = 1200
        _MieG ("Mie Scattering Direction Coefficient", Float) = -0.78

        _EarthAlbedo ("Earth Albedo (equirect)", 2D) = "white" {}
        _EarthBodyToEcl ("Earth BodyToEcl quat (xyzw)", Vector) = (0,0,0,1)

        _EarthAlbedoTint ("Earth Albedo Tint", Color) = (1,1,1,1)
        _EarthGamma ("Earth Albedo Gamma", Float) = 1.0

        _EarthLonOffsetDeg ("Earth Lon Offset (deg)", Float) = 180
        _EarthFlipU ("Earth Flip U (0/1)", Float) = 0
        _EarthFlipV ("Earth Flip V (0/1)", Float) = 1

        // --- Moon (secondary body in this shader) ---
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

            float _SpikeStrength;
            float _SpikeSharpness;
            float _SpikeWidth;
            float _SpikeLength;
            float _SpikeRotateDeg;

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

            sampler2D _EarthAlbedo;
            float4 _EarthBodyToEcl;
            float4 _EarthAlbedoTint;
            float  _EarthGamma;

            float _EarthLonOffsetDeg;
            float _EarthFlipU;
            float _EarthFlipV;

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

                float u = lon * (0.15915494309189535) + 0.5;
                float v = lat * (0.3183098861837907) + 0.5;

                return float2(u, v);
            }

            float3 EclToEarthBody(float3 vEcl)
            {
                float4 qE2B = QuatConjugate(_EarthBodyToEcl);
                return SafeNormalize(RotateByQuat(vEcl, qE2B));
            }

            float3 EclToMoonBody(float3 vEcl)
            {
                float4 qE2B = QuatConjugate(_MoonBodyToEcl);
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
                dTex.x = eclDir.y;
                dTex.y = -eclDir.x;
                dTex.z = eclDir.z;
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

            void BuildSunBasis(float3 sunDirEq, out float3 t1, out float3 t2)
            {
                float3 ref = (abs(sunDirEq.z) < 0.9) ? float3(0,0,1) : float3(0,1,0);
                t1 = SafeNormalize(cross(ref, sunDirEq));
                t2 = SafeNormalize(cross(sunDirEq, t1));
            }

            void RotateBasis(inout float3 t1, inout float3 t2, float deg)
            {
                float a = radians(deg);
                float s = sin(a);
                float c = cos(a);
                float3 u = t1;
                float3 v = t2;
                t1 = u * c + v * s;
                t2 = -u * s + v * c;
            }

            float EvalSpikes(float3 rayEq, float3 sunDirEq)
            {
                rayEq = SafeNormalize(rayEq);
                sunDirEq = SafeNormalize(sunDirEq);

                float cosAng = dot(rayEq, sunDirEq);

                float cosGate = cos(_SunAngRad + _SpikeLength);
                float gate = saturate((cosAng - cosGate) / max(1e-5, (1.0 - cosGate)));

                float3 t1, t2;
                BuildSunBasis(sunDirEq, t1, t2);
                RotateBasis(t1, t2, _SpikeRotateDeg);

                float u = dot(rayEq, t1);
                float v = dot(rayEq, t2);

                float w = max(1e-6, _SpikeWidth);
                float spikeU = exp(-abs(v) / w);
                float spikeV = exp(-abs(u) / w);

                spikeU = pow(saturate(spikeU), _SpikeSharpness);
                spikeV = pow(saturate(spikeV), _SpikeSharpness);

                float spikes = (spikeU + spikeV);
                return spikes * gate * _SpikeStrength;
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

                float4x4 rotMatrix  = float4x4(
                    cosY * cosZ, cosZ * sinX * sinY - cosX * sinZ, cosX * cosZ * sinY + sinX * sinZ, 0,
                    cosY * sinZ, cosX * cosZ + sinX * sinY * sinZ, -cosZ * sinX + cosX * sinY * sinZ, 0,
                    -sinY,      cosY * sinX,                      cosX * cosY,                      0,
                    0,          0,                                0,                                1
                );

                return rotMatrix;
            }

            float3 EvalSun(float3 rayEq, float3 sunDirEq)
            {
                float cosAng = dot(SafeNormalize(rayEq), SafeNormalize(sunDirEq));
                float cosLim = cos(_SunAngRad);
                float soft   = max(1e-6, _SunEdgeSoft);

                float disk = smoothstep(cosLim - soft, cosLim + soft, cosAng);
                float spikes = EvalSpikes(rayEq, sunDirEq);

                float3 col = _SunColor.rgb * _SunIntensity * disk;
                col += _SunColor.rgb * _SunIntensity * spikes;

                return col;
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
                float c2 = c*c;

                float num = 3.0 / 8.0 / PI * (1.0 - g2) * (1.0 - c2);
                float inner = 1.0 + g2 - 2.0*_MieG*c;
                float denom = inner * sqrt(inner) * (2.0 + g2);
                return num / denom;
            }

            float RayleighPhase(float c)
            {
                return 3.0 / 16.0 / PI * (1.0 + c*c);
            }

            float ScatterSun(float3 origin, float dist, float scale)
            {
                const int SUN_SCATTER_STEPS = 8;

                float stepLen = dist / SUN_SCATTER_STEPS;

                float total = 0.0;
                for (int i = 0; i < SUN_SCATTER_STEPS; i++)
                {
                    float3 pos = origin + (0.5 + i) * stepLen;
                    total += stepLen * ScatterDensity(pos, scale);
                }

                return total;
            }

            float3 EvalScattering(float3 dir, float dist, float3 background)
            {
                const int SCATTER_STEPS = 32;

                float near;
                float far;
                float r = _EarthRadiusM + _EarthScatterHeightM;
                if (!RaySphereHit2(dir, _EarthPosEcl.xyz, r, near, far))
                    return background;

                if (far > dist)
                    far = dist;

                float stepLen = (far - near) / SCATTER_STEPS;

                float3 rayleighCam = 0.0;
                float mieCam = 0.0;
                float3 rayleighTotal = 0.0;
                float3 mieTotal = 0.0;
                for (int i = 0; i < SCATTER_STEPS; i++)
                {
                    float t = near + (i + 0.5) * stepLen;
                    float3 pos = t * dir - _EarthPosEcl;

                    float3 rayleighLocal = _RayleighAmount * stepLen * ScatterDensity(pos, _RayleighScale);
                    float mieLocal = _MieAmount * stepLen * ScatterDensity(pos, _MieScale);

                    rayleighCam += rayleighLocal;
                    mieCam += mieLocal;

                    float _, lightFar;
                    RaySphereHit2(_SunDirEcl, -pos, r, _, lightFar);

                    float3 rayleighSun = _RayleighAmount * ScatterSun(pos, lightFar, _RayleighScale);
                    float mieSun = _MieAmount * ScatterSun(pos, lightFar, _MieScale);

                    float3 transmission = exp(-(rayleighCam + rayleighSun + mieCam + mieSun));

                    rayleighTotal += rayleighLocal * transmission;
                    mieTotal += mieLocal * transmission;
                }

                float3 groundTransmission = exp(-(rayleighCam + mieCam));

                float c = dot(dir, -_SunDirEcl);
                return rayleighTotal*RayleighPhase(c) + mieTotal*MiePhase(c) + background*groundTransmission;
            }

            float4 EvalEarth(float3 rayEcl_unit, float3 sunDirEcl_unit, out float dist)
            {
                float3 C = _EarthPosEcl.xyz;
                float  R = _EarthRadiusM;

                float t;
                if (!RaySphereHit(rayEcl_unit, C, R, t))
                    return float4(0,0,0,0);
                dist = t;

                float3 P = rayEcl_unit * t;
                float3 N = SafeNormalize(P - C);

                float nl = saturate(dot(N, sunDirEcl_unit));
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

                float3 col = albedo * shade;
                return float4(col, 1.0);
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

                float3 col = albedo * shade;
                return float4(col, 1.0);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 col;

                float3 dirB = SafeNormalize(float3(-i.dir.x, i.dir.y, i.dir.z));

                // Keep same convention as your current shader path
                float3 dirEq = SafeNormalize(RotateByQuat(dirB, _CraftBodyToEq));
                float3 sunDirEcl = SafeNormalize(_SunDirEcl.xyz);

                float dist;

                float4 earthCol = EvalEarth(dirEq, sunDirEcl, dist);
                float4 moonCol  = EvalMoon(dirEq, sunDirEcl);
                float3 sunCol   = EvalSun(dirEq, sunDirEcl);

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

                float4x4 rotMatrix = RotationMatrix(300, 171, 156);
                float3 rotatedDir = mul(rotMatrix, float4(ndir, 1.0)).xyz;
                fixed4 mw = Desaturate(texCUBE(_SkyboxTex, rotatedDir) * _MWbright, 0.6);

                col = mw.rgb + (s0+s1+s2+s3+s4+s5+s6+s7+s8) + sunCol;

                // Earth-dominant policy
                if (earthCol.a > 0.5)
                    col = earthCol.rgb;
                else
                    dist = 1.0 / 0.0; // INF

                if (moonCol.a > 0.5)
                    col = moonCol.rgb;

                col = EvalScattering(dirEq, dist, col);

                return float4(col, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}