Shader "Orbiter/HologramMarker"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 1.0, 0.9, 1.0)
        _Intensity ("Intensity", Float) = 2.0
        _Alpha ("Alpha", Range(0,1)) = 0.85

        _CenterFill ("Center Fill", Range(0,2)) = 0.60

        _FresnelPower ("Fresnel Power", Float) = 2.5
        _FresnelStrength ("Fresnel Strength", Float) = 1.2

        _ScanlineDensity ("Scanline Density", Float) = 55.0
        _ScanlineStrength ("Scanline Strength", Range(0,1)) = 0.06
        _ScanlineScroll ("Scanline Scroll", Float) = 0.20

        _FlickerStrength ("Flicker Strength", Range(0,1)) = 0.03
        _FlickerSpeed ("Flicker Speed", Float) = 7.0

        _PulseEnable ("Pulse Enable", Float) = 0
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _PulseStrength ("Pulse Strength", Range(0,1)) = 0.15

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

            fixed4 _Color;
            float _Intensity;
            float _Alpha;

            float _CenterFill;

            float _FresnelPower;
            float _FresnelStrength;

            float _ScanlineDensity;
            float _ScanlineStrength;
            float _ScanlineScroll;

            float _FlickerStrength;
            float _FlickerSpeed;

            float _PulseEnable;
            float _PulseSpeed;
            float _PulseStrength;

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
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldN = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 clipDelta = i.worldPos - _ClipCenterWorld.xyz;
                clip(_ClipRadiusWorld - length(clipDelta));

                float3 N = normalize(i.worldN);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);

                float fres = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
                float edgeGlow = 1.0 + fres * _FresnelStrength;

                float fill = _CenterFill;

                float scan = sin((i.worldPos.y + _Time.y * _ScanlineScroll) * _ScanlineDensity) * 0.5 + 0.5;
                float scanMod = lerp(1.0, scan, _ScanlineStrength);

                float flicker = 1.0 + _FlickerStrength *
                    sin(_Time.y * _FlickerSpeed + i.worldPos.x * 8.3 + i.worldPos.y * 4.7 + i.worldPos.z * 6.9);

                float pulse = 1.0;
                if (_PulseEnable > 0.5)
                {
                    pulse += _PulseStrength * sin(_Time.y * _PulseSpeed * 6.2831853);
                }

                fixed3 rgb = _Color.rgb * _Intensity * (fill + edgeGlow) * scanMod * flicker * pulse;
                float a = _Alpha * saturate(fill + 0.5 * fres) * pulse;

                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}