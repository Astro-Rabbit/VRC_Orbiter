Shader "Orbiter/CanopyGlass_SkyLinked"
{
    Properties
    {
        [Header(Base Glass)]
        _GlassTint ("Glass Tint", Color) = (0.72, 0.90, 1.00, 1.0)
        _BaseAlpha ("Base Alpha", Range(0.0, 1.0)) = 0.22

        [Header(Fresnel)]
        _FresnelColor ("Fresnel Color", Color) = (0.85, 0.95, 1.0, 1.0)
        _FresnelPower ("Fresnel Power", Range(0.5, 8.0)) = 3.0
        _FresnelStrength ("Fresnel Strength", Range(0.0, 2.0)) = 0.65

        [Header(Reflection)]
        _ReflectionStrength ("Reflection Strength", Range(0.0, 2.0)) = 0.45
        _ReflectionFresnelBoost ("Reflection Fresnel Boost", Range(0.0, 2.0)) = 0.75

        [Header(Imperfections)]
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0.0, 2.0)) = 0.35

        _DetailNormalMap ("Detail Normal Map", 2D) = "bump" {}
        _DetailNormalStrength ("Detail Normal Strength", Range(0.0, 2.0)) = 0.15
        _DetailTiling ("Detail Tiling", Float) = 6.0

        _ImperfectionMask ("Imperfection Mask", 2D) = "white" {}
        _ImperfectionStrength ("Imperfection Strength", Range(0.0, 1.0)) = 0.2

        [Header(Extra)]
        _EdgeDarken ("Edge Darken", Range(0.0, 1.0)) = 0.08
        _AlphaFresnelBoost ("Alpha Fresnel Boost", Range(0.0, 1.0)) = 0.08

        [Header(Vignette Control)]
        _VignetteStrength ("Vignette Strength", Range(0.0, 1.0)) = 0.0
        _VignetteStart ("Vignette Start", Range(0.0, 1.0)) = 0.35
        _VignetteEnd ("Vignette End", Range(0.0, 1.0)) = 0.75
        _VignetteHardness ("Vignette Hardness", Range(0.25, 8.0)) = 2.0

        [Header(Dark State)]
        _DarkFinalColor ("Dark Final Color", Color) = (0,0,0,1)
        _DarkFinalAlpha ("Dark Final Alpha", Range(0.0, 1.0)) = 1.0
        _DarkReflectStrength ("Dark Reflect Strength", Range(0.0, 2.0)) = 0.0
        _DarkFresnelStrength ("Dark Fresnel Strength", Range(0.0, 2.0)) = 0.0

        [Header(Hex Grid)]
        _GridEnable ("Grid Enable", Range(0.0, 1.0)) = 1.0
        _GridUVSelect ("Grid UV Select (0=UV0, 1=UV1)", Range(0.0, 1.0)) = 0.0
        _GridScale ("Grid Scale", Float) = 14.0
        _GridLineWidth ("Grid Line Width", Range(0.001, 0.08)) = 0.01
        _GridRateWidthBoost ("Grid Rate Width Boost", Range(0.0, 5.0)) = 1.5
        _GridIntensity ("Grid Intensity", Range(0.0, 1.0)) = 0.05
        _GridRateIntensityBoost ("Grid Rate Intensity Boost", Range(0.0, 5.0)) = 0.4
        _GridColor ("Grid Color", Color) = (0.75, 0.88, 1.0, 1.0)

        _GridEdgeBias ("Grid Edge Bias", Range(0.0, 3.0)) = 0.75
        _GridEdgeBiasPower ("Grid Edge Bias Power", Range(0.25, 4.0)) = 1.5

        _MotionRate ("Motion Rate", Range(0.0, 1.0)) = 0.0

        [Header(Sky Overlay 0)]
        _SkyOverlayTex ("Sky Overlay 0 (equatorial equirect)", 2D) = "black" {}
        _SkyOverlayEnable ("Sky Overlay 0 Enable", Range(0.0, 1.0)) = 1.0
        _SkyOverlayColor ("Sky Overlay 0 Color", Color) = (0.65, 0.90, 1.0, 1.0)
        _SkyOverlayIntensity ("Sky Overlay 0 Intensity", Range(0.0, 8.0)) = 1.0
        _SkyOverlayAlpha ("Sky Overlay 0 Alpha", Range(0.0, 1.0)) = 1.0
        _SkyOverlaySoftness ("Sky Overlay 0 Softness", Range(0.0, 1.0)) = 0.05
        _SkyOverlayLonOffsetDeg ("Sky Overlay 0 Lon Offset Deg", Range(-360, 360)) = 0.0
        _SkyOverlayFlipU ("Sky Overlay 0 Flip U", Range(0.0, 1.0)) = 0.0
        _SkyOverlayFlipV ("Sky Overlay 0 Flip V", Range(0.0, 1.0)) = 0.0
        _SkyOverlayUseTextureAlpha ("Sky Overlay 0 Use Texture Alpha", Range(0.0, 1.0)) = 1.0
        _SkyOverlayLumaCut ("Sky Overlay 0 Luma Cut", Range(0.0, 1.0)) = 0.05

        [Header(Sky Overlay 1)]
        _SkyOverlayTex1 ("Sky Overlay 1 (equatorial equirect)", 2D) = "black" {}
        _SkyOverlayEnable1 ("Sky Overlay 1 Enable", Range(0.0, 1.0)) = 0.0
        _SkyOverlayColor1 ("Sky Overlay 1 Color", Color) = (1.0, 0.7, 0.2, 1.0)
        _SkyOverlayIntensity1 ("Sky Overlay 1 Intensity", Range(0.0, 8.0)) = 1.0
        _SkyOverlayAlpha1 ("Sky Overlay 1 Alpha", Range(0.0, 1.0)) = 1.0
        _SkyOverlaySoftness1 ("Sky Overlay 1 Softness", Range(0.0, 1.0)) = 0.05
        _SkyOverlayLonOffsetDeg1 ("Sky Overlay 1 Lon Offset Deg", Range(-360, 360)) = 0.0
        _SkyOverlayFlipU1 ("Sky Overlay 1 Flip U", Range(0.0, 1.0)) = 0.0
        _SkyOverlayFlipV1 ("Sky Overlay 1 Flip V", Range(0.0, 1.0)) = 0.0
        _SkyOverlayUseTextureAlpha1 ("Sky Overlay 1 Use Texture Alpha", Range(0.0, 1.0)) = 1.0
        _SkyOverlayLumaCut1 ("Sky Overlay 1 Luma Cut", Range(0.0, 1.0)) = 0.05

        [Header(Sky Overlay 2)]
        _SkyOverlayTex2 ("Sky Overlay 2 (equatorial equirect)", 2D) = "black" {}
        _SkyOverlayEnable2 ("Sky Overlay 2 Enable", Range(0.0, 1.0)) = 0.0
        _SkyOverlayColor2 ("Sky Overlay 2 Color", Color) = (0.4, 1.0, 0.6, 1.0)
        _SkyOverlayIntensity2 ("Sky Overlay 2 Intensity", Range(0.0, 8.0)) = 1.0
        _SkyOverlayAlpha2 ("Sky Overlay 2 Alpha", Range(0.0, 1.0)) = 1.0
        _SkyOverlaySoftness2 ("Sky Overlay 2 Softness", Range(0.0, 1.0)) = 0.05
        _SkyOverlayLonOffsetDeg2 ("Sky Overlay 2 Lon Offset Deg", Range(-360, 360)) = 0.0
        _SkyOverlayFlipU2 ("Sky Overlay 2 Flip U", Range(0.0, 1.0)) = 0.0
        _SkyOverlayFlipV2 ("Sky Overlay 2 Flip V", Range(0.0, 1.0)) = 0.0
        _SkyOverlayUseTextureAlpha2 ("Sky Overlay 2 Use Texture Alpha", Range(0.0, 1.0)) = 1.0
        _SkyOverlayLumaCut2 ("Sky Overlay 2 Luma Cut", Range(0.0, 1.0)) = 0.05

        // Keep exact name so SkyBoxDriver can write to it.
        _CraftBodyToEq ("Craft Body To Skybox Drive Quat (xyzw)", Vector) = (0,0,0,1)
        _ObliquityDeg ("Obliquity (deg)", Float) = 23.439281

        // Static overlay-frame correction only.
        _SkyOverlayFrameAdjust ("Sky Overlay Frame Adjust (xyzw)", Vector) = (0,0,0,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Back
        ZWrite Off

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vertGlass
            #pragma fragment fragGlass
            #include "UnityCG.cginc"

            sampler2D _NormalMap;
            sampler2D _DetailNormalMap;
            sampler2D _ImperfectionMask;

            float4 _GlassTint;
            float _BaseAlpha;

            float4 _FresnelColor;
            float _FresnelPower;
            float _FresnelStrength;

            float _ReflectionStrength;
            float _ReflectionFresnelBoost;

            float _NormalStrength;
            float _DetailNormalStrength;
            float _DetailTiling;

            float _ImperfectionStrength;
            float _EdgeDarken;
            float _AlphaFresnelBoost;

            float _VignetteStrength;
            float _VignetteStart;
            float _VignetteEnd;
            float _VignetteHardness;

            float4 _DarkFinalColor;
            float _DarkFinalAlpha;

            float _GridEnable;
            float _GridUVSelect;
            float _GridScale;
            float _GridLineWidth;
            float _GridRateWidthBoost;
            float _GridIntensity;
            float _GridRateIntensityBoost;
            float4 _GridColor;
            float _GridEdgeBias;
            float _GridEdgeBiasPower;
            float _MotionRate;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
                float2 uv1    : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float2 uv1      : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 worldNrm : TEXCOORD3;
            };

            float3 SafeNormalize(float3 v)
            {
                float len2 = dot(v, v);
                if (len2 < 1e-12) return float3(0,0,0);
                return v * rsqrt(len2);
            }

            float HexLineGrid(float2 uv, float scale, float lineWidth)
            {
                uv *= scale;

                float2 q = float2(
                    uv.x * 2.0 / 3.0,
                    (-uv.x + 2.0 * uv.y) * 0.57735027
                );

                float2 hex = float2(q.x, q.y);
                float2 f = frac(hex) - 0.5;

                float d1 = abs(f.x);
                float d2 = abs(f.y);
                float d3 = abs(f.x + f.y);
                float d = min(min(d1, d2), d3);

                return 1.0 - smoothstep(lineWidth, lineWidth * 1.5, d);
            }

            v2f vertGlass(appdata v)
            {
                v2f o;
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.uv       = v.uv;
                o.uv1      = v.uv1;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNrm = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 fragGlass(v2f i) : SV_Target
            {
                float3 V = SafeNormalize(_WorldSpaceCameraPos - i.worldPos);
                float3 N = SafeNormalize(i.worldNrm);

                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                float3 col = _GlassTint.rgb;
                col += _FresnelColor.rgb * fres * _FresnelStrength;

                float alpha = _BaseAlpha + fres * _AlphaFresnelBoost;

                float2 c = abs(i.uv1 - 0.5) * 2.0;
                float edge = max(c.x, c.y);
                float mask = saturate((edge - _VignetteStart) / max(1e-6, (_VignetteEnd - _VignetteStart)));
                float darkT = pow(mask, _VignetteHardness) * _VignetteStrength;

                col   = lerp(col,   _DarkFinalColor.rgb, darkT);
                alpha = lerp(alpha, _DarkFinalAlpha,     darkT);

                float2 gridUV = lerp(i.uv, i.uv1, step(0.5, _GridUVSelect));
                float rate = saturate(_MotionRate);

                float width = _GridLineWidth * (1.0 + rate * _GridRateWidthBoost);
                float g = HexLineGrid(gridUV, _GridScale, width);

                float intensity = _GridIntensity + rate * _GridRateIntensityBoost;
                intensity = saturate(intensity);
                intensity = lerp(intensity, sqrt(intensity), rate);

                float2 edgeUV = abs(i.uv1 - 0.5) * 2.0;
                float edgeDist = max(edgeUV.x, edgeUV.y);
                float edgeMask = saturate(edgeDist);
                float edgeBias = lerp(1.0, 1.0 + _GridEdgeBias, pow(edgeMask, _GridEdgeBiasPower));

                float gridAmount = g * intensity * edgeBias * _GridEnable;
                col += _GridColor.rgb * gridAmount;

                return float4(col, alpha);
            }
            ENDCG
        }

        Pass
        {
            Blend One One

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vertOverlay
            #pragma fragment fragOverlay
            #include "UnityCG.cginc"

            sampler2D _SkyOverlayTex;
            sampler2D _SkyOverlayTex1;
            sampler2D _SkyOverlayTex2;

            float _SkyOverlayEnable;
            float4 _SkyOverlayColor;
            float _SkyOverlayIntensity;
            float _SkyOverlayAlpha;
            float _SkyOverlaySoftness;
            float _SkyOverlayLonOffsetDeg;
            float _SkyOverlayFlipU;
            float _SkyOverlayFlipV;
            float _SkyOverlayUseTextureAlpha;
            float _SkyOverlayLumaCut;

            float _SkyOverlayEnable1;
            float4 _SkyOverlayColor1;
            float _SkyOverlayIntensity1;
            float _SkyOverlayAlpha1;
            float _SkyOverlaySoftness1;
            float _SkyOverlayLonOffsetDeg1;
            float _SkyOverlayFlipU1;
            float _SkyOverlayFlipV1;
            float _SkyOverlayUseTextureAlpha1;
            float _SkyOverlayLumaCut1;

            float _SkyOverlayEnable2;
            float4 _SkyOverlayColor2;
            float _SkyOverlayIntensity2;
            float _SkyOverlayAlpha2;
            float _SkyOverlaySoftness2;
            float _SkyOverlayLonOffsetDeg2;
            float _SkyOverlayFlipU2;
            float _SkyOverlayFlipV2;
            float _SkyOverlayUseTextureAlpha2;
            float _SkyOverlayLumaCut2;

            float4 _CraftBodyToEq;
            float _ObliquityDeg;
            float4 _SkyOverlayFrameAdjust;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 localPos : TEXCOORD1;
            };

            float3 SafeNormalize(float3 v)
            {
                float len2 = dot(v, v);
                if (len2 < 1e-12) return float3(0,0,0);
                return v * rsqrt(len2);
            }

            float3 RotateByQuat(float3 v, float4 q)
            {
                float3 u = q.xyz;
                float s = q.w;
                float3 uv = cross(u, v);
                float3 uuv = cross(u, uv);
                return v + 2.0 * (s * uv + uuv);
            }

            float3 EclToEq(float3 vEcl, float epsDeg)
            {
                float eps = epsDeg * 0.017453292519943295;
                float c = cos(eps);
                float s = sin(eps);

                return float3(
                    vEcl.x,
                    vEcl.y * c - vEcl.z * s,
                    vEcl.y * s + vEcl.z * c
                );
            }

            float2 DirToEquatorialEquirectUV(float3 dirEq, float lonOffsetDeg, float flipU, float flipV)
            {
                float lon = atan2(dirEq.x, dirEq.y);
                float lat = asin(clamp(dirEq.z, -1.0, 1.0));

                float u = lon * 0.15915494309189535 + 0.5;
                float v = lat * 0.3183098861837907 + 0.5;

                u += lonOffsetDeg / 360.0;
                u = frac(u);

                if (flipU > 0.5) u = 1.0 - u;
                if (flipV > 0.5) v = 1.0 - v;

                return float2(u, v);
            }

            float3 EvalOverlayLayer(
                sampler2D texLayer,
                float enableLayer,
                float4 colorLayer,
                float intensityLayer,
                float alphaLayer,
                float softnessLayer,
                float lonOffsetDegLayer,
                float flipULayer,
                float flipVLayer,
                float useTextureAlphaLayer,
                float lumaCutLayer,
                float3 dirEqOverlay
            )
            {
                if (enableLayer < 0.5) return float3(0,0,0);

                float2 skyUV = DirToEquatorialEquirectUV(dirEqOverlay, lonOffsetDegLayer, flipULayer, flipVLayer);
                float4 overlayTex = tex2D(texLayer, skyUV);

                float overlayLuma = dot(overlayTex.rgb, float3(0.299, 0.587, 0.114));
                float overlayMaskFromLuma = smoothstep(
                    lumaCutLayer,
                    lumaCutLayer + max(1e-5, softnessLayer),
                    overlayLuma
                );

                float useTexAlpha = step(0.5, useTextureAlphaLayer);
                float overlayMask = lerp(overlayMaskFromLuma, overlayTex.a, useTexAlpha);
                float overlayA = saturate(overlayMask * alphaLayer);

                return overlayTex.rgb * colorLayer.rgb * intensityLayer * overlayA;
            }

            v2f vertOverlay(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 fragOverlay(v2f i) : SV_Target
            {
                float3 camLocal = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1.0)).xyz;
                float3 dirLocal = SafeNormalize(i.localPos - camLocal);

                // Keep motion chain fixed
                float3 dirBodySky = SafeNormalize(float3(-dirLocal.x, dirLocal.y, dirLocal.z));
                float3 dirEcl = SafeNormalize(RotateByQuat(dirBodySky, _CraftBodyToEq));
                float3 dirEq = SafeNormalize(EclToEq(dirEcl, _ObliquityDeg));

                // Static overlay-frame correction only
                float3 dirEqOverlay = SafeNormalize(RotateByQuat(dirEq, _SkyOverlayFrameAdjust));

                float3 rgb = float3(0,0,0);

                rgb += EvalOverlayLayer(
                    _SkyOverlayTex,
                    _SkyOverlayEnable,
                    _SkyOverlayColor,
                    _SkyOverlayIntensity,
                    _SkyOverlayAlpha,
                    _SkyOverlaySoftness,
                    _SkyOverlayLonOffsetDeg,
                    _SkyOverlayFlipU,
                    _SkyOverlayFlipV,
                    _SkyOverlayUseTextureAlpha,
                    _SkyOverlayLumaCut,
                    dirEqOverlay
                );

                rgb += EvalOverlayLayer(
                    _SkyOverlayTex1,
                    _SkyOverlayEnable1,
                    _SkyOverlayColor1,
                    _SkyOverlayIntensity1,
                    _SkyOverlayAlpha1,
                    _SkyOverlaySoftness1,
                    _SkyOverlayLonOffsetDeg1,
                    _SkyOverlayFlipU1,
                    _SkyOverlayFlipV1,
                    _SkyOverlayUseTextureAlpha1,
                    _SkyOverlayLumaCut1,
                    dirEqOverlay
                );

                rgb += EvalOverlayLayer(
                    _SkyOverlayTex2,
                    _SkyOverlayEnable2,
                    _SkyOverlayColor2,
                    _SkyOverlayIntensity2,
                    _SkyOverlayAlpha2,
                    _SkyOverlaySoftness2,
                    _SkyOverlayLonOffsetDeg2,
                    _SkyOverlayFlipU2,
                    _SkyOverlayFlipV2,
                    _SkyOverlayUseTextureAlpha2,
                    _SkyOverlayLumaCut2,
                    dirEqOverlay
                );

                return float4(rgb, 0.0);
            }
            ENDCG
        }
    }
}