Shader "Orbiter/MFD/GroundTrackMap_SegmentTexture"
{
    Properties
    {
        _MainTex ("Map Texture", 2D) = "white" {}
        _Color ("Map Tint", Color) = (1,1,1,1)

        _TrackColor ("Track Color", Color) = (0,1,0,1)
        _TrackWidth ("Track Width UV", Range(0.0005, 0.05)) = 0.004
        _TrackSoftness ("Track Softness UV", Range(0.0001, 0.02)) = 0.0015

        _MarkerColor ("Marker Color", Color) = (1,1,0,1)
        _MarkerRadius ("Marker Radius UV", Range(0.001, 0.05)) = 0.01
        _MarkerSoftness ("Marker Softness UV", Range(0.0001, 0.02)) = 0.002

        _SegTex ("Segment Texture", 2D) = "black" {}
        _SegCount ("Segment Count", Float) = 0
        _SegTexWidth ("Segment Texture Width", Float) = 1

        _CurrentPoint ("Current Point UV", Vector) = (0.5,0.5,0,0)
        _HasCurrentPoint ("Has Current Point", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _SegTex;

            fixed4 _Color;
            fixed4 _TrackColor;
            float _TrackWidth;
            float _TrackSoftness;

            fixed4 _MarkerColor;
            float _MarkerRadius;
            float _MarkerSoftness;

            float _SegCount;
            float _SegTexWidth;

            float4 _CurrentPoint;
            float _HasCurrentPoint;

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
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 GetSegment(int idx)
            {
                float u = ((float)idx + 0.5) / max(_SegTexWidth, 1.0);
                return tex2D(_SegTex, float2(u, 0.5));
            }

            float DistPointToSegmentWrapped(float2 p, float2 a, float2 b)
            {
                float best = 1e9;

                [unroll]
                for (int sx = -1; sx <= 1; sx++)
                {
                    float2 pp = p + float2((float)sx, 0.0);
                    float2 ab = b - a;
                    float ab2 = dot(ab, ab);

                    float t = 0.0;
                    if (ab2 > 1e-12)
                        t = saturate(dot(pp - a, ab) / ab2);

                    float2 q = a + t * ab;
                    best = min(best, length(pp - q));
                }

                return best;
            }

            float DistPointToCircleWrapped(float2 p, float2 c)
            {
                float best = 1e9;

                [unroll]
                for (int sx = -1; sx <= 1; sx++)
                {
                    float2 pp = p + float2((float)sx, 0.0);
                    best = min(best, length(pp - c));
                }

                return best;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv) * _Color;

                float lineAlpha = 0.0;

                // Hard cap for shader loop. Driver can send fewer.
                [unroll]
                for (int s = 0; s < 128; s++)
                {
                    if (s >= (int)_SegCount) break;

                    float4 seg = GetSegment(s);
                    float2 a = seg.xy;
                    float2 b = seg.zw;

                    float d = DistPointToSegmentWrapped(i.uv, a, b);
                    float m = 1.0 - smoothstep(_TrackWidth, _TrackWidth + _TrackSoftness, d);
                    lineAlpha = max(lineAlpha, m);
                }

                float markerAlpha = 0.0;
                if (_HasCurrentPoint > 0.5)
                {
                    float dmk = DistPointToCircleWrapped(i.uv, _CurrentPoint.xy);
                    markerAlpha = 1.0 - smoothstep(_MarkerRadius, _MarkerRadius + _MarkerSoftness, dmk);
                }

                fixed3 rgb = baseCol.rgb;
                rgb = lerp(rgb, _TrackColor.rgb, saturate(lineAlpha * _TrackColor.a));
                rgb = lerp(rgb, _MarkerColor.rgb, saturate(markerAlpha * _MarkerColor.a));

                float outA = max(baseCol.a, max(lineAlpha * _TrackColor.a, markerAlpha * _MarkerColor.a));
                return fixed4(rgb, outA);
            }
            ENDCG
        }
    }
}