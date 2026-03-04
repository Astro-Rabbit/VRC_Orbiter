Shader "Skybox/SkySTarOnly_OrbiterEcl_MIN"
{
    Properties
    {
        _StarData ("Mag Data (R16)", 2D) = "white" {}
        _TempData ("Temp Data (R16)", 2D) = "white" {}
        _XoffData ("X Data (R16)", 2D) = "white" {}
        _YoffData ("Y Data (R16)", 2D) = "white" {}

        _PixelSize ("pixelscale", float) = 1024
        _maxMag ("Mag Limit", float) = 10

        _sigma ("gaussSigma", float) = 60
        _scaleFactor ("Mag shift", float) = 0
        _brightnessScale ("LinearBrightnessScale", float) = 10

        _SkyboxTex ("Milky Way", CUBE) = "" {}
        _MWbright ("MW brightness Scale", float) = 1

        // Quaternion (x,y,z,w): BODY -> EQUATORIAL (RA/Dec bake frame)
        _CraftBodyToEq ("Craft BodyToEq quat (xyzw)", Vector) = (0,0,0,1)

        // Obliquity epsilon in degrees (equatorial -> ecliptic)
        _ObliquityDeg ("Obliquity (deg)", Float) = 23.439281
    }

    SubShader
    {
        Tags { "RenderType"="Background" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 dir    : TEXCOORD0;
            };

            sampler2D _StarData;
            sampler2D _TempData;
            sampler2D _XoffData;
            sampler2D _YoffData;

            float _PixelSize;
            float _maxMag;
            float _sigma;
            float _scaleFactor;
            float _brightnessScale;

            samplerCUBE _SkyboxTex;
            float _MWbright;

            float4 _CraftBodyToEq; // xyzw
            float  _ObliquityDeg;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.dir = normalize(v.vertex.xyz);
                return o;
            }

            float3 SafeNormalize(float3 v)
            {
                float len2 = dot(v,v);
                if (len2 < 1e-12) return float3(0,0,0);
                return v * rsqrt(len2);
            }

            // Active quaternion rotation: q=(x,y,z,w)
            float3 RotateByQuat(float3 v, float4 q)
            {
                float3 u = q.xyz;
                float  s = q.w;
                float3 uv  = cross(u, v);
                float3 uuv = cross(u, uv);
                return v + 2.0 * (s * uv + uuv);
            }

            // Equatorial -> Ecliptic rotation about +X by +epsilon
            float3 EqToEcl(float3 dEq, float obliquityDeg)
            {
                float eps = obliquityDeg * 0.017453292519943295; // pi/180
                float c = cos(eps);
                float s = sin(eps);
                return float3(
                    dEq.x,
                    dEq.y * c + dEq.z * s,
                   -dEq.y * s + dEq.z * c
                );
            }

            // Ecliptic -> your octa/star texture frame convention
            // Want RA0 = +X_ecl (vernal). Encoding uses RA0 = -Y_tex.
            // So: X_tex=+Y_ecl, Y_tex=-X_ecl, Z_tex=+Z_ecl.
            float3 EclToStarTexFrame(float3 eclDir)
            {
                float3 dTex;
                dTex.x = eclDir.y;
                dTex.y = -eclDir.x;
                dTex.z = eclDir.z;
                return SafeNormalize(dTex);
            }

            float decodeMagnitude(float encodedValue)
            {
                float maxMag = -1.46;
                float minMag = _maxMag;
                if (encodedValue == 0) return 40.0;
                return (minMag + (minMag - maxMag) * (encodedValue * -1.0));
            }

            float magnitudeToBrightness(float magnitude)
            {
                return exp2((-magnitude / 2.5) * 3.32192809489);
            }

            float drawStar(float distanceArcsec, float sigmaArcsec)
            {
                return exp(-(distanceArcsec * distanceArcsec) / (2.0 * sigmaArcsec * sigmaArcsec));
            }

            void OctaBaseFromDir(float3 dir, out float3 dFlip, out float2 baseTexel)
            {
                dFlip = float3(-dir.x, dir.y, dir.z);

                float sumAbs = abs(dFlip.x) + abs(dFlip.y) + abs(dFlip.z);
                float3 p = dFlip / sumAbs;

                float2 coord;
                if (p.z >= 0.0)
                {
                    coord = p.xy;
                }
                else
                {
                    coord = float2(sign(p.x) * (1.0 - abs(p.y)),
                                   sign(p.y) * (1.0 - abs(p.x)));
                }

                float2 uvOct = coord * 0.5 + 0.5;
                float2 pixelSpace = uvOct * _PixelSize;
                baseTexel = floor(pixelSpace);
            }

            float4 RetreivePixInfo(float3 dFlip, float2 baseTexel, float2 pixelOff)
            {
                float2 pixelCenter = baseTexel + 0.5 + pixelOff;
                float2 uvCenter = pixelCenter / _PixelSize;

                float starData = tex2D(_StarData, uvCenter);
                half3 tempR   = tex2D(_TempData, uvCenter);
                float XData   = tex2D(_XoffData, uvCenter);
                float YData   = tex2D(_YoffData, uvCenter);

                float starBrightness = magnitudeToBrightness(decodeMagnitude(starData) - _scaleFactor);
                starBrightness *= _brightnessScale;

                float2 coord1;
                coord1.x = (uvCenter.x * 2.0 - 1.0) + (-((((YData - 0.25) * 2.0) * 3.0) - 1.5) / _PixelSize);
                coord1.y = (uvCenter.y * 2.0 - 1.0) + ( ((((XData - 0.25) * 2.0) * 3.0) - 1.5) / _PixelSize);

                float3 Pprime1;
                if (abs(coord1.x) + abs(coord1.y) <= 1.0)
                {
                    Pprime1.xy = coord1;
                    Pprime1.z  = 1.0 - abs(coord1.x) - abs(coord1.y);
                }
                else
                {
                    Pprime1.x = sign(coord1.x) * (1.0 - abs(coord1.y));
                    Pprime1.y = sign(coord1.y) * (1.0 - abs(coord1.x));
                    Pprime1.z = -(1.0 - abs(Pprime1.x) - abs(Pprime1.y));
                }

                float3 pDir = normalize(Pprime1);
                float3 baseDir = normalize(dFlip);
                float vecDistArcsec = length(pDir - baseDir) * 206265.0;

                float intensity = drawStar(vecDistArcsec, _sigma);

                return float4(tempR, 1.0) * (starBrightness * intensity);
            }

            fixed4 Desaturate(fixed4 color, float amount)
            {
                float gray = dot(color.rgb, fixed3(0.299, 0.587, 0.114));
                fixed3 d = lerp(color.rgb, fixed3(gray, gray, gray), amount);
                return fixed4(d, color.a);
            }

            float3 RotX(float3 v, float deg)
            {
                float a = deg * 0.017453292519943295;
                float c = cos(a), s = sin(a);
                return float3(v.x, v.y*c - v.z*s, v.y*s + v.z*c);
            }

            float4x4 RotationMatrix(float y, float x, float z) {
                // Convert angles from degrees to radians
                x = radians(x);
                y = radians(y);
                z = radians(z);
            
                // Precompute sine and cosine
                float sinX = sin(x);
                float cosX = cos(x);
                float sinY = sin(y);
                float cosY = cos(y);
                float sinZ = sin(z);
                float cosZ = cos(z);
            
                // Construct the rotation matrix
                float4x4 rotMatrix  = float4x4(
                    cosY * cosZ, cosZ * sinX * sinY - cosX * sinZ, cosX * cosZ * sinY + sinX * sinZ, 0,
                    cosY * sinZ, cosX * cosZ + sinX * sinY * sinZ, -cosZ * sinX + cosX * sinY * sinZ, 0,
                    -sinY,      cosY * sinX,                      cosX * cosY,                      0,
                    0,          0,                                0,                                1
                );
            
                return rotMatrix ;
            }            

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dirB = SafeNormalize(i.dir);

                // Body -> Equatorial (RA/Dec-baked frame)
                float3 dirEq = SafeNormalize(RotateByQuat(dirB, _CraftBodyToEq));
                float3 dirEqAligned = RotX(dirEq, 00.0);

                // Equatorial -> Ecliptic (sim frame)

                float3 dirEcl = SafeNormalize(EqToEcl(dirEq, _ObliquityDeg));

                // Ecliptic -> texture frame expected by your octa encoding
                float3 ndir = EclToStarTexFrame(dirEcl);

                float3 dFlip;
                float2 baseTexel;
                OctaBaseFromDir(ndir, dFlip, baseTexel);

                float4 s0 = RetreivePixInfo(dFlip, baseTexel, float2(0,0));
                float4 s1 = RetreivePixInfo(dFlip, baseTexel, float2(1,0));
                float4 s2 = RetreivePixInfo(dFlip, baseTexel, float2(1,1));
                float4 s3 = RetreivePixInfo(dFlip, baseTexel, float2(0,1));
                float4 s4 = RetreivePixInfo(dFlip, baseTexel, float2(-1,1));
                float4 s5 = RetreivePixInfo(dFlip, baseTexel, float2(-1,0));
                float4 s6 = RetreivePixInfo(dFlip, baseTexel, float2(-1,-1));
                float4 s7 = RetreivePixInfo(dFlip, baseTexel, float2(0,-1));
                float4 s8 = RetreivePixInfo(dFlip, baseTexel, float2(1,-1));


                float4x4 rotMatrix = RotationMatrix(300, 171, 156);

                float3 rotatedDir = mul(rotMatrix, float4(ndir, 1.0)).xyz;
                fixed4 mw = Desaturate(texCUBE(_SkyboxTex, rotatedDir)*_MWbright,0.6);


                // fixed4 mw = Desaturate(texCUBE(_SkyboxTex, ndir) * _MWbright, 0.6);

                return mw + (s0+s1+s2+s3+s4+s5+s6+s7+s8);
            }
            ENDCG
        }
    }

    FallBack Off
}
