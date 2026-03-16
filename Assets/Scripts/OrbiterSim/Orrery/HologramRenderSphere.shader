Shader "Orbiter/HologramRenderSphere"
{
    Properties
    {
        _Color ("Color", Color) = (0.20, 0.95, 1.00, 1.0)
        _Intensity ("Intensity", Float) = 1.0
        _Alpha ("Alpha", Range(0,1)) = 0.18

        _FresnelPower ("Fresnel Power", Float) = 3.5
        _FresnelStrength ("Fresnel Strength", Float) = 1.2

        _BaseGlowStrength ("Base Glow Strength", Float) = 1.2
        _BaseGlowPower ("Base Glow Power", Float) = 1.8
        _TopFadeStrength ("Top Fade Strength", Range(0,1)) = 0.35

        _VerticalBandDensity ("Vertical Band Density", Float) = 26.0
        _VerticalBandStrength ("Vertical Band Strength", Range(0,1)) = 0.12
        _VerticalBandScroll ("Vertical Band Scroll", Float) = 0.20

        _SweepStrength ("Sweep Strength", Range(0,1)) = 0.20
        _SweepSpeed ("Sweep Speed", Float) = 0.35
        _SweepSharpness ("Sweep Sharpness", Float) = 5.0

        _MeridianDensity ("Meridian Density", Float) = 18.0
        _MeridianStrength ("Meridian Strength", Range(0,1)) = 0.06

        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.08
        _NoiseScale ("Noise Scale", Float) = 8.0
        _NoiseScroll ("Noise Scroll", Float) = 0.12

        _FlickerStrength ("Flicker Strength", Range(0,1)) = 0.03
        _FlickerSpeed ("Flicker Speed", Float) = 5.0

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

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Lighting Off

        // Usually for the visible shell we do NOT want to require stencil equality,
        // because this shell itself is the visible boundary.
        // But if you want it confined to an outer stencil volume, you can enable this.
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

            fixed4 _Color;
            float _Intensity;
            float _Alpha;

            float _FresnelPower;
            float _FresnelStrength;

            float _BaseGlowStrength;
            float _BaseGlowPower;
            float _TopFadeStrength;

            float _VerticalBandDensity;
            float _VerticalBandStrength;
            float _VerticalBandScroll;

            float _SweepStrength;
            float _SweepSpeed;
            float _SweepSharpness;

            float _MeridianDensity;
            float _MeridianStrength;

            float _NoiseStrength;
            float _NoiseScale;
            float _NoiseScroll;

            float _FlickerStrength;
            float _FlickerSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos        : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
                float3 worldN     : TEXCOORD1;
                float3 localPos   : TEXCOORD2;
                float3 localN     : TEXCOORD3;
            };

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);

                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash31(i + float3(0,0,0));
                float n100 = hash31(i + float3(1,0,0));
                float n010 = hash31(i + float3(0,1,0));
                float n110 = hash31(i + float3(1,1,0));
                float n001 = hash31(i + float3(0,0,1));
                float n101 = hash31(i + float3(1,0,1));
                float n011 = hash31(i + float3(0,1,1));
                float n111 = hash31(i + float3(1,1,1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldN = UnityObjectToWorldNormal(v.normal);
                o.localPos = v.vertex.xyz;
                o.localN = v.normal;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 N = normalize(i.worldN);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);

                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float fresTerm = fres * _FresnelStrength;

                // Sphere local normalized direction
                float3 pLocal = i.localPos;
                float pLen = max(length(pLocal), 1e-5);
                float3 pDir = pLocal / pLen;

                // Assume local +Y is "up" for the sphere.
                // bottom hemisphere => pDir.y near -1
                float bottom01 = saturate((-pDir.y + 1.0) * 0.5);
                float top01 = saturate((pDir.y + 1.0) * 0.5);

                float baseGlow = pow(bottom01, _BaseGlowPower) * _BaseGlowStrength;
                float topFade = lerp(1.0, 1.0 - top01, _TopFadeStrength);

                // Vertical animated bands rising upward
                float bandPhase = (pDir.y + _Time.y * _VerticalBandScroll) * _VerticalBandDensity;
                float bands = sin(bandPhase) * 0.5 + 0.5;
                bands = lerp(1.0, bands, _VerticalBandStrength);

                // Upward sweep / refresh wave
                float sweepCenter = frac(_Time.y * _SweepSpeed) * 2.0 - 1.0; // -1..1
                float sweepDist = abs(pDir.y - sweepCenter);
                float sweep = saturate(1.0 - sweepDist * _SweepSharpness);
                sweep *= _SweepStrength;

                // Meridian / longitude field structure
                float meridianAngle = atan2(pDir.z, pDir.x);
                float meridians = cos(meridianAngle * _MeridianDensity) * 0.5 + 0.5;
                meridians = lerp(1.0, meridians, _MeridianStrength);

                // Procedural field noise
                float n = noise3(pDir * _NoiseScale + float3(0.0, _Time.y * _NoiseScroll, 0.0));
                float noiseMod = lerp(1.0, n, _NoiseStrength);

                // Subtle global flicker
                float flicker = 1.0 + _FlickerStrength *
                    sin(_Time.y * _FlickerSpeed + i.worldPos.x * 3.1 + i.worldPos.y * 2.3 + i.worldPos.z * 2.7);

                float field = (0.25 + baseGlow + fresTerm + sweep) * bands * meridians * noiseMod * topFade;

                fixed3 rgb = _Color.rgb * _Intensity * field * flicker;

                float a = _Alpha * saturate(0.20 + 0.55 * fres + 0.35 * baseGlow + 0.25 * sweep);
                a *= topFade;

                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}