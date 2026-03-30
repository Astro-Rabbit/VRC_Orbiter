Shader "Orbiter/MFD/GroundTrackCombined_TwoPass"
{
    Properties
    {
        // Propagation inputs
        _SampleCount ("Sample Count", Float) = 1

        _A ("Semi-major Axis", Float) = 7000000
        _E ("Eccentricity", Float) = 0
        _Inc ("Inclination Rad", Float) = 0
        _RAAN ("RAAN Rad", Float) = 0
        _ArgP ("Arg Periapsis Rad", Float) = 0
        _Nu0 ("True Anomaly At Epoch Rad", Float) = 0
        _Mu ("Primary Mu", Float) = 398600441800000
        _T0 ("Epoch Time", Float) = 0
        _SampleStepSec ("Sample Step Sec", Float) = 30
        _BodyRadius ("Body Radius", Float) = 6371000

        _BodyQPF2E ("Body Q PF->E", Vector) = (0,0,0,1)
        _BodyOmega ("Body Omega Inertial", Vector) = (0,0,0,0)

        _AltDisplayScale ("Alt Display Scale", Float) = 1000000

        // Map inputs
        _MapTex ("Map Texture", 2D) = "white" {}
        _Color ("Map Tint", Color) = (1,1,1,1)
        _MapAspect ("Map Aspect", Float) = 2.0
        _TrackColor ("Track Color", Color) = (0,1,0,1)
        _TrackWidth ("Track Width UV", Range(0.0005, 0.05)) = 0.004
        _TrackSoftness ("Track Softness UV", Range(0.0001, 0.02)) = 0.0015

        _MarkerColor ("Marker Color", Color) = (1,1,0,1)
        _MarkerRadius ("Marker Radius UV", Range(0.001, 0.05)) = 0.01
        _MarkerSoftness ("Marker Softness UV", Range(0.0001, 0.02)) = 0.002

        _TrackTex ("Track Texture", 2D) = "black" {}
        _TrackSampleCount ("Track Sample Count", Float) = 1

        _LonOffset ("Longitude Offset UV", Range(-1,1)) = 0
        _ShowCurrentMarker ("Show Current Marker", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Name "PROPAGATE_TRACK_DATA"
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment fragPropagate
            #include "UnityCG.cginc"

            float _SampleCount;

            float _A;
            float _E;
            float _Inc;
            float _RAAN;
            float _ArgP;
            float _Nu0;
            float _Mu;
            float _T0;
            float _SampleStepSec;
            float _BodyRadius;
            float _AltDisplayScale;
            float4 _BodyQPF2E;
            float4 _BodyOmega;

            static const float PI = 3.14159265358979323846;
            static const float TWO_PI = 6.28318530717958647692;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Wrap2Pi(float x)
            {
                x = fmod(x, TWO_PI);
                if (x < 0.0) x += TWO_PI;
                return x;
            }

            float4 QuatConjugate(float4 q)
            {
                return float4(-q.x, -q.y, -q.z, q.w);
            }

            float4 QuatMul(float4 a, float4 b)
            {
                return float4(
                    a.w*b.x + a.x*b.w + a.y*b.z - a.z*b.y,
                    a.w*b.y - a.x*b.z + a.y*b.w + a.z*b.x,
                    a.w*b.z + a.x*b.y - a.y*b.x + a.z*b.w,
                    a.w*b.w - a.x*b.x - a.y*b.y - a.z*b.z
                );
            }

            float3 QuatRotate(float4 q, float3 v)
            {
                float3 t = 2.0 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            float4 QuatFromAxisAngle(float3 axis, float angle)
            {
                float halfAng = 0.5 * angle;
                float s = sin(halfAng);
                return float4(axis * s, cos(halfAng));
            }

            float4 AdvanceBodyQuaternion(float4 qPF2E0, float3 omegaE, float dt)
            {
                float om = length(omegaE);
                if (om < 1e-8)
                    return normalize(qPF2E0);

                float3 axis = omegaE / om;
                float ang = om * dt;

                float4 dq = QuatFromAxisAngle(axis, ang);
                return normalize(QuatMul(dq, qPF2E0));
            }

            bool SolveKeplerEllipse(float M, float e, out float E)
            {
                E = M;
                if (e < 1e-6)
                    return true;

                [unroll]
                for (int it = 0; it < 8; it++)
                {
                    float s = sin(E);
                    float c = cos(E);
                    float f = E - e * s - M;
                    float fp = 1.0 - e * c;
                    if (abs(fp) < 1e-8) break;
                    E -= f / fp;
                }
                return true;
            }

            bool MeanAnomalyFromTrueAnomaly_Ellipse(float e, float nu, out float M)
            {
                if (e < 0.0 || e >= 1.0)
                {
                    M = 0.0;
                    return false;
                }

                float s = sqrt(max(0.0, 1.0 - e * e)) * sin(nu);
                float c = e + cos(nu);
                float E = atan2(s, c);
                E = Wrap2Pi(E);
                M = Wrap2Pi(E - e * sin(E));
                return true;
            }

            void BuildPQWToInertialMatrix(
                float raan, float inc, float argp,
                out float3 row0, out float3 row1, out float3 row2)
            {
                float cO = cos(raan);
                float sO = sin(raan);
                float ci = cos(inc);
                float si = sin(inc);
                float cw = cos(argp);
                float sw = sin(argp);

                row0 = float3(cO*cw - sO*sw*ci, -cO*sw - sO*cw*ci, sO*si);
                row1 = float3(sO*cw + cO*sw*ci, -sO*sw + cO*cw*ci, -cO*si);
                row2 = float3(sw*si, cw*si, ci);
            }

            bool PropagateEllipticPosition(
                float aMeters, float e, float inc, float raan, float argp,
                float nu0, float epochT, float sampleT, float mu,
                out float3 rInertial)
            {
                rInertial = float3(0,0,0);

                if (mu <= 0.0) return false;
                if (aMeters <= 0.0) return false;
                if (e < 0.0 || e >= 1.0) return false;
                if (abs(1.0 - e) <= 1e-6) return false;

                float M0;
                if (!MeanAnomalyFromTrueAnomaly_Ellipse(e, nu0, M0)) return false;

                float dt = sampleT - epochT;
                float n = sqrt(mu / (aMeters * aMeters * aMeters));
                if (!(n > 0.0)) return false;

                float M = Wrap2Pi(M0 + n * dt);

                float E;
                if (!SolveKeplerEllipse(M, e, E)) return false;

                float cE = cos(E);
                float sE = sin(E);
                float fac = sqrt(max(0.0, 1.0 - e * e));

                float xpf = aMeters * (cE - e);
                float ypf = aMeters * (fac * sE);

                float3 row0, row1, row2;
                BuildPQWToInertialMatrix(raan, inc, argp, row0, row1, row2);

                float3 rPQW = float3(xpf, ypf, 0.0);

                rInertial = float3(
                    dot(row0, rPQW),
                    dot(row1, rPQW),
                    dot(row2, rPQW)
                );

                return true;
            }

            fixed4 fragPropagate(v2f i) : SV_Target
            {
                float count = max(_SampleCount, 1.0);

                float idx = floor(saturate(i.uv.x) * count);
                idx = min(idx, count - 1.0);

                float dt = idx * _SampleStepSec;
                float tSample = _T0 + dt;

                float3 rE;
                bool ok = PropagateEllipticPosition(
                    _A, _E, _Inc, _RAAN, _ArgP, _Nu0,
                    _T0, tSample, _Mu,
                    rE);

                if (!ok)
                    return float4(1.0, 0.0, 0.0, 1.0);

                float4 qPF2E_now = normalize(_BodyQPF2E);
                float3 omegaE = _BodyOmega.xyz;

                float4 qPF2E_future = AdvanceBodyQuaternion(qPF2E_now, omegaE, dt);
                float4 qE2PF_future = QuatConjugate(qPF2E_future);

                float3 rPF = QuatRotate(qE2PF_future, rE);

                float rMag = length(rPF);
                if (rMag < 1e-5)
                    return float4(1.0, 0.0, 1.0, 1.0);

                float3 u = rPF / rMag;
                float alt = rMag - _BodyRadius;

                float3 rgb = 0.5 * u + 0.5;
                float aVis = saturate(alt / max(_AltDisplayScale, 1.0));

                return float4(rgb, aVis);
            }
            ENDCG
        }

        Pass
        {
            Name "COMPOSITE_MAP"
            Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vertMap
            #pragma fragment fragMap
            #include "UnityCG.cginc"

            static const float PI = 3.14159265358979323846;
            static const float INV_PI = 0.31830988618379067154;
            static const float INV_TWOPI = 0.15915494309189533577;

            sampler2D _MapTex;
            sampler2D _TrackTex;

            fixed4 _Color;
            fixed4 _TrackColor;
            float _TrackWidth;
            float _TrackSoftness;
            float _MapAspect;

            fixed4 _MarkerColor;
            float _MarkerRadius;
            float _MarkerSoftness;

            float _TrackSampleCount;
            float _LonOffset;
            float _ShowCurrentMarker;

            struct appdataMap
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2fMap
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2fMap vertMap(appdataMap v)
            {
                v2fMap o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
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
                    float2 d = pp - c;
                    d.x *= _MapAspect;
                    best = min(best, length(d));
                }

                return best;
            }

            fixed4 fragMap(v2fMap i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MapTex, i.uv) * _Color;

                float count = max(_TrackSampleCount, 1.0);
                int segCount = max((int)count - 1, 0);

                float lineAlpha = 0.0;

                [loop]
                for (int s = 0; s < 256; s++)
                {
                    if (s >= segCount) break;

                    float3 u0 = LoadTrackUnit((float)s, count);
                    float3 u1 = LoadTrackUnit((float)(s + 1), count);

                    float2 a = UnitToMapUV(u0);
                    float2 b = UnitToMapUV(u1);

                    float dx = b.x - a.x;
                    if (dx > 0.5) b.x -= 1.0;
                    else if (dx < -0.5) b.x += 1.0;

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
                return fixed4(rgb, 1.0);
            }
            ENDCG
        }
    }
}