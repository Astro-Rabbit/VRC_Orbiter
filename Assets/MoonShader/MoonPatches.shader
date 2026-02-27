Shader "Skybox/MoonOrbit_M5_AlbedoPatches_Stars"
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

        _MWRotY ("MW Rot Y (deg)", Float) = 300
        _MWRotX ("MW Rot X (deg)", Float) = 171
        _MWRotZ ("MW Rot Z (deg)", Float) = 156

        // =========================
        // Moon (sphere, albedo patching only)
        // =========================
        _MoonCenterWS ("Moon Center WS (m)", Vector) = (0, 0, 4000000, 1)
        _MoonRadiusWS ("Moon Radius (m)", Float)  = 1727400

        _MoonYawDeg   ("Moon Yaw (deg, about +Z body)", Float) = 0
        _MoonPitchDeg ("Moon Pitch (deg, about +X body)", Float) = 0
        _MoonRollDeg  ("Moon Roll (deg, about +Y body)", Float) = 0

        _MoonAlbedo   ("Moon Albedo BASE (equirect)", 2D) = "gray" {}

        // Up to 4 patch tiles (preloaded now; streamed later)
        _MoonPatch0 ("Moon Patch 0 (tile)", 2D) = "black" {}
        _MoonPatch1 ("Moon Patch 1 (tile)", 2D) = "black" {}
        _MoonPatch2 ("Moon Patch 2 (tile)", 2D) = "black" {}
        _MoonPatch3 ("Moon Patch 3 (tile)", 2D) = "black" {}

        // Rects are (uMin, vMin, uMax, vMax) in global equirect UV.
        // Dateline-wrap convention: if uMin > uMax, region wraps across u=1->0.
        _MoonPatchRect0 ("PatchRect0 (uMin,vMin,uMax,vMax)", Vector) = (0,0,0,0)
        _MoonPatchRect1 ("PatchRect1 (uMin,vMin,uMax,vMax)", Vector) = (0,0,0,0)
        _MoonPatchRect2 ("PatchRect2 (uMin,vMin,uMax,vMax)", Vector) = (0,0,0,0)
        _MoonPatchRect3 ("PatchRect3 (uMin,vMin,uMax,vMax)", Vector) = (0,0,0,0)

        _MoonPatchFeather ("Patch Feather (UV units)", Range(0.0, 0.02)) = 0.002
        _MoonPatchEnable ("Enable Patches", Range(0,1)) = 1

        _LimbAA ("Limb AA Strength", Range(0.5, 30.0)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Back
        ZWrite Off
        ZTest LEqual

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

            sampler2D _MoonPatch0, _MoonPatch1, _MoonPatch2, _MoonPatch3;
            float4 _MoonPatchRect0, _MoonPatchRect1, _MoonPatchRect2, _MoonPatchRect3;
            float _MoonPatchFeather;
            float _MoonPatchEnable;

            float _LimbAA;

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
            // Utility
            // -------------------------
            float3 SafeNormalize(float3 v)
            {
                float len2 = dot(v,v);
                if (len2 < 1e-12) return float3(0,0,1);
                return v * rsqrt(len2);
            }

            float2 WrapU(float2 uv) { uv.x = frac(uv.x); return uv; }

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
            // Star functions (unchanged)
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
                return exp2((-magnitude / 2.5) * 3.32192809489);
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

                float3 ref0 = float3(0, -1, 0);
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

                float vecDist = length(pDir - baseDir) * 206265.0;
                float intensity = drawStar(vecDist, _sigma);

                return float4(tempR, 1.0) * (starBrightness * intensity);
            }

            // -------------------------
            // Moon helpers (rotation + equirect UV)
            // -------------------------
            float3x3 RotX(float a){ float s=sin(a), c=cos(a); return float3x3(1,0,0, 0,c,-s, 0,s,c); }
            float3x3 RotY(float a){ float s=sin(a), c=cos(a); return float3x3(c,0,s, 0,1,0, -s,0,c); }
            float3x3 RotZ(float a){ float s=sin(a), c=cos(a); return float3x3(c,-s,0, s,c,0, 0,0,1); }

            float2 BodyDirToUV(float3 N_body)
            {
                float lon = atan2(N_body.y, N_body.x);
                float lat = asin(clamp(N_body.z, -1.0, 1.0));
                float u = lon * (1.0 / (2.0 * UNITY_PI)) + 0.5;
                float v = lat * (1.0 / UNITY_PI) + 0.5;
                return float2(frac(u), saturate(v));
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

            // -------------------------
            // Patch sampling
            // Rect: (uMin, vMin, uMax, vMax). Wrap if uMin > uMax.
            // Returns:
            //   w: feathered weight in [0..1]
            //   uvLocal: 0..1 within patch for sampling
            // -------------------------
            float RectWidthU(float uMin, float uMax)
            {
                // Handles wrap
                return (uMin <= uMax) ? (uMax - uMin) : ((1.0 - uMin) + uMax);
            }

            float InRangeWrapU(float u, float uMin, float uMax)
            {
                // Returns 1 if inside region considering wrap convention.
                if (uMin <= uMax)
                    return (u >= uMin && u <= uMax) ? 1.0 : 0.0;

                // Wrap case: [uMin..1] U [0..uMax]
                return (u >= uMin || u <= uMax) ? 1.0 : 0.0;
            }

            float DistToEdgeU_Wrap(float u, float uMin, float uMax)
            {
                // Distance to nearest U edge, respecting wrap convention.
                // Only valid when u is inside the region.
                if (uMin <= uMax)
                {
                    return min(u - uMin, uMax - u);
                }
                else
                {
                    // inside means u>=uMin OR u<=uMax
                    float d0 = (u >= uMin) ? (u - uMin) : (u + 1.0 - uMin);
                    float d1 = (u <= uMax) ? (uMax - u) : (uMax + 1.0 - u);
                    return min(d0, d1);
                }
            }

            float PatchWeightAndLocalUV(float2 uv, float4 rect, float feather, out float2 uvLocal)
            {
                float uMin = frac(rect.x);
                float vMin = rect.y;
                float uMax = frac(rect.z);
                float vMax = rect.w;

                // Reject empty/disabled rects quickly (common when unused slots are 0s)
                float wRect = RectWidthU(uMin, uMax);
                float hRect = vMax - vMin;
                if (wRect <= 1e-6 || hRect <= 1e-6)
                {
                    uvLocal = float2(0,0);
                    return 0.0;
                }

                uv = WrapU(uv);

                float insideU = InRangeWrapU(uv.x, uMin, uMax);
                float insideV = (uv.y >= vMin && uv.y <= vMax) ? 1.0 : 0.0;

                if (insideU * insideV < 0.5)
                {
                    uvLocal = float2(0,0);
                    return 0.0;
                }

                // Local UV mapping in U handles wrap.
                float uSpan = wRect;
                float uOff;
                if (uMin <= uMax)
                {
                    uOff = uv.x - uMin;
                }
                else
                {
                    // Wrap: shift u into same "unwrapped" interval starting at uMin
                    uOff = (uv.x >= uMin) ? (uv.x - uMin) : (uv.x + 1.0 - uMin);
                }

                float vOff = uv.y - vMin;

                uvLocal = float2(uOff / max(uSpan, 1e-6), vOff / max(hRect, 1e-6));

                // Feather weight: min distance to any edge / feather
                float dU = DistToEdgeU_Wrap(uv.x, uMin, uMax);
                float dV = min(uv.y - vMin, vMax - uv.y);
                float d = min(dU, dV);

                float f = max(feather, 1e-6);
                float w = saturate(d / f);      // 0 at edge, 1 after feather distance
                // soften a bit
                w = w * w * (3.0 - 2.0 * w);    // smoothstep(0..1)
                return w;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 O = _WorldSpaceCameraPos;
                float3 D = normalize(i.dirWS);

                // =========================
                // 1) Star + Milky Way background
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
                bg = max(bg, _BgColor.rgb);

                // =========================
                // 2) Moon intersection (pure sphere)
                // =========================
                float3 C = _MoonCenterWS.xyz;
                float  R = max(_MoonRadiusWS, 1e-3);

                float tNear, tFar;
                if (!RaySphereNearFar(O, D, C, R, tNear, tFar))
                    return fixed4(bg, 1.0);

                if (tFar <= 0.0)
                    return fixed4(bg, 1.0);

                if (tNear < 0.0) tNear = 0.0;

                // Closest approach for limb AA
                float3 oc = O - C;
                float b = dot(oc, D);
                float tC = clamp(-b, tNear, tFar);

                // Signed distance to surface at closest point
                float3 Pc = O + tC * D;
                float Fc = length(Pc - C) - R;

                float w = fwidth(Fc) * _LimbAA;
                float coverage = saturate(0.5 - Fc / max(w, 1e-6));

                if (coverage <= 0.0)
                    return fixed4(bg, 1.0);

                // For a sphere, near hit is sufficient
                float3 P = O + tNear * D;
                float3 N_ws = SafeNormalize(P - C);

                // Apply Moon body rotation (so you can rotate the texture)
                float yaw   = radians(_MoonYawDeg);
                float pitch = radians(_MoonPitchDeg);
                float roll  = radians(_MoonRollDeg);
                float3x3 bodyToWorld = mul(RotY(roll), mul(RotX(pitch), RotZ(yaw)));
                float3x3 worldToBody = transpose(bodyToWorld);

                float3 N_body = mul(worldToBody, N_ws);
                float2 uv = BodyDirToUV(N_body);

                // =========================
                // 3) Albedo = base + patch tiles
                // =========================
                float3 baseCol = tex2D(_MoonAlbedo, uv).rgb;

                if (_MoonPatchEnable > 0.5)
                {
                    float feather = _MoonPatchFeather;

                    float3 sumCol = 0.0;
                    float  sumW   = 0.0;

                    float2 uvL;
                    float w0 = PatchWeightAndLocalUV(uv, _MoonPatchRect0, feather, uvL);
                    if (w0 > 0.0) { sumCol += w0 * tex2D(_MoonPatch0, uvL).rgb; sumW += w0; }

                    float w1 = PatchWeightAndLocalUV(uv, _MoonPatchRect1, feather, uvL);
                    if (w1 > 0.0) { sumCol += w1 * tex2D(_MoonPatch1, uvL).rgb; sumW += w1; }

                    float w2 = PatchWeightAndLocalUV(uv, _MoonPatchRect2, feather, uvL);
                    if (w2 > 0.0) { sumCol += w2 * tex2D(_MoonPatch2, uvL).rgb; sumW += w2; }

                    float w3 = PatchWeightAndLocalUV(uv, _MoonPatchRect3, feather, uvL);
                    if (w3 > 0.0) { sumCol += w3 * tex2D(_MoonPatch3, uvL).rgb; sumW += w3; }

                    if (sumW > 1e-5)
                    {
                        float3 patchAvg = sumCol / sumW;
                        baseCol = lerp(baseCol, patchAvg, saturate(sumW));
                    }
                }

                // =========================
                // 4) Composite: Moon occludes background
                // =========================
                #if defined(UNITY_COLORSPACE_GAMMA)
                    bg      = GammaToLinearSpace(bg);
                    baseCol = GammaToLinearSpace(baseCol);
                #endif

                float3 outCol = lerp(bg, baseCol, coverage);

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