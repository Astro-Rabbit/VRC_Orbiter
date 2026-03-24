Shader "Orbiter/Debug/GroundTrackGpuQuatPreview"
{
    Properties
    {
        _QuatTex ("Quat Texture", 2D) = "white" {}
        _SampleCount ("Sample Count", Float) = 1
        _DebugGain ("Debug Gain", Float) = 64
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _QuatTex;
            float _SampleCount;
            float _DebugGain;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float2 SampleUVFromIndex(float idx, float count)
            {
                float u = (idx + 0.5) / max(count, 1.0);
                return float2(u, 0.5);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float count = max(_SampleCount, 1.0);
                float idx = floor(saturate(i.uv.x) * count);
                idx = min(idx, count - 1.0);

                float2 suv = SampleUVFromIndex(idx, count);
                float4 q = tex2D(_QuatTex, suv);

                float3 rgb = saturate(abs(q.xyz) * _DebugGain);

                return float4(rgb, 1.0);
            }
            ENDCG
        }
    }
}