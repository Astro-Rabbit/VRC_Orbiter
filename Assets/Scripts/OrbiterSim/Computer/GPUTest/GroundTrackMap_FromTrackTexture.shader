Shader "Orbiter/MFD/GroundTrackMap_FromTrackTexture"
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

        _TrackTex ("Track Texture", 2D) = "black" {}
        _TrackSampleCount ("Track Sample Count", Float) = 1

        // Optional longitude shift in UV space, if your map needs calibration.
        _LonOffset ("Longitude Offset UV", Range(-1,1)) = 0

        // 0 = first sample marker off, 1 = on
        _ShowCurrentMarker ("Show Current Marker", Float) = 1
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

            static const float PI = 3.14159265358979323846;
            static const float INV_PI = 0.31830988618379067154;
            static const float INV_TWOPI = 0.15915494309189533577;

            sampler2D _MainTex;
            float4 _MainTex_ST;

            sampler2D _TrackTex;

            fixed4 _Color;
            fixed4 _TrackColor;
            float _TrackWidth;
            float _TrackSoftness;

            fixed4 _MarkerColor;
            float _MarkerRadius;
            float _MarkerSoftness;

            float _TrackSampleCount;
            float _LonOffset;
            float _ShowCurrentMarker;

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

            float2 TrackSampleUV(float idx, float count)
            {
                float u = (idx + 0.5) / max(count, 1.0);
                return float2(u, 0.5);
            }

            float3 LoadTrackUnit(float idx, float count)
            {
                float2 suv = TrackSampleUV(idx, count);
                float4 t = tex2D(_TrackTex, suv);

                // Unpack from RGB = 0.5*u + 0.5
                float3 u = t.rgb * 2.0 - 1.0;

                float mag = length(u);
                if (mag > 1e-6)
                    u /= mag;
                else
                    u = float3(1, 0, 0);

                return u;
            }

            float2 UnitToMapUV(float3 u)
            {
                float lon = atan2(u.y, u.x);
                float lat = asin(clamp(u.z, -1.0, 1.0));

                float2 uv;
                uv.x = lon * INV_TWOPI + 0.5 + _LonOffset;
                uv.y = lat * INV_PI + 0.5;

                uv.x = frac(uv.x);
                return uv;
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

                float count = max(_TrackSampleCount, 1.0);
                int segCount = max((int)count - 1, 0);

                float lineAlpha = 0.0;

                // Hard cap for shader loop. Raise if you need more, but keep it reasonable.
                [loop]
                for (int s = 0; s < 256; s++)
                {
                    if (s >= segCount) break;

                    float3 u0 = LoadTrackUnit((float)s, count);
                    float3 u1 = LoadTrackUnit((float)(s + 1), count);

                    float2 a = UnitToMapUV(u0);
                    float2 b = UnitToMapUV(u1);
                    float dx = b.x - a.x;
                    if (dx > 0.5)
                        b.x -= 1.0;
                    else if (dx < -0.5)
                        b.x += 1.0;
                    float d = DistPointToSegmentWrapped(i.uv, a, b);
                    float m = 1.0 - smoothstep(_TrackWidth, _TrackWidth + _TrackSoftness, d);
                    lineAlpha = max(lineAlpha, m);
                }

                float markerAlpha = 0.0;
                if (_ShowCurrentMarker > 0.5 && count >= 1.0)
                {
                    float3 uCur = LoadTrackUnit(0.0, count);
                    float2 curUV = UnitToMapUV(uCur);

                    float dmk = DistPointToCircleWrapped(i.uv, curUV);
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