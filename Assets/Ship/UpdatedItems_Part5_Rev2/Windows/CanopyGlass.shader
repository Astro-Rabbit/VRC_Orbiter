Shader "Orbiter/CanopyGlass_Base"
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
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _NormalMap;
            sampler2D _DetailNormalMap;
            sampler2D _ImperfectionMask;

            float4 _NormalMap_ST;
            float4 _DetailNormalMap_ST;
            float4 _ImperfectionMask_ST;

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

            struct appdata {
                float4 vertex:POSITION;
                float3 normal:NORMAL;
                float2 uv:TEXCOORD0;
                float2 uv1:TEXCOORD1;
            };

            struct v2f {
                float4 pos:SV_POSITION;
                float2 uv:TEXCOORD0;
                float2 uv1:TEXCOORD1;
                float3 worldPos:TEXCOORD2;
                float3 normal:TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uv1 = v.uv1;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float HexLineGrid(float2 uv, float scale, float lineWidth)
            {
                uv *= scale;

                // convert to hex grid space (pointy-top layout)
                float2 q = float2(
                    uv.x * 2.0/3.0,
                    (-uv.x + 2.0 * uv.y) * 0.57735027
                );

                float2 hex = float2(q.x, q.y);
                float2 cell = floor(hex);
                float2 f = frac(hex) - 0.5;

                // distance to 3 hex edge directions
                float d1 = abs(f.x);
                float d2 = abs(f.y);
                float d3 = abs(f.x + f.y);

                float d = min(min(d1, d2), d3);

                // line mask
                return 1.0 - smoothstep(lineWidth, lineWidth * 1.5, d);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 N = normalize(i.normal);

                float fres = pow(1 - saturate(dot(N,V)), _FresnelPower);

                float3 col = _GlassTint.rgb;
                col += _FresnelColor.rgb * fres * _FresnelStrength;

                float alpha = _BaseAlpha + fres * _AlphaFresnelBoost;

                // --- vignette ---
                float2 c = abs(i.uv1 - 0.5)*2;
                float edge = max(c.x,c.y);
                float mask = saturate((edge - _VignetteStart)/(_VignetteEnd - _VignetteStart));
                float t = pow(mask,_VignetteHardness) * _VignetteStrength;

                // --- dark replace ---
                col = lerp(col, _DarkFinalColor.rgb, t);
                alpha = lerp(alpha, _DarkFinalAlpha, t);

                // --- GRID (independent system) ---
                float2 gridUV = lerp(i.uv, i.uv1, step(0.5, _GridUVSelect));

                float rate = saturate(_MotionRate);

                // thickness scaling
                float width = _GridLineWidth * (1.0 + rate * _GridRateWidthBoost);

                // proper hex lines
                float g = HexLineGrid(gridUV, _GridScale, width);

                // intensity scaling
                float intensity = _GridIntensity + rate * _GridRateIntensityBoost;
                intensity = saturate(intensity);
                intensity = lerp(intensity, sqrt(intensity), rate);

                // independent edge bias (NOT vignette)
                float2 edgeUV = abs(i.uv1 - 0.5) * 2.0;
                float edgeDist = max(edgeUV.x, edgeUV.y);
                float edgeMask = saturate(edgeDist);

                float edgeBias = lerp(1.0, 1.0 + _GridEdgeBias, pow(edgeMask, _GridEdgeBiasPower));

                // final grid
                float gridAmount = g * intensity * edgeBias * _GridEnable;

                // embed into glass
                col += _GridColor.rgb * gridAmount;

                return float4(col, alpha);
            }
            ENDCG
        }
    }
}