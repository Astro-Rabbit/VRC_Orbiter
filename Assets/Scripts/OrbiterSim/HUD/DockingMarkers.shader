Shader "HUD/DockStencilObject"
{
    Properties
    {
        _Color ("Color", Color) = (0.45, 1.0, 0.55, 1.0)
        _Intensity ("Intensity", Float) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One One

        Stencil
        {
            Ref 1
            Comp Equal
            Pass Keep
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(_Color.rgb * _Intensity, 1.0);
            }
            ENDCG
        }
    }
}