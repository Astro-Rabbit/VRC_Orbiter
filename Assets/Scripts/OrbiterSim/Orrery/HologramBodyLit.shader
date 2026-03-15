Shader "Orbiter/HologramBodyLit"
{
    Properties
    {
        _MainTex ("Day Texture", 2D) = "white" {}
        _NightTex ("Night Texture", 2D) = "black" {}

        _Color ("Body Tint", Color) = (1,1,1,1)
        _Intensity ("Base Intensity", Float) = 1.0
        _Alpha ("Alpha", Range(0,1)) = 0.5

        _Ambient ("Ambient Floor", Range(0,1)) = 0.08
        _DiffuseStrength ("Diffuse Strength", Float) = 1.0
        _NightIntensity ("Night Intensity", Float) = 1.0
        _NightThreshold ("Night Threshold", Range(0,1)) = 0.2
        _NightSharpness ("Night Sharpness", Float) = 2.0

        _LongitudeOffset ("Longitude Offset", Range(-1,1)) = 0.0

        _FresnelPower ("Fresnel Power", Float) = 3.0
        _FresnelStrength ("Fresnel Strength", Float) = 0.6

        _ScanlineDensity ("Scanline Density", Float) = 40.0
        _ScanlineStrength ("Scanline Strength", Range(0,1)) = 0.08
        _ScanlineScroll ("Scanline Scroll", Float) = 0.15

        _FlickerStrength ("Flicker Strength", Range(0,1)) = 0.04
        _FlickerSpeed ("Flicker Speed", Float) = 6.0

        _StencilRef ("Stencil Ref", Float) = 64

        _ClipCenterWorld ("Clip Center World", Vector) = (0,0,0,0)
        _ClipRadiusWorld ("Clip Radius World", Float) = 1.0
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

            sampler2D _MainTex;
            sampler2D _NightTex;
            float4 _MainTex_ST;
            float4 _NightTex_ST;

            fixed4 _Color;
            float _Intensity;
            float _Alpha;

            float _Ambient;
            float _DiffuseStrength;
            float _NightIntensity;
            float _NightThreshold;
            float _NightSharpness;

            float _LongitudeOffset;

            float _FresnelPower;
            float _FresnelStrength;

            float _ScanlineDensity;
            float _ScanlineStrength;
            float _ScanlineScroll;

            float _FlickerStrength;
            float _FlickerSpeed;

            float4 _SunDirWorld; // set from script, xyz used

            float4 _ClipCenterWorld;
            float _ClipRadiusWorld;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldN   : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldN = UnityObjectToWorldNormal(v.normal);

                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                uv.x = uv.x + _LongitudeOffset;
                o.uv = uv;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 clipDelta = i.worldPos - _ClipCenterWorld.xyz;
                clip(_ClipRadiusWorld - length(clipDelta));

                float3 N = normalize(i.worldN);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 L = normalize(_SunDirWorld.xyz);

                float ndl = saturate(dot(N, L));

                float dayLight = _Ambient + _DiffuseStrength * ndl;

                fixed3 dayTex = tex2D(_MainTex, i.uv).rgb;
                fixed3 nightTex = tex2D(_NightTex, i.uv).rgb;

                fixed3 litDay = dayTex * _Color.rgb * _Intensity * dayLight;

                float nightBase = saturate(1.0 - ndl);
                float nightMask = saturate((nightBase - _NightThreshold) / max(1e-4, 1.0 - _NightThreshold));
                nightMask = pow(nightMask, _NightSharpness);

                fixed3 litNight = nightTex * _NightIntensity * nightMask;

                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float fresBoost = 1.0 + fres * _FresnelStrength;

                float scan = sin((i.worldPos.y + _Time.y * _ScanlineScroll) * _ScanlineDensity) * 0.5 + 0.5;
                float scanMod = lerp(1.0, scan, _ScanlineStrength);

                float flicker = 1.0 + _FlickerStrength *
                    sin(_Time.y * _FlickerSpeed + i.worldPos.x * 9.7 + i.worldPos.y * 5.3 + i.worldPos.z * 7.1);

                fixed3 rgb = (litDay + litNight) * fresBoost * scanMod * flicker;

                float a = _Alpha * saturate(0.85 + 0.15 * fres);

                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}