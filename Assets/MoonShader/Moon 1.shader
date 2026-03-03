Shader "Skybox/MoonOrbit_M5_Basic_OptionB"
{
    Properties
    {
        // =========================
        // Background
        // =========================
        _BgColor      ("Background Color", Color) = (0.02, 0.02, 0.03, 1)

        // =========================
        // Option B: sky rotation (Unity WS quaternion xyzw)
        // =========================
        _SkyQ ("Sky Rotation Quaternion (xyzw)", Vector) = (0,0,0,1)

        // =========================
        // Moon (sphere, meters) -- PRE-sky-rotation frame (UInert)
        // =========================
        _MoonCenterUInert ("Moon Center UInert (m)", Vector) = (0, 0, 4000000, 1)
        _MoonRadiusWS     ("Moon Radius (m)", Float)  = 1727400

        // Moon orientation (Body-fixed -> UInert). Quaternion (x,y,z,w).
        _MoonBodyToUInertQ ("Moon Body->UInert Quaternion (xyzw)", Vector) = (0,0,0,1)

        _MoonAlbedo   ("Moon Albedo (equirect)", 2D) = "gray" {}
        _MoonNormal   ("Moon Normal (tangent space)", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 3)) = 1.0

        // =========================
        // Lighting (simple) -- PRE-sky-rotation frame (UInert)
        // =========================
        _SunDirUInert  ("Sun Direction UInert (to Sun)", Vector) = (0, 1, 0, 0)
        _SunIntensity  ("Sun Intensity", Range(0, 5)) = 1.0
        _Ambient       ("Ambient", Range(0, 0.3)) = 0.03
        _TermSoft      ("Terminator Softness", Range(0.0, 0.2)) = 0.03
        _Wrap          ("Diffuse Wrap", Range(0, 0.5)) = 0.12

        // Limb AA tuning (keep from your proven approach)
        _LimbAA       ("Limb AA Strength", Range(0.5, 30.0)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Back
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BgColor;

            float4 _SkyQ;

            float4 _MoonCenterUInert;
            float  _MoonRadiusWS;
            float4 _MoonBodyToUInertQ;

            sampler2D _MoonAlbedo;
            sampler2D _MoonNormal;
            float _NormalStrength;

            float4 _SunDirUInert;
            float _SunIntensity, _Ambient, _TermSoft, _Wrap;
            float _LimbAA;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dirWS : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 dirOS = v.vertex.xyz;
                float3 dirWS = mul((float3x3)unity_ObjectToWorld, dirOS);
                o.dirWS = normalize(dirWS);
                return o;
            }

            // -------------------------
            // Utility
            // -------------------------
            float3 SafeNormalize(float3 v)
            {
                float len2 = dot(v,v);
                if (len2 < 1e-12) return float3(0,0,1);
                return v * rsqrt(len2);
            }

            // Quaternion (x,y,z,w) -> 3x3 rotation matrix
            float3x3 QuatToMat(float4 qIn)
            {
                float4 q = qIn;
                float invLen = rsqrt(max(1e-12, dot(q, q)));
                q *= invLen;

                float x = q.x, y = q.y, z = q.z, w = q.w;

                float xx = x*x, yy = y*y, zz = z*z;
                float xy = x*y, xz = x*z, yz = y*z;
                float wx = w*x, wy = w*y, wz = w*z;

                return float3x3(
                    1.0 - 2.0*(yy + zz),  2.0*(xy - wz),        2.0*(xz + wy),
                    2.0*(xy + wz),        1.0 - 2.0*(xx + zz),  2.0*(yz - wx),
                    2.0*(xz - wy),        2.0*(yz + wx),        1.0 - 2.0*(xx + yy)
                );
            }

            // Rotate a vector by quaternion (xyzw), active rotation: v' = q * v * q^-1
            float3 QRotate(float4 qIn, float3 v)
            {
                float4 q = qIn;
                float invLen = rsqrt(max(1e-12, dot(q, q)));
                q *= invLen;

                float3 u = q.xyz;
                float s = q.w;

                // Rodrigues via quaternion: v' = v + 2*s*(u×v) + 2*(u×(u×v))
                float3 uv  = cross(u, v);
                float3 uuv = cross(u, uv);
                return v + (2.0 * s) * uv + 2.0 * uuv;
            }

            float2 BodyDirToUV(float3 N_body)
            {
                float lon = atan2(N_body.y, N_body.x);
                float lat = asin(clamp(N_body.z, -1.0, 1.0));
                float u = lon * (1.0 / (2.0 * UNITY_PI)) + 0.5;
                float v = lat * (1.0 / UNITY_PI) + 0.5;
                return float2(frac(u), saturate(v));
            }

            void BuildTBN_FromBodyNormal(float3 N_body, float3x3 bodyToWorld, out float3 T_ws, out float3 B_ws)
            {
                float3 up_body = (abs(N_body.z) < 0.999) ? float3(0,0,1) : float3(0,1,0);
                float3 T_body = normalize(cross(up_body, N_body));
                float3 B_body = cross(N_body, T_body);

                T_ws = mul(bodyToWorld, T_body);
                B_ws = mul(bodyToWorld, B_body);
            }

            bool RaySphereHit(float3 O, float3 D, float3 C, float R, out float tHit, out float tNear, out float tFar)
            {
                float3 oc = O - C;
                float b = dot(oc, D);
                float c = dot(oc, oc) - R * R;
                float h = b*b - c;
                if (h < 0.0) { tHit = tNear = tFar = 0.0; return false; }
                float s = sqrt(h);
                tNear = -b - s;
                tFar  = -b + s;

                tHit = (tNear > 0.0) ? tNear : tFar;
                return (tFar > 0.0);
            }

            float4 QConj(float4 q) { return float4(-q.x, -q.y, -q.z, q.w); }

            fixed4 frag (v2f i) : SV_Target
            {
                // Camera origin stays in Unity WS. We rotate the *ray* into the pre-sky-rotation frame (UInert).
                float3 O_ws = _WorldSpaceCameraPos;
                float3 D_ws = normalize(i.dirWS);

                
                // Option B: apply sky rotation to ray (puts ray into UInert frame)
                float3 D = SafeNormalize(QRotate(QConj(_SkyQ), D_ws));

                // Background
                float3 bg = _BgColor.rgb;

                // Moon params in UInert
                float3 C = _MoonCenterUInert.xyz;
                float  R = max(_MoonRadiusWS, 1e-3);

                // Moon orientation in UInert
                float3x3 bodyToWorld = QuatToMat(_MoonBodyToUInertQ);
                float3x3 worldToBody = transpose(bodyToWorld);

                // IMPORTANT: For a skybox, treat origin at (0,0,0). Do NOT use world camera position here.
                // This matches your "craft at origin / skybox moved" model.
                float3 O = float3(0,0,0);

                // Intersect
                float tHit, tNear, tFar;
                if (!RaySphereHit(O, D, C, R, tHit, tNear, tFar))
                    return fixed4(bg, 1.0);

                // Limb AA coverage (signed distance at closest approach)
                float3 oc = O - C;
                float b = dot(oc, D);
                float tC = clamp(-b, max(tNear, 0.0), tFar);
                float3 Pc = O + tC * D;
                float Fc = length(Pc - C) - R;
                float w = fwidth(Fc) * _LimbAA;
                float coverage = saturate(0.5 - Fc / max(w, 1e-6));

                if (coverage <= 0.0)
                    return fixed4(bg, 1.0);

                // Surface point + normals
                float3 P = O + tHit * D;
                float3 N_ws = SafeNormalize(P - C);

                // Convert to body for UVs
                float3 N_body = mul(worldToBody, N_ws);
                float2 uv = BodyDirToUV(N_body);

                // Albedo
                float3 albedo = tex2D(_MoonAlbedo, uv).rgb;

                // Normal map
                float3 T_ws, B_ws;
                BuildTBN_FromBodyNormal(N_body, bodyToWorld, T_ws, B_ws);

                float3 n_ts = UnpackNormal(tex2D(_MoonNormal, uv));
                n_ts.xy *= _NormalStrength;
                n_ts = normalize(n_ts);

                float3 N_final = normalize(n_ts.x * T_ws + n_ts.y * B_ws + n_ts.z * N_ws);

                // Lighting in UInert (same frame as everything else here)
                float3 toSun = SafeNormalize(_SunDirUInert.xyz);
                float ndotl = dot(N_final, toSun);

                float ndWrap = saturate((ndotl + _Wrap) / (1.0 + _Wrap));
                float lit = smoothstep(0.0, _TermSoft, ndWrap);

                float dayMask = step(0.0, dot(N_ws, toSun));
                lit *= dayMask;

                float lightFactor = saturate(_Ambient + _SunIntensity * lit);
                float3 moonRGB = albedo * lightFactor;

                // Composite
                #if defined(UNITY_COLORSPACE_GAMMA)
                    bg      = GammaToLinearSpace(bg);
                    moonRGB = GammaToLinearSpace(moonRGB);
                #endif

                float3 outCol = lerp(bg, moonRGB, coverage);

                #if defined(UNITY_COLORSPACE_GAMMA)
                    outCol = LinearToGammaSpace(outCol);
                #endif

                return fixed4(outCol, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}