// The star data are encoded into textures that are uv mapped to the sky with a octahedral projection. The data for any given star is written to a texture pixel at the star's actual location. since the texture is a low resolution for stars (1024x1024) the texture data includes subpixel offsets in the R&G channels which are used to place the star with fairly high precision. Magnitude and temperature are encoded in the remaining channels for the brightness and color.

//Phosphenolic came up with the star shader methodology awhile ago but only somewhat recently have I started turning it into reality.
Shader "Skybox/SkySTarOnly"
{
    Properties
    {
        _StarData ("Mag Data (R16)", 2D) = "white" {}
        _TempData ("Temp Data (R16)", 2D) = "white" {}
        _XoffData ("X Data (R16)", 2D) = "white" {}
        _YoffData ("Y Data (R16)", 2D) = "white" {}

        _PixelSize ("pixelscale", float) = 1024 
        _maxMag ("Mag Limit", float) = 10

        _sigma ("gaussSigma", float)  = 60
        _scaleFactor ("Mag shift (changes mag zero point)", float)  = 0
        _brightnessScale ("LinearBrightnessScale", float)  = 10


        _SkyboxTex ("Milky Way", CUBE) = "" {}
        _MWbright ("MW brightness Scale", float)  = 1

        _RotationY ("Rotation Y", Float) = 0
        _RotationX ("Rotation X", Float) = 0
        _RotationZ ("Rotation Z", Float) = 0
        _CelestialNorthWS ("Celestial North Pole WS", Vector) = (0,1,0,0)
        _RARollDeg        ("RA Roll about Pole (deg)", Range(-180,180)) = 0


    }
    SubShader
    {
        Tags { "RenderType"="Background" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float3 direction : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _StarData;
            sampler2D _TempData;
            sampler2D _XoffData;
            sampler2D _YoffData;


            float _PixelSize;
            float _maxMag;

            float _sigma;

            float _scaleFactor;
            float _MWbright;
            float _brightnessScale;

            samplerCUBE _SkyboxTex;

            float4 _CelestialNorthWS;
            float _RARollDeg;

            float _RotationY;
            float _RotationX;
            float _RotationZ;





            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                    // Convert from Cartesian to spherical coordinates
                o.direction = normalize(v.vertex.xyz);
                return o;
            }

            float decodeMagnitude(float encodedValue) {
                float maxMag = -1.46; // Brightest
                float minMag = _maxMag; // Dimmest
                if (encodedValue ==0)
                {
                    return 40;
                }
                else
                {

                    return (minMag + (minMag - maxMag) * ((encodedValue) *-1));
                }
            }

            float magnitudeToBrightness(float magnitude) {
                // Convert astronomical magnitude to linear brightness
                // Remember: Lower magnitude means brighter star
                // return pow(10.0, (0.0 - magnitude) / 2.5);
                return exp2((-magnitude / 2.5) * 3.32192809489); // log2(10)
            }



            float drawStar(float distance, float sigma) {
                float gaussianIntensity = exp(-pow(distance, 2.0) / (2.0 * sigma * sigma));

                return gaussianIntensity;
            }

            void OctaBaseFromDir(float3 dir, out float3 dFlip, out float2 baseTexel, out float2 uvBase)
            {
                dFlip = float3(-dir.x, dir.y, dir.z);

                float sumAbs = abs(dFlip.x) + abs(dFlip.y) + abs(dFlip.z);
                float3 p = dFlip / sumAbs;

                float2 coord = (p.z >= 0.0)
                    ? p.xy
                    : float2(sign(p.x) * (1.0 - abs(p.y)),
                            sign(p.y) * (1.0 - abs(p.x)));

                float2 uvOct = coord * 0.5 + 0.5;
                uvBase = uvOct;

                float2 pixelSpace = uvOct * _PixelSize;
                baseTexel = floor(pixelSpace);
            }
            
            float3 SafeNormalize(float3 v)
            {
                float len2 = dot(v, v);
                if (len2 < 1e-12) return float3(0,0,0);
                return v * rsqrt(len2);
            }

            float3 RotateAroundAxisRodrigues(float3 v, float3 axisUnit, float angleRad)
            {
                // axisUnit must be normalized
                float s = sin(angleRad);
                float c = cos(angleRad);
                return v * c + cross(axisUnit, v) * s + axisUnit * dot(axisUnit, v) * (1.0 - c);
            }

            // Builds a basis in the SAME SPACE as ndir:
            //   Z axis = north pole
            //   X axis = RA=90 direction (east) on equator
            //   Y axis = RA=180 direction (because RA=0 is -Y, and +Y is opposite)
            //
            // Then converts world/view direction -> "texture frame direction" compatible with your existing encoding.
            float3 ApplyCelestialOrientation(float3 ndir, float3 northInput, float raRollDeg)
            {
                // Normalize desired pole
                float3 N = SafeNormalize(northInput);

                // Reference that preserves your current default:
                // When N = +Z, projecting ref0 = -Y onto the equator gives -Y => RA=0 stays at -Y.
                float3 ref0 = float3(0, -1, 0);

                // Project reference onto equator plane (perp to N)
                float3 E0 = ref0 - N * dot(ref0, N);     // candidate for RA=0 direction
                float e0Len2 = dot(E0, E0);

                // Degeneracy fallback if N ~ ref0
                if (e0Len2 < 1e-8)
                {
                    float3 ref1 = float3(1, 0, 0);
                    E0 = ref1 - N * dot(ref1, N);
                }
                E0 = SafeNormalize(E0);                  // RA=0 direction on equator (before roll)

                // Apply RA roll about the pole
                float rollRad = radians(raRollDeg);
                E0 = RotateAroundAxisRodrigues(E0, N, rollRad);

                // Define RA=90 direction (eastward) to complete right-handed basis
                float3 E90 = SafeNormalize(cross(N, E0));

                // Your *existing* texture frame uses the standard xyz axes such that:
                // default case gives identity (ndir_tex == ndir).
                //
                // In default:
                //   N = +Z
                //   E0 = -Y
                //   E90 = +X
                // so we want:
                //   Xaxis_world = +X = E90
                //   Yaxis_world = +Y = -E0
                //   Zaxis_world = +Z = N
                float3 Xaxis = E90;
                float3 Yaxis = -E0;
                float3 Zaxis = N;

                // Convert direction into that frame (dot with basis)
                float3 ndir_tex;
                ndir_tex.x = dot(ndir, Xaxis);
                ndir_tex.y = dot(ndir, Yaxis);
                ndir_tex.z = dot(ndir, Zaxis);

                return SafeNormalize(ndir_tex);
            }
            


            
            float4 RetreivePixInfo(float3 dir, float3 dFlip, float2 baseTexel, float2 pixelOff) {

                float2  pixelCenter = baseTexel + 0.5 + pixelOff;
                float2  uvCenter = pixelCenter / _PixelSize;

                // float2 uvCenter = float2(u, v) - fmod(float2(u, v), _PixelSize) + 0.5;
                float starData = tex2D(_StarData, uvCenter);
                half3 tempR = tex2D(_TempData, uvCenter);
                float XData = tex2D(_XoffData, uvCenter);
                float YData = tex2D(_YoffData, uvCenter);


                float starBrightness = magnitudeToBrightness(decodeMagnitude(starData)-_scaleFactor);
                float2 coord1;
                coord1.x = (uvCenter.x * 2.0 - 1.0)+(-((((YData-0.25)*2)*3)-1.5)/(_PixelSize));
                coord1.y = (uvCenter.y * 2.0 - 1.0)+(((((XData-0.25)*2)*3)-1.5)/(_PixelSize));

                float3 Pprime1;
                if (abs(coord1.x) + abs(coord1.y) <= 1.0) // Original condition for Pprime.z >= 0
                {
                    Pprime1.xy = coord1;
                    Pprime1.z = 1.0 - abs(coord1.x) - abs(coord1.y); // Invert original front-facing projection
                }
                else // Original condition for Pprime.z < 0, needs guessing
                {
                    // This branch is trickier because the original transformation compresses more information into the same space
                    Pprime1.x = sign(coord1.x) * (1.0 - abs(coord1.y));
                    Pprime1.y = sign(coord1.y) * (1.0 - abs(coord1.x));
                    Pprime1.z = -(1.0 - abs(Pprime1.x) - abs(Pprime1.y)); // Invert original front-facing projection
                }

                float3 pDir = normalize(Pprime1); // This normalization assumes Pprime was a direction vector.
                float3 baseDir = normalize(dFlip); // could be passed in pre-normalized
                float vecDist = length(pDir - baseDir) * 206265.0;

                float intensity = drawStar(vecDist, _sigma);

                starBrightness *= _brightnessScale;

                // Use the brightness as the alpha value
                return float4(tempR, 1.0)*(starBrightness*intensity);

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

            fixed4 Desaturate(fixed4 color, float desaturationAmount) {
                float gray = dot(color.rgb, fixed3(0.299, 0.587, 0.114)); // Luminance calculation
                fixed3 desaturatedColor = lerp(color.rgb, fixed3(gray, gray, gray), desaturationAmount);
                return fixed4(desaturatedColor, color.a); // Maintain original alpha
            }
            
            

            fixed4 frag (v2f i) : SV_Target
            {

                float3 dir = normalize(i.direction); // Normalize just to be safe
                // Convert direction to spherical coordinates (RA, Dec)

                // float3 ndir1 = mul(RotationMatrix(0, 90, 180), float4(i.direction, 1.0)).xyz;
                // float3 ndir = mul(RotationMatrix(_RotationY, 90-_RotationX, -_RotationZ), float4(ndir1, 1.0)).xyz;

                float3 northOS = normalize(mul((float3x3)unity_WorldToObject, _CelestialNorthWS.xyz));

                float3 ndir = ApplyCelestialOrientation(dir, northOS, _RARollDeg);

                // float3 sunDirWS = GetSunDirWS();

  

                float3 dFlip;
                float2 baseTexel, uvBase;
                OctaBaseFromDir(ndir, dFlip, baseTexel, uvBase);

                    // Convert RA and Dec to Cartesian coordinates
                float4 color0 = RetreivePixInfo(ndir, dFlip, baseTexel, float2(0,0));
                float4 color1 = RetreivePixInfo(ndir, dFlip, baseTexel, float2(1,0));
                float4 color2 = RetreivePixInfo(ndir, dFlip, baseTexel, float2(1,1));
                float4 color3 = RetreivePixInfo(ndir, dFlip, baseTexel, float2(0,1));
                float4 color4 = RetreivePixInfo(ndir, dFlip, baseTexel, float2(-1,1));
                float4 color5 = RetreivePixInfo(ndir, dFlip, baseTexel, float2(-1,0));
                float4 color6 = RetreivePixInfo(ndir, dFlip, baseTexel, float2(-1,-1));
                float4 color7 = RetreivePixInfo(ndir, dFlip, baseTexel, float2(0,-1));
                float4 color8 = RetreivePixInfo(ndir, dFlip, baseTexel, float2(1,-1));


                float4x4 rotMatrix = RotationMatrix(300, 171, 156);

                float3 rotatedDir = mul(rotMatrix, float4(ndir, 1.0)).xyz;
                fixed4 skyColor = Desaturate(texCUBE(_SkyboxTex, rotatedDir)*_MWbright,0.6);

                // float3 color = TemperatureToRGB(decodeTemp(-1));
                // Use the brightness as the alpha value
                return (skyColor+color0+color1+color2+color3+color4+color5+color6+color7+color8);
                // 
                // ;
                // return skyColor+color0;
                // return float4(color, 1.0)*0.5;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
