Shader "Orbiter/Debug/GroundTrackGpuPropagate_AnalyticBodySpin"
{
    Properties
    {
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
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
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

                // Inertial-frame angular velocity advancing a PF->E orientation:
                // future basis rotates in inertial frame => left-multiply.
                float4 dq = QuatFromAxisAngle(axis, ang);
                return normalize(QuatMul(dq, qPF2E0));
            }

            bool MeanAnomalyFromTrueAnomaly_Ellipse(float e, float nu, out float M)
            {
                M = 0.0;

                if (e < 0.0 || e >= 1.0) return false;

                float s = sqrt(max(0.0, 1.0 - e * e));
                float sinNu = sin(nu);
                float cosNu = cos(nu);

                float E = atan2(s * sinNu, e + cosNu);
                E = Wrap2Pi(E);

                M = Wrap2Pi(E - e * sin(E));
                return true;
            }

            bool SolveKeplerEllipse(float M, float e, out float E)
            {
                E = (e > 0.8) ? PI : M;

                [unroll(8)]
                for (int k = 0; k < 8; k++)
                {
                    float sE = sin(E);
                    float cE = cos(E);
                    float f = E - e * sE - M;
                    float fp = 1.0 - e * cE;

                    if (abs(fp) < 1e-6) return false;
                    E = E - f / fp;
                }

                return true;
            }

            void BuildPQWToInertialMatrix(
                float raan, float inc, float argp,
                out float3 row0,
                out float3 row1,
                out float3 row2)
            {
                float cO = cos(raan);
                float sO = sin(raan);
                float ci = cos(inc);
                float si = sin(inc);
                float co = cos(argp);
                float so = sin(argp);

                row0 = float3(cO * co - sO * so * ci,
                              -cO * so - sO * co * ci,
                               sO * si);

                row1 = float3(sO * co + cO * so * ci,
                              -sO * so + cO * co * ci,
                              -cO * si);

                row2 = float3(so * si,
                               co * si,
                               ci);
            }

            bool PropagateEllipticPosition(
                float aMeters,
                float e,
                float inc,
                float raan,
                float argp,
                float nu0,
                float epochT,
                float sampleT,
                float mu,
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

            fixed4 frag(v2f i) : SV_Target
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
    }
}