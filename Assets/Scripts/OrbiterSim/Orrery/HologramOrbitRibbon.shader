Shader "Orbiter/HologramOrbitRibbon"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.9, 1.0, 1.0)
        _Intensity ("Intensity", Float) = 2.0
        _Alpha ("Alpha", Range(0,1)) = 0.8

        _RibbonWidthWorld ("Ribbon Width World", Float) = 0.004

        _EdgeSoftness ("Edge Softness", Float) = 2.0
        _CenterBoost ("Center Boost", Float) = 1.5

        _DashEnable ("Dash Enable", Float) = 0
        _DashCount ("Dash Count", Float) = 24.0
        _DashDuty ("Dash Duty", Range(0.05,1)) = 0.65
        _DashScroll ("Dash Scroll", Float) = 0.0

        _FlowEnable ("Flow Enable", Float) = 1
        _FlowSpeed ("Flow Speed", Float) = 0.4
        _FlowStrength ("Flow Strength", Float) = 0.2

        _CraftU ("Craft Orbit U", Range(0,1)) = 0.0
        _ProgradeFadeStrength ("Prograde Fade Strength", Range(0,1)) = 0.45
        _RetroMinBrightness ("Retro Min Brightness", Range(0,1)) = 0.55

        _FlickerStrength ("Flicker Strength", Range(0,1)) = 0.06
        _FlickerSpeed ("Flicker Speed", Float) = 7.0

        _StencilRef ("Stencil Ref", Float) = 64
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Lighting Off

        Stencil
        {
            Ref [_StencilRef]
            Comp Equal
            Pass Keep
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION; // centerline position in object space
                float3 normal : NORMAL;   // tangent in object space
                float2 uv     : TEXCOORD0; // x=along orbit, y=side sign encoded 0/1
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            fixed4 _Color;
            float _Intensity;
            float _Alpha;

            float _RibbonWidthWorld;

            float _EdgeSoftness;
            float _CenterBoost;

            float _DashEnable;
            float _DashCount;
            float _DashDuty;
            float _DashScroll;

            float _FlowEnable;
            float _FlowSpeed;
            float _FlowStrength;

            float _CraftU;
            float _ProgradeFadeStrength;
            float _RetroMinBrightness;

            float _FlickerStrength;
            float _FlickerSpeed;

            float WrappedDistance01(float a, float b)
            {
                float d = abs(a - b);
                return min(d, 1.0 - d);
            }

            v2f vert(appdata v)
            {
                v2f o;

                // Center point in world
                float3 worldCenter = mul(unity_ObjectToWorld, v.vertex).xyz;

                // Tangent in world
                float3 tangentW = normalize(UnityObjectToWorldNormal(v.normal));

                // View direction
                float3 viewDirW = normalize(_WorldSpaceCameraPos.xyz - worldCenter);

                // Camera-facing side direction
                float3 sideW = cross(viewDirW, tangentW);
                float sideLen = length(sideW);

                // Fallback if viewDir and tangent nearly parallel
                if (sideLen < 1e-5)
                {
                    float3 upFallback = float3(0,1,0);
                    sideW = cross(upFallback, tangentW);
                    sideLen = length(sideW);

                    if (sideLen < 1e-5)
                    {
                        float3 rightFallback = float3(1,0,0);
                        sideW = cross(rightFallback, tangentW);
                        sideLen = max(length(sideW), 1e-5);
                    }
                }

                sideW /= sideLen;

                float sideSign = lerp(-1.0, 1.0, v.uv.y);
                float3 worldPos = worldCenter + sideSign * 0.5 * _RibbonWidthWorld * sideW;

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Across-width profile
                float edge = abs(i.uv.y * 2.0 - 1.0); // 0 center, 1 edges
                float widthFade = saturate(1.0 - pow(edge, _EdgeSoftness));
                widthFade *= lerp(1.0, _CenterBoost, 1.0 - edge);

                // Dash
                float dashMask = 1.0;
                if (_DashEnable > 0.5)
                {
                    float u = frac(i.uv.x * _DashCount + _DashScroll * _Time.y);
                    dashMask = step(u, _DashDuty);
                }

                // Flow shimmer
                float flow = 1.0;
                if (_FlowEnable > 0.5)
                {
                    flow += _FlowStrength * sin((i.uv.x - _Time.y * _FlowSpeed) * 40.0);
                }

                // Prograde bright, retro dim
                float dWrap = WrappedDistance01(i.uv.x, _CraftU);
                float phase = saturate(dWrap / 0.5);
                float aroundFade = 0.5 + 0.5 * cos(phase * UNITY_PI);
                aroundFade = lerp(1.0, aroundFade, _ProgradeFadeStrength);
                aroundFade = max(aroundFade, _RetroMinBrightness);

                // Subtle holo flicker
                float flicker = 1.0 + _FlickerStrength *
                    sin(_Time.y * _FlickerSpeed + i.uv.x * 31.7 + i.uv.y * 11.3);

                float a = _Alpha * widthFade * dashMask;
                fixed3 rgb = _Color.rgb * _Intensity * widthFade * dashMask * flow * aroundFade * flicker;

                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}