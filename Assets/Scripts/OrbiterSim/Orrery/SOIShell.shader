Shader "Orbiter/HologramSOIShell"
{
    Properties
    {
        _Color ("Color", Color) = (0.20, 0.95, 1.00, 1.0)
        _Intensity ("Intensity", Float) = 1.0
        _Alpha ("Alpha", Range(0,1)) = 0.22

        _FresnelPower ("Fresnel Power", Float) = 4.0
        _FresnelStrength ("Fresnel Strength", Float) = 1.35

        _LineAlphaBoost ("Line Alpha Boost", Range(0,2)) = 0.55
        _FillStrength ("Fill Strength", Range(0,1)) = 0.08

        _LongitudeCount ("Longitude Count", Float) = 18.0
        _LatitudeCount ("Latitude Count", Float) = 9.0
        _LineWidth ("Line Width", Range(0.001,0.25)) = 0.045

        _DashRepeat ("Dash Repeat", Float) = 32.0
        _DashDuty ("Dash Duty", Range(0.05,0.95)) = 0.52
        _DashSoftness ("Dash Softness", Range(0.001,0.25)) = 0.05
        _DashScroll ("Dash Scroll", Float) = 0.10

        _SweepStrength ("Sweep Strength", Range(0,1)) = 0.20
        _SweepSpeed ("Sweep Speed", Float) = 0.20
        _SweepSharpness ("Sweep Sharpness", Float) = 8.0

        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.05
        _NoiseScale ("Noise Scale", Float) = 10.0
        _NoiseScroll ("Noise Scroll", Float) = 0.08

        _ClipFeather ("Clip Feather", Float) = 0.01

        _ClipCenterWorld ("Clip Center World", Vector) = (0,0,0,0)
        _ClipRadiusWorld ("Clip Radius World", Float) = 1.0

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

            fixed4 _Color;
            float _Intensity;
            float _Alpha;

            float _FresnelPower;
            float _FresnelStrength;

            float _LineAlphaBoost;
            float _FillStrength;

            float _LongitudeCount;
            float _LatitudeCount;
            float _LineWidth;

            float _DashRepeat;
            float _DashDuty;
            float _DashSoftness;
            float _DashScroll;

            float _SweepStrength;
            float _SweepSpeed;
            float _SweepSharpness;

            float _NoiseStrength;
            float _NoiseScale;
            float _NoiseScroll;

            float _ClipFeather;
            float4 _ClipCenterWorld;
            float _ClipRadiusWorld;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldN   : TEXCOORD1;
                float3 localPos : TEXCOORD2;
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

            float linePulse(float coord, float count, float width)
            {
                // repeating signed-distance-ish line field around periodic coordinate
                float x = frac(coord * count);
                float d = abs(x - 0.5) * 2.0;   // 0 at line center, 1 between lines
                return 1.0 - smoothstep(0.0, width, d);
            }

            float dashMask(float alongCoord)
            {
                float u = frac(alongCoord * _DashRepeat + _Time.y * _DashScroll);
                return 1.0 - smoothstep(_DashDuty, _DashDuty + _DashSoftness, u);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldN = UnityObjectToWorldNormal(v.normal);
                o.localPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // -----------------------------
                // World-space clip sphere
                // -----------------------------
                float clipR = max(_ClipRadiusWorld, 1e-5);
                float distToClipCenter = distance(i.worldPos, _ClipCenterWorld.xyz);

                // hard reject outside clip sphere
                if (distToClipCenter > clipR)
                    discard;

                // optional soft fade near clip boundary
                float clipFade = 1.0;
                if (_ClipFeather > 1e-5)
                {
                    float edgeDist = clipR - distToClipCenter;
                    clipFade = saturate(edgeDist / _ClipFeather);
                }

                // -----------------------------
                // View terms
                // -----------------------------
                float3 N = normalize(i.worldN);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);

                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float fresTerm = fres * _FresnelStrength;

                // -----------------------------
                // Sphere parameterization
                // local sphere assumed centered at origin
                // -----------------------------
                float3 p = normalize(i.localPos);

                // longitude: -pi..pi
                float lon = atan2(p.z, p.x);
                float lon01 = frac(lon / (2.0 * UNITY_PI) + 0.5);

                // latitude as normalized 0..1 from south to north
                float lat01 = asin(clamp(p.y, -1.0, 1.0)) / UNITY_PI + 0.5;

                // -----------------------------
                // Dashed longitude lines
                // each longitude line dashes along latitude
                // -----------------------------
                float lonLines = linePulse(lon01, _LongitudeCount, _LineWidth);
                float lonDash  = dashMask(lat01);
                float lonField = lonLines * lonDash;

                // -----------------------------
                // Dashed latitude lines
                // each latitude line dashes along longitude
                // -----------------------------
                float latLines = linePulse(lat01, _LatitudeCount, _LineWidth);
                float latDash  = dashMask(lon01 + 0.37); // phase offset so both fields do not line up
                float latField = latLines * latDash;

                float lineField = saturate(lonField + latField);

                // faint shell fill so it reads as a volume
                float fillField = _FillStrength;

                // slow sweep in latitude
                float sweepCenter = frac(_Time.y * _SweepSpeed) * 2.0 - 1.0;
                float sweepDist = abs(p.y - sweepCenter);
                float sweep = saturate(1.0 - sweepDist * _SweepSharpness) * _SweepStrength;

                // subtle procedural modulation
                float n = noise3(p * _NoiseScale + float3(0.0, _Time.y * _NoiseScroll, 0.0));
                float noiseMod = lerp(1.0, n, _NoiseStrength);

                // brighten shell toward rim and on lines
                float field =
                    (fillField + lineField + fresTerm + sweep) *
                    noiseMod;

                fixed3 rgb = _Color.rgb * _Intensity * field;

                float a =
                    _Alpha *
                    (fillField + lineField * _LineAlphaBoost + 0.45 * fres + 0.20 * sweep);

                a *= clipFade;
                a = saturate(a);

                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}