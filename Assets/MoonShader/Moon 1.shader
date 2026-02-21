Shader "Skybox/MoonOrbit_M3_Albedo"
{
    Properties
    {
        _BgColor      ("Background Color", Color) = (0.02, 0.02, 0.03, 1)

        // World-space sphere definition
        _MoonCenterWS ("Moon Center WS", Vector) = (0, 0, 1000, 1)
        _MoonRadiusWS ("Moon Radius WS", Float)  = 500

        // Orientation controls (degrees). Placeholders for future physical libration.
        _MoonYawDeg   ("Moon Yaw (deg, about +Z body)", Float) = 0
        _MoonPitchDeg ("Moon Pitch (deg, about +X body)", Float) = 0
        _MoonRollDeg  ("Moon Roll (deg, about +Y body)", Float) = 0

        // Texture
        _MoonAlbedo   ("Moon Albedo (equirect)", 2D) = "gray" {}
        _MoonNormal ("Moon Normal (tangent space)", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 3)) = 1.0

        _MoonHeight ("Moon Height", 2D) = "gray" {}
        _HeightScale ("Height Scale", Range(0, 5)) = 1.0
        _HeightShadow ("Height Shadow Strength", Range(0, 2)) = 0.5

        _SunDirWS     ("Sun Direction WS", Vector) = (0, 1, 0, 0)
        _UseSceneSun   ("Use Scene Directional Light", Range(0,1)) = 1
        _FlipSceneSun  ("Flip Scene Sun Direction", Range(0,1)) = 0
        _SunIntensity ("Sun Intensity", Range(0, 5)) = 1.0
        _Ambient      ("Ambient", Range(0, 0.3)) = 0.03
        _TermSoft     ("Terminator Softness", Range(0.0, 0.2)) = 0.03

        _SunShadowEnable   ("Sun Shadows (Height Ray)", Range(0,1)) = 1
        _SunShadowSteps    ("Sun Shadow Steps", Range(1,16)) = 8
        _SunShadowStepUV   ("Sun Shadow Step (UV)", Range(0.0001, 0.01)) = 0.0015
        _SunShadowStrength ("Sun Shadow Strength", Range(0, 50)) = 12
        _SunShadowBias     ("Sun Shadow Bias", Range(0, 0.05)) = 0.002
        _SunShadowTermBand ("Shadow Terminator Band", Range(0.01, 0.5)) = 0.20

        // Debug switches
        _UseUVDebug   ("Use UV Debug", Range(0,1)) = 0
        _ShowSeam     ("Show Seam Lines (Debug)", Range(0,1)) = 1

        _LimbAA ("Limb AA Strength", Range(0.5, 30.0)) = 1.0

    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Back
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BgColor;

            float4 _MoonCenterWS;
            float  _MoonRadiusWS;

            float _MoonYawDeg;
            float _MoonPitchDeg;
            float _MoonRollDeg;

            sampler2D _MoonAlbedo;
            float4 _MoonAlbedo_ST;
            sampler2D _MoonNormal;
            float _NormalStrength;

            sampler2D _MoonHeight;
            float _HeightScale;
            float _HeightShadow;

            float4 _SunDirWS;
            float _UseSceneSun;
            float _FlipSceneSun;
            float  _SunIntensity;
            float  _Ambient;
            float  _TermSoft;

            float _SunShadowEnable;
            float _SunShadowSteps;
            float _SunShadowStepUV;
            float _SunShadowStrength;
            float _SunShadowBias;
            float _SunShadowTermBand;

            float _UseUVDebug;
            float _ShowSeam;
            float _LimbAA;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float3 dirWS : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                float3 dirOS = v.vertex.xyz;
                float3 dirWS = mul((float3x3)unity_ObjectToWorld, dirOS);
                o.dirWS = normalize(dirWS);

                return o;
            }

            bool RaySphereIntersect(float3 rayOriginWS, float3 rayDirWS, float3 centerWS, float radius, out float tHit)
            {
                float3 oc = rayOriginWS - centerWS;

                float b = dot(oc, rayDirWS);
                float c = dot(oc, oc) - radius * radius;
                float h = b * b - c;

                if (h < 0.0)
                {
                    tHit = 0.0;
                    return false;
                }

                float sqrtH = sqrt(h);
                float tNear = -b - sqrtH;
                float tFar  = -b + sqrtH;

                if (tNear > 0.0) { tHit = tNear; return true; }
                if (tFar  > 0.0) { tHit = tFar;  return true; }

                tHit = 0.0;
                return false;
            }

            float3x3 RotX(float a)
            {
                float s = sin(a), c = cos(a);
                return float3x3(
                    1, 0, 0,
                    0, c,-s,
                    0, s, c
                );
            }

            float3x3 RotY(float a)
            {
                float s = sin(a), c = cos(a);
                return float3x3(
                     c, 0, s,
                     0, 1, 0,
                    -s, 0, c
                );
            }

            float3x3 RotZ(float a)
            {
                float s = sin(a), c = cos(a);
                return float3x3(
                    c,-s, 0,
                    s, c, 0,
                    0, 0, 1
                );
            }

            // Build a stable tangent basis from body-space normal.
            // U ~ longitude (east), V ~ latitude (north).
            void BuildTBN_FromBodyNormal(float3 N_body, float3x3 bodyToWorld, out float3 T_ws, out float3 B_ws)
            {
                // Define body-space "up" to resolve the basis; avoid degeneracy near poles.
                float3 up_body = (abs(N_body.z) < 0.999) ? float3(0,0,1) : float3(0,1,0);

                // Tangent points roughly along increasing longitude (east)
                float3 T_body = normalize(cross(up_body, N_body));

                // Bitangent completes right-handed basis, roughly toward increasing latitude (north)
                float3 B_body = cross(N_body, T_body);

                T_ws = mul(bodyToWorld, T_body);
                B_ws = mul(bodyToWorld, B_body);
            }
            // Height-map self-shadow approximation by marching in UV along the projected sun direction.
            // Returns 1 = fully lit, 0 = fully shadowed.
            float HeightSunShadowUV(
                float2 uv0,
                float h0,
                float2 sunDirUV,          // normalized direction in UV space
                float ndotl,              // dot(N, L) using your perturbed normal
                float stepsF,
                float stepUV,
                float strength,
                float bias)
            {
                // Only meaningful on day side
                if (ndotl <= 0.0) return 0.0;

                // March distance scales with grazing angle: lower sun => longer shadows.
                // This makes the effect naturally concentrate near the terminator.
                float grazing = saturate(1.0 - ndotl);

                float maxOcc = 0.0;

                // Clamp steps to [1..16]
                int steps = (int)clamp(stepsF, 1.0, 16.0);

                float2 uv = uv0;

                // A simple height threshold ramp:
                // as we march "toward the sun", the ray rises; near terminator (grazing~1) it rises slowly.
                // This is not physically exact, but it is stable and tunable.
                float risePerStep = bias + 0.02 * grazing; // tuneable baseline rise

                [unroll]
                for (int i = 1; i <= 16; i++)
                {
                    if (i > steps) break;

                    uv += sunDirUV * stepUV;

                    // Wrap U, clamp V (avoid sampling beyond poles)
                    float2 uvs;
                    uvs.x = frac(uv.x);
                    uvs.y = saturate(uv.y);

                    float hi = tex2D(_MoonHeight, uvs).r;

                    float expected = h0 + risePerStep * i;

                    // If the terrain is above the "sun ray" height, it occludes
                    float occ = hi - expected;
                    maxOcc = max(maxOcc, occ);
                }

                // Convert occlusion amount to shadow factor
                // More occlusion => darker. Clamp occ to avoid extreme exponent.
                maxOcc = max(0.0, maxOcc);
                float shadow = exp(-strength * maxOcc);

                return saturate(shadow);
            }


            fixed4 frag (v2f i) : SV_Target
            {
                float3 O = _WorldSpaceCameraPos;
                float3 D = normalize(i.dirWS);

                float3 C = _MoonCenterWS.xyz;
                float  R = max(_MoonRadiusWS, 1e-3);

                // --- Closest-approach terms (stable at the limb) ---
                float3 oc = O - C;
                float  b  = dot(oc, D);
                float  d2 = dot(oc, oc) - b * b;     // squared closest distance from ray to center

                // Signed distance to the silhouette (world units)
                float distToRay = sqrt(max(d2, 0.0));
                float sdf = distToRay - R;           // <0 inside, >0 outside

                // Analytic AA coverage
                float w = fwidth(sdf) * _LimbAA;
                float coverage = saturate(0.5 - sdf / max(w, 1e-6));

                // Outside: background
                if (coverage <= 0.0)
                    return _BgColor;

                // --- Compute a stable "hit" even at tangent ---
                // For a true intersection, h >= 0, where h = R^2 - d2.
                // At the limb, h ~ 0. We clamp to 0 so tangent pixels still get a valid point.
                float h = R * R - d2;
                float sqrtH = sqrt(max(h, 0.0));
                // If even the FAR intersection is behind the camera, this ray does not hit in front.
                // This is the key fix that removes the "opposite sky" duplicate.
                float tFar = -b + sqrtH;
                if (tFar <= 0.0)
                    return _BgColor;

                float tNear = -b - sqrtH;
                float tHit = (tNear > 0.0) ? tNear : tFar;

                float3 H_ws = O + tHit * D;
                float3 N_ws = normalize(H_ws - C);

                // Orientation
                float yaw   = radians(_MoonYawDeg);
                float pitch = radians(_MoonPitchDeg);
                float roll  = radians(_MoonRollDeg);

                float3x3 bodyToWorld = mul(RotY(roll), mul(RotX(pitch), RotZ(yaw)));
                float3x3 worldToBody = transpose(bodyToWorld);

                float3 N_body = mul(worldToBody, N_ws);

                float3 T_ws, B_ws;
                BuildTBN_FromBodyNormal(N_body, bodyToWorld, T_ws, B_ws);               

                // Equirectangular UV
                float lon = atan2(N_body.y, N_body.x);
                float lat = asin(clamp(N_body.z, -1.0, 1.0));

                float u = lon * (1.0 / (2.0 * UNITY_PI)) + 0.5;
                float v = lat * (1.0 / UNITY_PI) + 0.5;

                // Debug mode (still anti-aliased at limb)
                if (_UseUVDebug > 0.5)
                {
                    float seam = 0.0;
                    if (_ShowSeam > 0.5)
                    {
                        float edgeU = min(u, 1.0 - u);
                        float edgeV = min(v, 1.0 - v);
                        float seamU = smoothstep(0.0, 0.01, edgeU);
                        float seamV = smoothstep(0.0, 0.01, edgeV);
                        seam = 1.0 - min(seamU, seamV);
                    }

                    fixed4 dbg = fixed4(u, v, seam, 1.0);

                    // Blend debug with bg using coverage
                    fixed3 outDbg = lerp(_BgColor.rgb, dbg.rgb, coverage);
                    return fixed4(outDbg, 1.0);
                }

                fixed4 albedo = tex2D(_MoonAlbedo, float2(u, v));
                albedo.a = 1.0;

                // --- Lighting (geometric normal only) ---
                float3 L = normalize(_SunDirWS.xyz);     // light direction in world space

                if (_UseSceneSun > 0.5)
                {
                    // _WorldSpaceLightPos0 is valid for the main light in forward rendering.
                    // For a directional light, w == 0.
                    float3 sceneL = _WorldSpaceLightPos0.xyz;

                    // Some projects want the opposite convention; make it explicit.
                    if (_FlipSceneSun > 0.5) sceneL = -sceneL;

                    // Only trust it if it's directional-ish (w ~ 0). Otherwise keep manual.
                    if (abs(_WorldSpaceLightPos0.w) < 0.5)
                        L = normalize(sceneL);
                }


                // Tangent-space normal from normal map (UnpackNormal handles Unity normal encoding)
                float3 n_ts = UnpackNormal(tex2D(_MoonNormal, float2(u, v)));

                // Strength control (scale XY; keep Z positive-ish)
                n_ts.xy *= _NormalStrength;
                n_ts = normalize(n_ts);

                // Convert to world space using TBN
                float3 Nn_ws = normalize(n_ts.x * T_ws + n_ts.y * B_ws + n_ts.z * N_ws);

                float ndotl = dot(Nn_ws, L);              // -1..1

                // Project sun direction onto local tangent plane -> UV direction.
                // We use the tangent basis you already built (T_ws ~ +U, B_ws ~ +V).
                float2 sunDirUV = float2(dot(L, T_ws), dot(L, B_ws));
                float sunDirLen = length(sunDirUV);
                sunDirUV = (sunDirLen > 1e-5) ? (sunDirUV / sunDirLen) : float2(0.0, 0.0);


                // // -----------------------------
                // // Height-driven micro-occlusion (near terminator only)
                // // -----------------------------
                // float height01 = tex2D(_MoonHeight, float2(u, v)).r;
                // float heightCentered = (height01 - 0.5) * _HeightScale;

                // // Mask: 1 at terminator (ndotl ~ 0), 0 by the time we reach brighter day side.
                // // Tune OCC_START to control how wide the band is.
                // const float OCC_START = 0.25; // try 0.15–0.35
                // float termMask = 0.0;
                // if (ndotl > 0.0)
                // {
                //     termMask = saturate((OCC_START - ndotl) / OCC_START);
                //     termMask = termMask * termMask; // sharpen falloff
                // }

                // // Occlusion: valleys (negative heightCentered) darken more near terminator.
                // // If your height map polarity is opposite, flip the sign inside max().
                // float valley = max(0.0, -heightCentered);
                // float heightOcc = exp(-_HeightShadow * valley * termMask);
                // heightOcc = saturate(heightOcc);
                
                // Soft terminator: remap ndotl around 0 with a small width.
                // This avoids a razor-sharp edge and looks better in orbit.
                float lit = smoothstep(-_TermSoft, _TermSoft, ndotl); // 0 (night) to 1 (day)


                // Base height sample at the current surface point
                float h0 = _HeightScale*tex2D(_MoonHeight, float2(u, v)).r;

                // Only apply this near the terminator on the day side
                float termMask = 0.0;
                if (ndotl > 0.0)
                {
                    // 1 at terminator, 0 by ndotl >= _SunShadowTermBand
                    termMask = saturate((_SunShadowTermBand - ndotl) / max(_SunShadowTermBand, 1e-4));
                    termMask = termMask * termMask;
                }

                float sunShadow = 1.0;
                if (_SunShadowEnable > 0.5 && termMask > 0.0)
                {
                    sunShadow = HeightSunShadowUV(
                        float2(u, v),
                        h0,
                        sunDirUV,
                        ndotl,
                        _SunShadowSteps,
                        _SunShadowStepUV,
                        _SunShadowStrength,
                        _SunShadowBias
                    );

                    // Blend the effect in only near terminator
                    sunShadow = lerp(1.0, sunShadow, termMask);
                }
                // Final light factor
                float lightFactor = _Ambient + (_SunIntensity * lit * sunShadow);
                lightFactor = saturate(lightFactor); // keep sane                

                // --- IMPORTANT: do the blend in linear space to avoid "dark fringe" in Gamma projects ---
                fixed3 bgCol = _BgColor.rgb;
                fixed3 moonCol = albedo.rgb* lightFactor;

                #if defined(UNITY_COLORSPACE_GAMMA)
                    bgCol   = GammaToLinearSpace(bgCol);
                    moonCol = GammaToLinearSpace(moonCol);
                #endif

                fixed3 outCol = lerp(bgCol, moonCol, coverage);

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
