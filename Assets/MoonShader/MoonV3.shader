Shader "Skybox/Orbiter_StarsEQ_SunMoonECL_NoDisp"
{
    Properties
    {
        // =========================
        // Background / Stars (octa packed, legacy star frame)
        // =========================
        _BgColor      ("Background Color (fallback)", Color) = (0.02, 0.02, 0.03, 1)

        _StarData ("Mag Data (R16)", 2D) = "white" {}
        _TempData ("Temp Data (R16)", 2D) = "white" {}
        _XoffData ("X Data (R16)", 2D) = "white" {}
        _YoffData ("Y Data (R16)", 2D) = "white" {}

        _PixelSize ("pixelscale", Float) = 1024
        _maxMag ("Mag Limit", Float) = 10
        _sigma ("gaussSigma", Float)  = 60
        _scaleFactor ("Mag shift (changes mag zero point)", Float)  = 0
        _brightnessScale ("LinearBrightnessScale", Float)  = 10

        // =========================
        // Milky Way cubemap
        // =========================
        _MWCube ("Milky Way Cube", CUBE) = "" {}
        _MWbright ("MW brightness Scale", Float) = 1
        _MWDesat ("MW desaturation", Range(0,1)) = 0.6

        // Rotation mapping legacy StarFrame direction -> MW cubemap direction
        // (Use the quaternion you validated, derived from your old Y/X/Z)
        _Q_MW_from_Star ("MW Rot (quat: MW<-StarFrame)", Vector) = (0,0,0,1)

        // =========================
        // Craft attitude: body -> ECL inertial
        // =========================
        _Q_BE ("Craft qBE (quat: ECL<-B)", Vector) = (0,0,0,1)

        // =========================
        // ECL -> EQ tilt (obliquity) and EQ -> StarFrame
        // =========================
        // Default is J2000 obliquity: rotate about +X by +23.439281 deg (ECL->EQ)
        _Q_EQ_from_ECL ("Quat EQ<-ECL (x-tilt)", Vector) = (0.20312295, 0, 0, 0.97915324)

        // Legacy StarFrame convention: Aries = -Y, NCP = +Z.
        // If EQ has Aries=+X, then StarFrame = Rz(-90deg) * EQ
        _Q_Star_from_EQ ("Quat Star<-EQ (Aries->-Y)", Vector) = (0,0,-0.70710678,0.70710678)

        // =========================
        // Sun (ECL inertial)
        // =========================
        _SunDir_ECL      ("Sun Direction (ECL, to Sun)", Vector) = (0, 1, 0, 0)

        _SunAngularRadiusDeg ("Sun Angular Radius (deg)", Range(0.05, 5.0)) = 0.2666
        _SunEdgeSoftnessPx   ("Sun Edge Softness (px)", Range(0.0, 6.0)) = 2.0
        _SunColor ("Sun Color", Color) = (1, 0.98, 0.92, 1)
        _SunDiskIntensity ("Sun Disk Intensity", Range(0, 200)) = 25.0

        _SunGlareEnable    ("Sun Glare Enable", Range(0,1)) = 1
        _SunGlareStrength  ("Sun Glare Strength", Range(0, 10)) = 2.0
        _SunGlareRadiusDeg ("Sun Glare Radius (deg)", Range(0.1, 10)) = 3.0
        _SunGlarePower     ("Sun Glare Power", Range(0.5, 8)) = 2.5

        _SunSpikeEnable    ("Sun Spikes Enable", Range(0,1)) = 1
        _SunSpikeStrength  ("Sun Spike Strength", Range(0, 5)) = 0.6
        _SunSpikeCount     ("Sun Spike Count", Range(2, 12)) = 4
        _SunSpikeSharpness ("Sun Spike Sharpness", Range(1, 64)) = 14
        _SunSpikeLengthR   ("Sun Spike Length (radii)", Range(1, 8)) = 3.0
        _SunSpikeFalloff   ("Sun Spike Falloff Power", Range(0.5, 6)) = 2.0

        // =========================
        // Moon (ECL placement, Moon body-fixed rotation)
        // =========================
        _MoonDir_ECL          ("Moon Direction (ECL, to Moon)", Vector) = (0, 0, 1, 0)
        _MoonAngularRadiusDeg ("Moon Angular Radius (deg)", Range(0.01, 20.0)) = 3.0
        _MoonEdgeSoftnessPx   ("Moon Edge Softness (px)", Range(0.0, 6.0)) = 2.0

        // Moon body-fixed -> ECL inertial (from ephem.moon_q*)
        _Q_MoonBE ("Moon qBE (quat: ECL<-MoonBody)", Vector) = (0,0,0,1)

        _MoonAlbedo   ("Moon Albedo (equirect)", 2D) = "gray" {}
        _MoonNormal   ("Moon Normal (tangent space)", 2D) = "bump" {}
        _MoonNormalStrength ("Moon Normal Strength", Range(0, 3)) = 1.0

        _MoonSunIntensity  ("Moon Sun Intensity", Range(0, 5)) = 1.0
        _MoonAmbient       ("Moon Ambient", Range(0, 0.3)) = 0.03
        _MoonTermSoftDeg   ("Moon Terminator Soft (deg)", Range(0.0, 10.0)) = 1.0
        _MoonWrap          ("Moon Diffuse Wrap", Range(0, 0.5)) = 0.12
        _MoonLimbDark      ("Moon Limb Darkening", Range(0, 2)) = 0.25

        // Debug
        _Debug_ShowDirs ("Debug: viewDir frames (0=off,1=ECL,2=EQ,3=Star)", Range(0,3)) = 0
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

            sampler2D _StarData, _TempData, _XoffData, _YoffData;
            float _PixelSize, _maxMag, _sigma, _scaleFactor, _brightnessScale;

            samplerCUBE _MWCube;
            float _MWbright, _MWDesat;
            float4 _Q_MW_from_Star;

            float4 _Q_BE;
            float4 _Q_EQ_from_ECL;
            float4 _Q_Star_from_EQ;

            float4 _SunDir_ECL;
            float _SunAngularRadiusDeg, _SunEdgeSoftnessPx, _SunDiskIntensity;
            float4 _SunColor;
            float _SunGlareEnable, _SunGlareStrength, _SunGlareRadiusDeg, _SunGlarePower;
            float _SunSpikeEnable, _SunSpikeStrength, _SunSpikeCount, _SunSpikeSharpness;
            float _SunSpikeLengthR, _SunSpikeFalloff;

            float4 _MoonDir_ECL;
            float  _MoonAngularRadiusDeg;
            float  _MoonEdgeSoftnessPx;

            float4 _Q_MoonBE;

            sampler2D _MoonAlbedo;
            sampler2D _MoonNormal;
            float _MoonNormalStrength;

            float _MoonSunIntensity, _MoonAmbient, _MoonTermSoftDeg, _MoonWrap, _MoonLimbDark;

            float _Debug_ShowDirs;

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

            float3 SafeNormalize(float3 v)
            {
                float len2 = dot(v,v);
                if (len2 < 1e-12) return float3(0,0,1);
                return v * rsqrt(len2);
            }

            // Quaternion rotate: q * v * q^-1, q=(x,y,z,w)
            float3 QuatRotate(float4 q, float3 v)
            {
                float3 qv = q.xyz;
                float3 t = 2.0 * cross(qv, v);
                return v + q.w * t + cross(qv, t);
            }
            float4 QuatConjugate(float4 q) { return float4(-q.x, -q.y, -q.z, q.w); }

            fixed3 Desaturate3(fixed3 rgb, float amt)
            {
                float gray = dot(rgb, fixed3(0.299, 0.587, 0.114));
                return lerp(rgb, gray.xxx, amt);
            }

            // -------------------------
            // Stars (octa packed) - expects legacy StarFrame directions
            // -------------------------
            float decodeMagnitude(float encodedValue)
            {
                float maxMag = -1.46;
                float minMag = _maxMag;
                if (encodedValue == 0) return 40;
                return (minMag + (minMag - maxMag) * (encodedValue * -1));
            }

            float magnitudeToBrightness(float magnitude)
            {
                return exp2((-magnitude / 2.5) * 3.32192809489);
            }

            float drawStar(float distanceArcsec, float sigma)
            {
                return exp(- (distanceArcsec*distanceArcsec) / (2.0 * sigma * sigma));
            }

            void OctaBaseFromDir(float3 dir, out float3 dFlip, out float2 baseTexel)
            {
                // keep your flip convention as-authored
                dFlip = float3(-dir.x, dir.y, dir.z);

                float sumAbs = abs(dFlip.x) + abs(dFlip.y) + abs(dFlip.z);
                float3 p = dFlip / max(sumAbs, 1e-9);

                float2 coord = (p.z >= 0.0)
                    ? p.xy
                    : float2(sign(p.x) * (1.0 - abs(p.y)),
                             sign(p.y) * (1.0 - abs(p.x)));

                float2 uvOct = coord * 0.5 + 0.5;
                float2 pixelSpace = uvOct * _PixelSize;
                baseTexel = floor(pixelSpace);
            }

            float4 RetreivePixInfo(float3 dFlip, float2 baseTexel, float2 pixelOff)
            {
                float2 pixelCenter = baseTexel + 0.5 + pixelOff;
                float2 uvCenter = pixelCenter / _PixelSize;

                float starData = tex2D(_StarData, uvCenter).r;
                half3 tempR    = tex2D(_TempData, uvCenter).rgb;
                float XData    = tex2D(_XoffData, uvCenter).r;
                float YData    = tex2D(_YoffData, uvCenter).r;

                float mag = decodeMagnitude(starData) - _scaleFactor;
                float starBrightness = magnitudeToBrightness(mag) * _brightnessScale;

                float2 coord1;
                coord1.x = (uvCenter.x * 2.0 - 1.0) + (-((((YData-0.25)*2)*3)-1.5)/(_PixelSize));
                coord1.y = (uvCenter.y * 2.0 - 1.0) + ( ((((XData-0.25)*2)*3)-1.5)/(_PixelSize));

                float3 Pprime1;
                if (abs(coord1.x) + abs(coord1.y) <= 1.0)
                {
                    Pprime1.xy = coord1;
                    Pprime1.z = 1.0 - abs(coord1.x) - abs(coord1.y);
                }
                else
                {
                    Pprime1.x = sign(coord1.x) * (1.0 - abs(coord1.y));
                    Pprime1.y = sign(coord1.y) * (1.0 - abs(coord1.x));
                    Pprime1.z = -(1.0 - abs(Pprime1.x) - abs(Pprime1.y));
                }

                float3 pDir    = SafeNormalize(Pprime1);
                float3 baseDir = SafeNormalize(dFlip);

                float vecDist = length(pDir - baseDir) * 206265.0;
                float intensity = drawStar(vecDist, _sigma);

                return float4(tempR, 1.0) * (starBrightness * intensity);
            }

            // -------------------------
            // Sun disk in ECL
            // -------------------------
            float3 RenderSunPerceptual(float3 viewDirECL, float3 sunDirECL)
            {
                float3 V = SafeNormalize(viewDirECL);
                float3 S = SafeNormalize(sunDirECL);

                float cosA = saturate(dot(V, S));
                float angRad = radians(_SunAngularRadiusDeg);
                float cosR = cos(angRad);

                float w = fwidth(cosA) * max(0.5, _SunEdgeSoftnessPx);
                float disk = smoothstep(cosR - w, cosR + w, cosA);

                float3 diskRGB = _SunColor.rgb * (_SunDiskIntensity * disk);

                float theta = acos(cosA);
                float thetaDeg = degrees(theta);

                float glare = 0.0;
                if (_SunGlareEnable > 0.5)
                {
                    float t = saturate(1.0 - thetaDeg / max(1e-3, _SunGlareRadiusDeg));
                    glare = _SunGlareStrength * pow(t, _SunGlarePower);
                }

                float spikes = 0.0;
                if (_SunSpikeEnable > 0.5)
                {
                    float3 up = (abs(S.y) < 0.99) ? float3(0,1,0) : float3(1,0,0);
                    float3 U = SafeNormalize(cross(up, S));
                    float3 W = cross(S, U);

                    float x = dot(V, U);
                    float y = dot(V, W);
                    float az = atan2(y, x);

                    float rRadii = theta / max(1e-6, angRad);

                    float radial = saturate(1.0 - (rRadii - 1.0) / max(1e-3, (_SunSpikeLengthR - 1.0)));
                    radial = pow(radial, _SunSpikeFalloff);

                    float n = max(2.0, _SunSpikeCount);
                    float pat = abs(cos(az * 0.5 * n));
                    pat = pow(pat, _SunSpikeSharpness);

                    spikes = _SunSpikeStrength * pat * radial;
                    spikes *= (1.0 - disk);
                }

                float3 haloRGB = _SunColor.rgb * (glare + spikes);
                return diskRGB + haloRGB;
            }

            // -------------------------
            // Moon analytic sphere disk in ECL, textured in moon body frame
            // -------------------------
            float2 DirToEquirectUV(float3 dirUnit)
            {
                float lon = atan2(dirUnit.y, dirUnit.x);
                float lat = asin(clamp(dirUnit.z, -1.0, 1.0));
                float u = lon * (1.0 / (2.0 * UNITY_PI)) + 0.5;
                float v = lat * (1.0 / UNITY_PI) + 0.5;
                return float2(frac(u), saturate(v));
            }

            bool SkyDiskPointOnSphere(
                float3 viewDir, float3 toCenter, float angRadiusDeg, float edgeSoftPx,
                out float3 nSurf, out float mask, out float r2)
            {
                float3 V = SafeNormalize(viewDir);
                float3 C = SafeNormalize(toCenter);

                float cosA = dot(V, C);
                float angRad = radians(angRadiusDeg);
                float cosR = cos(angRad);

                nSurf = C;
                r2 = 2.0;
                mask = 0.0;

                float wAA = fwidth(cosA) * max(0.5, edgeSoftPx);
                mask = smoothstep(cosR - wAA, cosR + wAA, cosA);
                if (mask <= 0.0) return false;

                float3 up = (abs(C.y) < 0.99) ? float3(0,1,0) : float3(1,0,0);
                float3 U = SafeNormalize(cross(up, C));
                float3 W = cross(C, U);

                float x = dot(V, U);
                float y = dot(V, W);

                float sinR = sin(angRad);
                float2 p = float2(x, y) / max(1e-6, sinR);
                r2 = dot(p, p);

                if (r2 > 1.0)
                {
                    nSurf = C;
                    return true;
                }

                float zSurf = sqrt(max(0.0, 1.0 - r2));
                float3 nLocal = float3(p.x, p.y, zSurf);
                nSurf = SafeNormalize(nLocal.x * U + nLocal.y * W + nLocal.z * C);
                return true;
            }

            void BuildTBN_FromBodyNormal(float3 N_body, out float3 T_body, out float3 B_body)
            {
                float3 up_body = (abs(N_body.z) < 0.999) ? float3(0,0,1) : float3(0,1,0);
                T_body = SafeNormalize(cross(up_body, N_body));
                B_body = cross(N_body, T_body);
            }

            float3 RenderMoonSphere(
                float3 viewDirECL, float3 toMoonECL, float3 toSunECL,
                float4 qMoonBE,
                out float moonMask)
            {
                float3 N_surfECL;
                float r2;
                moonMask = 0.0;

                bool hit = SkyDiskPointOnSphere(viewDirECL, toMoonECL, _MoonAngularRadiusDeg, _MoonEdgeSoftnessPx,
                                                N_surfECL, moonMask, r2);
                if (!hit || moonMask <= 0.0) return float3(0,0,0);

                // Convert surface normal into moon body-fixed frame:
                // qMoonBE maps MoonBody -> ECL, so inverse maps ECL -> MoonBody
                float4 qEM = QuatConjugate(qMoonBE);
                float3 N_body = SafeNormalize(QuatRotate(qEM, N_surfECL));

                float2 uv = DirToEquirectUV(N_body);
                float3 albedo = tex2D(_MoonAlbedo, uv).rgb;

                float3 T_body, B_body;
                BuildTBN_FromBodyNormal(N_body, T_body, B_body);

                float3 n_ts = UnpackNormal(tex2D(_MoonNormal, uv));
                n_ts.xy *= _MoonNormalStrength;
                n_ts = SafeNormalize(n_ts);

                float3 N_pert_body = SafeNormalize(n_ts.x * T_body + n_ts.y * B_body + n_ts.z * N_body);
                float3 N_pert_ECL = SafeNormalize(QuatRotate(qMoonBE, N_pert_body));

                float3 L = SafeNormalize(toSunECL);

                float ndl = dot(N_pert_ECL, L);
                float ndWrap = saturate((ndl + _MoonWrap) / (1.0 + _MoonWrap));

                float soft = sin(radians(max(0.001, _MoonTermSoftDeg)));
                float lit = smoothstep(-soft, soft, ndl) * ndWrap;

                float muC = saturate(dot(N_surfECL, SafeNormalize(toMoonECL)));
                float limb = lerp(1.0 - _MoonLimbDark, 1.0, muC);

                float3 rgb = albedo * (_MoonAmbient + _MoonSunIntensity * lit);
                rgb *= limb;

                return rgb;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // View ray in Unity/craft frame (we assume craft isn't rotated in Unity)
                float3 viewDirU = SafeNormalize(i.dirWS);

                // 1) Convert to ECL inertial via craft attitude qBE
                float3 viewDirECL = SafeNormalize(QuatRotate(_Q_BE, viewDirU));

                // 2) Convert ECL -> EQ (so +Z becomes celestial north, i.e. "Polaris axis")
                float3 viewDirEQ = SafeNormalize(QuatRotate(_Q_EQ_from_ECL, viewDirECL));

                // 3) Convert EQ -> legacy StarFrame (Aries = -Y)
                float3 ndirStar = SafeNormalize(QuatRotate(_Q_Star_from_EQ, viewDirEQ));

                // Debug visualizations
                if (_Debug_ShowDirs > 0.5)
                {
                    float m = _Debug_ShowDirs;
                    float3 d = (m < 1.5) ? viewDirECL : ((m < 2.5) ? viewDirEQ : ndirStar);
                    return fixed4(0.5 + 0.5 * d, 1.0);
                }

                // =========================
                // Stars (legacy star frame)
                // =========================
                float3 dFlip;
                float2 baseTexel;
                OctaBaseFromDir(ndirStar, dFlip, baseTexel);

                float4 starSum = 0;
                starSum += RetreivePixInfo(dFlip, baseTexel, float2( 0, 0));
                starSum += RetreivePixInfo(dFlip, baseTexel, float2( 1, 0));
                starSum += RetreivePixInfo(dFlip, baseTexel, float2( 1, 1));
                starSum += RetreivePixInfo(dFlip, baseTexel, float2( 0, 1));
                starSum += RetreivePixInfo(dFlip, baseTexel, float2(-1, 1));
                starSum += RetreivePixInfo(dFlip, baseTexel, float2(-1, 0));
                starSum += RetreivePixInfo(dFlip, baseTexel, float2(-1,-1));
                starSum += RetreivePixInfo(dFlip, baseTexel, float2( 0,-1));
                starSum += RetreivePixInfo(dFlip, baseTexel, float2( 1,-1));

                // =========================
                // Milky Way: star frame -> MW frame via your calibrated quaternion
                // =========================
                float3 mwDir = SafeNormalize(QuatRotate(_Q_MW_from_Star, ndirStar));
                half3 mw = texCUBE(_MWCube, mwDir).rgb * _MWbright;
                mw = Desaturate3(mw, _MWDesat);

                half3 bg = mw + starSum.rgb;
                bg = max(bg, _BgColor.rgb);

                // =========================
                // Sun disk (ECL)
                // =========================
                float3 toSunECL = SafeNormalize(_SunDir_ECL.xyz);
                float3 sunRGB = RenderSunPerceptual(viewDirECL, toSunECL);
                bg = (fixed3)((float3)bg + sunRGB);

                // =========================
                // Moon sphere disk (ECL)
                // =========================
                float3 toMoonECL = SafeNormalize(_MoonDir_ECL.xyz);
                float moonMask;
                float3 moonRGB = RenderMoonSphere(viewDirECL, toMoonECL, toSunECL, _Q_MoonBE, moonMask);

                float3 outRGB = lerp(bg, moonRGB, moonMask);

                #if defined(UNITY_COLORSPACE_GAMMA)
                    outRGB = LinearToGammaSpace(outRGB);
                #endif

                return fixed4(outRGB, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}