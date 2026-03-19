Shader "Skybox/SkySunMoonEarth"
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


        // --- Sun (vacuum, space-view) ---
        _SunDirEcl   ("Sun Dir (ECL, unit)", Vector) = (1,0,0,0)
        _SunAngRad   ("Sun Angular Radius (rad)", Float) = 0.00463    // ~0.265 deg at 1 AU
        _SunColor    ("Sun Color (HDR)", Color) = (8,8,7.5,1)         // HDR-ish, tweak later
        _SunEdgeSoft ("Sun Edge Softness", Float) = 0.00015           // radians; small

        // --- Sun lens artifacts (stylized) ---
        _SunIntensity   ("Sun Intensity (HDR scalar)", Float) = 25.0

        _SpikeStrength  ("Spike Strength", Float) = 0.6
        _SpikeSharpness ("Spike Sharpness", Float) = 80.0
        _SpikeWidth     ("Spike Width (rad)", Float) = 0.0006
        _SpikeLength    ("Spike Length (rad)", Float) = 0.02
        _SpikeRotateDeg ("Spike Rotation (deg)", Float) = 0.0

        // --- Moon (focus: sphere intersection in ECL frame) ---
        _MoonPosEcl   ("Moon Pos (craft->moon, ECL, meters)", Vector) = (0,0,0,0)
        _MoonRadiusM  ("Moon Radius (m)", Float) = 1737400.0
        _MoonColor    ("Moon Base Color", Color) = (0.75,0.75,0.75,1)
        _MoonAmbient  ("Moon Ambient", Range(0,1)) = 0.02
        _MoonShadowPow("Moon Terminator Power", Float) = 1.0
        // --- Moon textures (Stage C) ---
        _MoonAlbedo ("Moon Albedo (equirect)", 2D) = "gray" {}
        _MoonBodyToEcl ("Moon BodyToEcl quat (xyzw)", Vector) = (0,0,0,1)

        // Optional: quick art controls
        _MoonAlbedoTint ("Moon Albedo Tint", Color) = (1,1,1,1)
        _MoonGamma ("Moon Albedo Gamma", Float) = 1.0

        _MoonLonOffsetDeg ("Moon Lon Offset (deg)", Float) = 180
        _MoonFlipU ("Moon Flip U (0/1)", Float) = 0
        _MoonFlipV ("Moon Flip V (0/1)", Float) = 1
        

        // --- Earth (secondary body for moon-dominant shader) ---
        _EarthPosEcl   ("Earth Pos (craft->earth, ECL, meters)", Vector) = (0,0,0,0)
        _EarthRadiusM  ("Earth Radius (m)", Float) = 6371000.0
        _EarthAmbient  ("Earth Ambient", Range(0,1)) = 0.03
        _EarthShadowPow("Earth Terminator Power", Float) = 1.0

        _EarthAlbedo ("Earth Albedo (equirect)", 2D) = "white" {}
        _EarthBodyToEcl ("Earth BodyToEcl quat (xyzw)", Vector) = (0,0,0,1)

        _EarthAlbedoTint ("Earth Albedo Tint", Color) = (1,1,1,1)
        _EarthGamma ("Earth Albedo Gamma", Float) = 1.0

        _EarthLonOffsetDeg ("Earth Lon Offset (deg)", Float) = 180
        _EarthFlipU ("Earth Flip U (0/1)", Float) = 0
        _EarthFlipV ("Earth Flip V (0/1)", Float) = 1

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

            float4 _SunDirEcl;   // xyz unit vector, ecliptic
            float  _SunAngRad;   // radians
            float4 _SunColor;    // HDR
            float  _SunEdgeSoft; // radians
            float _SunIntensity;

            float _SpikeStrength;
            float _SpikeSharpness;
            float _SpikeWidth;
            float _SpikeLength;
            float _SpikeRotateDeg;


            float4 _MoonPosEcl;   // xyz meters, craft->moon
            float  _MoonRadiusM;  // meters
            float4 _MoonColor;
            float  _MoonAmbient;
            float  _MoonShadowPow;

            sampler2D _MoonAlbedo;
            float4 _MoonBodyToEcl;   // xyzw, body-fixed -> ecl inertial
            float4 _MoonAlbedoTint;
            float  _MoonGamma;

            float _MoonLonOffsetDeg;
            float _MoonFlipU;
            float _MoonFlipV;

            float4 _EarthPosEcl;
            float  _EarthRadiusM;
            float  _EarthAmbient;
            float  _EarthShadowPow;

            sampler2D _EarthAlbedo;
            float4 _EarthBodyToEcl;
            float4 _EarthAlbedoTint;
            float  _EarthGamma;

            float _EarthLonOffsetDeg;
            float _EarthFlipU;
            float _EarthFlipV;


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

            float4 QuatConjugate(float4 q)
            {
                return float4(-q.x, -q.y, -q.z, q.w);
            }            


            float2 MoonUV_Equirect(float3 nB)
            {
                // nB is unit normal in moon body-fixed frame:
                //  +Z = north pole.
                //  Longitude 0 at +Y (by choice here), increasing toward +X (right-handed).
                float lon = atan2(nB.x, nB.y);      // [-pi, pi]
                float lat = asin(clamp(nB.z, -1.0, 1.0)); // [-pi/2, pi/2]

                float u = lon * (0.15915494309189535) + 0.5; // 1/(2pi)
                float v = lat * (0.3183098861837907) + 0.5;  // 1/pi

                return float2(u, v);
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


            float3 EclToMoonBody(float3 vEcl)
            {
                // _MoonBodyToEcl is body->ecl, so inverse maps ecl->body
                float4 qE2B = QuatConjugate(_MoonBodyToEcl);
                return SafeNormalize(RotateByQuat(vEcl, qE2B));
            }





            float2 EarthUV_Equirect(float3 nB)
            {
                float lon = atan2(nB.x, nB.y);
                float lat = asin(clamp(nB.z, -1.0, 1.0));

                float u = lon * (0.15915494309189535) + 0.5;
                float v = lat * (0.3183098861837907) + 0.5;

                return float2(u, v);
            }

            float3 EclToEarthBody(float3 vEcl)
            {
                float4 qE2B = QuatConjugate(_EarthBodyToEcl);
                return SafeNormalize(RotateByQuat(vEcl, qE2B));
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

            void BuildSunBasis(float3 sunDirEq, out float3 t1, out float3 t2)
            {
                // Pick a stable reference not parallel to sunDir
                float3 ref = (abs(sunDirEq.z) < 0.9) ? float3(0,0,1) : float3(0,1,0);
                t1 = SafeNormalize(cross(ref, sunDirEq));
                t2 = SafeNormalize(cross(sunDirEq, t1));
            }
            void RotateBasis(inout float3 t1, inout float3 t2, float deg)
            {
                float a = radians(deg);
                float s = sin(a);
                float c = cos(a);
                float3 u = t1;
                float3 v = t2;
                t1 = u * c + v * s;
                t2 = -u * s + v * c;
            }
            float EvalSpikes(float3 rayEq, float3 sunDirEq)
            {
                rayEq = SafeNormalize(rayEq);
                sunDirEq = SafeNormalize(sunDirEq);

                // Angular separation proxy via dot
                float cosAng = dot(rayEq, sunDirEq);

                // We want spikes mainly close to the sun direction:
                // Use a soft angular gate around the sun (in cos-space, stable).
                float cosGate = cos(_SunAngRad + _SpikeLength); // larger than disk
                float gate = saturate((cosAng - cosGate) / max(1e-5, (1.0 - cosGate)));

                // Tangent basis around sunDir
                float3 t1, t2;
                BuildSunBasis(sunDirEq, t1, t2);
                RotateBasis(t1, t2, _SpikeRotateDeg);

                // Project ray into tangent plane coordinates
                float u = dot(rayEq, t1);
                float v = dot(rayEq, t2);

                // "Width" controls how thin the spikes are (in radians-ish)
                // Use an exponential falloff from the axes lines (u=0 or v=0).
                float w = max(1e-6, _SpikeWidth);
                float spikeU = exp(-abs(v) / w); // bright along t1 axis (v ~ 0)
                float spikeV = exp(-abs(u) / w); // bright along t2 axis (u ~ 0)

                // Sharpen
                spikeU = pow(saturate(spikeU), _SpikeSharpness);
                spikeV = pow(saturate(spikeV), _SpikeSharpness);

                float spikes = (spikeU + spikeV);

                // Gate by proximity to sun so it doesn't affect the whole screen
                return spikes * gate * _SpikeStrength;
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

            float3 EvalSun(float3 rayEq, float3 sunDirEq)
            {
                float cosAng = dot(SafeNormalize(rayEq), SafeNormalize(sunDirEq));
                float cosLim = cos(_SunAngRad);
                float soft   = max(1e-6, _SunEdgeSoft);

                float disk = smoothstep(cosLim - soft, cosLim + soft, cosAng);

                float spikes = EvalSpikes(rayEq, sunDirEq);

                // Bloom hook: push HDR intensity
                float3 col = _SunColor.rgb * _SunIntensity * disk;

                // Spikes are also bright but usually less than the disk
                col += _SunColor.rgb * _SunIntensity * spikes;

                return col;
            }

            bool RaySphereHit(float3 D_unit, float3 C, float R, out float tHit)
            {
                // Ray: P(t) = t * D, origin at (0,0,0)
                // Sphere: |P - C|^2 = R^2

                float b = dot(D_unit, C);
                float c = dot(C, C) - R * R;
                float h = b * b - c;

                if (h < 0.0)
                {
                    tHit = 0.0;
                    return false;
                }

                float s = sqrt(h);

                // nearest positive hit
                float t0 = b - s;
                float t1 = b + s;

                // Choose the smallest positive t
                tHit = (t0 > 0.0) ? t0 : ((t1 > 0.0) ? t1 : 0.0);

                return (tHit > 0.0);
            }

            float4 EvalMoon(float3 rayEcl_unit, float3 sunDirEcl_unit)
            {
                float3 C = _MoonPosEcl.xyz;   // meters
                float  R = _MoonRadiusM;

                float t;
                if (!RaySphereHit(rayEcl_unit, C, R, t))
                    return float4(0,0,0,0);

                // Hit point and normal in ECL space
                float3 P = rayEcl_unit * t;
                float3 N = SafeNormalize(P - C);

                // Simple Lambert (vacuum)
                float nl = saturate(dot(N, sunDirEcl_unit));
                nl = pow(nl, max(1e-3, _MoonShadowPow)); // =1 for true Lambert

                float shade = max(_MoonAmbient, nl);


                // Convert ECL normal to moon body-fixed for texturing
                float3 nB = EclToMoonBody(N);
                float2 uv = MoonUV_Equirect(nB);

                uv.x = frac(uv.x + (_MoonLonOffsetDeg / 360.0));
                if (_MoonFlipU > 0.5) uv.x = 1.0 - uv.x;
                if (_MoonFlipV > 0.5) uv.y = 1.0 - uv.y;

                // Sample albedo
                float3 albedo = tex2D(_MoonAlbedo, uv).rgb;

                // Optional gamma/tint (leave gamma=1 unless needed)
                albedo = pow(max(albedo, 0.0), _MoonGamma);
                albedo *= _MoonAlbedoTint.rgb;

                float3 col = albedo * shade;

                return float4(col, 1.0);
            }


            float4 EvalEarth(float3 rayEcl_unit, float3 sunDirEcl_unit)
            {
                float3 C = _EarthPosEcl.xyz;
                float  R = _EarthRadiusM;

                float t;
                if (!RaySphereHit(rayEcl_unit, C, R, t))
                    return float4(0,0,0,0);

                float3 P = rayEcl_unit * t;
                float3 N = SafeNormalize(P - C);

                float nl = saturate(dot(N, sunDirEcl_unit));
                nl = pow(nl, max(1e-3, _EarthShadowPow));

                float shade = max(_EarthAmbient, nl);

                float3 nB = EclToEarthBody(N);
                float2 uv = EarthUV_Equirect(nB);

                uv.x = frac(uv.x + (_EarthLonOffsetDeg / 360.0));
                if (_EarthFlipU > 0.5) uv.x = 1.0 - uv.x;
                if (_EarthFlipV > 0.5) uv.y = 1.0 - uv.y;

                float3 albedo = tex2D(_EarthAlbedo, uv).rgb;
                albedo = pow(max(albedo, 0.0), _EarthGamma);
                albedo *= _EarthAlbedoTint.rgb;

                float3 col = albedo * shade;
                return float4(col, 1.0);
            }




            fixed4 frag(v2f i) : SV_Target
            {
                float3 dirB = SafeNormalize(float3(-i.dir.x, i.dir.y, i.dir.z));

                // Body -> Equatorial (RA/Dec-baked frame)
                float3 dirEq = SafeNormalize(RotateByQuat(dirB, _CraftBodyToEq));

                // --- Sun ---
                float3 sunDirEcl = SafeNormalize(_SunDirEcl.xyz);
                float4 moonCol   = EvalMoon(dirEq, sunDirEcl);
                float4 earthCol = EvalEarth(dirEq, sunDirEcl);

                float3 sunCol = EvalSun(dirEq, sunDirEcl);

                // Moon-dominant shader policy:
                // moon wins first, earth is secondary fallback
                if (moonCol.a > 0.5)
                    return float4(moonCol.rgb, 1.0);

                if (earthCol.a > 0.5)
                    return float4(earthCol.rgb, 1.0);

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

                return mw + (s0+s1+s2+s3+s4+s5+s6+s7+s8)+ float4(sunCol, 0);
            }
            ENDCG
        }
    }

    FallBack Off
}
