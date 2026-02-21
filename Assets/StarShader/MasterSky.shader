// The star data are encoded into textures that are uv mapped to the sky with a octahedral projection. The data for any given star is written to a texture pixel at the star's actual location. since the texture is a low resolution for stars (1024x1024) the texture data includes subpixel offsets in the R&G channels which are used to place the star with fairly high precision. Magnitude and temperature are encoded in the remaining channels for the brightness and color.

//Phosphenolic came up with the star shader methodology awhile ago but only somewhat recently have I started turning it into reality.
Shader "Skybox/SkyMaster"
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

        _UseSceneLightForSun ("Use Scene Directional Light for Sun Dir", Range(0,1)) = 1
        _SunDirWS ("Sun Direction WS (override)", Vector) = (0,1,0,0)
        _FlipSun ("Flip Sun Sign", Range(0,1)) = 0




        //# Moon parameters
        _MoonEquirect ("Moon Texture (2D)", 2D) = "white" {}
        _MoonNormal ("Moon Normal (tangent)", 2D) = "bump" {}

        _MoonAngularRadiusDeg ("Moon Angular Radius (deg)", Range(0.05, 5.0)) = 0.27
        _MoonEdgeSoftness ("Moon Edge Softness (pixels)", Range(0, 4.0)) = 2

        _MoonRotYawDeg   ("Moon Body Yaw (deg)",   Range(-180,180)) = 0
        _MoonRotPitchDeg ("Moon Body Pitch (deg)", Range(-90,90))   = 0
        _MoonRotRollDeg  ("Moon Body Roll (deg)",  Range(-180,180)) = 0

        _MoonDirWS ("Moon Direction WS (to Moon)", Vector) = (0, 0, 1, 0)
        _LunarNorthPoleWS ("Lunar North Pole WS", Vector) = (0, 1, 0, 0)

        _TerminatorPower ("Terminator Power", Range(0.4, 2.0)) = 1
        _Ambient ("Ambient / Earthshine", Range(0,0.5)) = 0.003

        _NormalStrength ("Normal Strength", Range(0, 5)) = 3.0

        _MoonExtRGB ("Moon Extinction RGB", Vector) = (0.02, 0.06, 0.20, 0)
        _MoonExtStrength ("Moon Ext Strength", Range(0, 6)) = 1.0

        _SkyBaseNight ("Sky Base (night)", Color) = (0.002, 0.003, 0.006, 1)
        _SkyExtRGB ("Sky Extinction RGB", Vector) = (0.02, 0.04, 0.08, 0)
        _SkyExtStrength ("Sky Ext Strength", Range(0,2)) = 1
        _MoonSkyEnable ("Moon Sky Enable", Range(0,1)) = 1
        _MoonSkyStrength ("Moon Sky Strength", Range(0,2)) = 0.35
        _MoonSkyPhasePower ("Moon Sky Phase Power", Range(0.5, 4)) = 2.0
        _MoonSkyHorizonBoost ("Moon Sky Horizon Boost", Range(0,2)) = 1.0
        _MoonSkyColor ("Moon Sky Color", Color) = (0.35, 0.42, 0.55, 1)
        _MoonSkyWidthDeg ("Moon Sky Width (deg)", Range(5, 120)) = 60

        _RayleighStrengthMoon ("Rayleigh Strength", Range(0,2)) = 0.35
        _MieStrengthMoon ("Mie Strength", Range(0,2)) = 0.65
        _MieGMoon ("Mie g", Range(0.0, 0.98)) = 0.85

        _HaloEnable ("Halo Enable", Range(0,1)) = 1
        _HaloStrength ("Halo Strength", Range(0,1)) = 0.08
        _HaloWidthDeg ("Halo Width (deg)", Range(0.1, 10.0)) = 1.0
        _HaloTint ("Halo Tint", Color) = (0.7, 0.75, 0.85, 1)
        _HaloPhasePower ("Halo Phase Power", Range(0.5, 4)) = 2.0

        _OppositionEnable ("Opposition Enable", Range(0,1)) = 0
        _OppositionBoost  ("Opposition Boost", Range(0,5)) = 1.5
        _OppositionPower  ("Opposition Power", Range(0.5,12)) = 4.0

        _GlareEnable ("Glare Enable", Range(0,1)) = 0
        _GlareStrength ("Glare Strength", Range(0,1)) = 0.06
        _GlareWidthDeg ("Glare Width (deg)", Range(2, 40)) = 12
        _GlarePhasePower ("Glare Phase Power", Range(0.5, 6)) = 2.5
        _GlareTint ("Glare Tint", Color) = (0.8, 0.85, 1.0, 1)

        _MoonHDRBoost ("Moon HDR Boost", Range(0, 50)) = 1

        _StarDimEnable ("Star Dim Enable", Range(0,1)) = 0
        _StarDimStrength ("Star Dim Strength", Range(0,1)) = 0.6
        _StarDimWidthDeg ("Star Dim Width (deg)", Range(1, 60)) = 15
        _StarDimPhasePower ("Star Dim Phase Power", Range(0.5, 6)) = 2.0
        _StarDimFloor ("Star Dim Floor", Range(0,1)) = 0.15

        //#Sun parameters
        _SunAngularRadiusDeg ("Sun Angular Radius (deg)", Range(0.05, 5.0)) = 0.2666
        _SunEdgeSoftness ("Sun Edge Softness (pixels)", Range(0, 6.0)) = 2.0

        _SunColor ("Sun Color (above atmosphere)", Color) = (1, 0.98, 0.92, 1)
        _SunIntensity ("Sun Intensity", Range(0, 250)) = 10.0

        _AtmoEnable ("Atmo Enable", Range(0,1)) = 1
        _AtmoUpWS ("Atmosphere Up WS", Vector) = (0,1,0,0)

        _SunExtRGB ("Sun Extinction RGB", Vector) = (0.06, 0.12, 0.35, 0)
        _SunExtStrength ("Sun Ext Strength", Range(0, 50)) = 1.0

        _SunHorizonFadeDeg ("Sun Horizon Fade (deg)", Range(0, 5)) = 0.5

        _SkyEnable ("Sky Enable", Range(0,1)) = 1
        _SkyBase ("Sky Base", Color) = (0,0,0,1)
        _SkyStrength ("Sky Strength", Range(0, 10)) = 1.0
        _SkyExtRGBSun ("Sky Extinction RGB", Vector) = (0.02, 0.04, 0.08, 0)
        _SkyExtStrengthSun ("Sky Ext Strength", Range(0, 6)) = 1.0





        _RayleighStrength ("Rayleigh Strength", Range(0, 10)) = 1.0
        _MieStrength ("Mie Strength", Range(0, 10)) = 0.5
        _MieG ("Mie g", Range(0.0, 0.98)) = 0.85
        _RayleighTint ("Rayleigh Tint", Color) = (0.2, 0.6, 1.0, 1)
        _MieTint ("Mie Tint", Color) = (1.0, 0.95, 0.9, 1)

        _MieExtRGB ("Mie Extinction RGB", Vector) = (0.06, 0.10, 0.18, 0)
        _MieExtStrength ("Mie Ext Strength", Range(0, 12)) = 3.0

        _MieClampDeg ("Mie Clamp Angle (deg)", Range(0.1, 10)) = 2.0

        _TwilightStartDeg ("Twilight Start (deg)", Range(-30, 0)) = -18.0
        _TwilightEndDeg   ("Twilight End (deg)",   Range(-10, 10)) = 0.0

        _EarthShadowAltDeg ("Earth Shadow Alt Scale (deg)", Range(0.5, 15)) = 5.0
        _EarthShadowStrength ("Earth Shadow Strength", Range(0,1)) = 0.6
        _EarthShadowMu0Max   ("Earth Shadow Mu0 Max", Range(-1, 0.5)) = -0.35
        _EarthShadowSoftness ("Earth Shadow Softness", Range(0.01, 1.0)) = 0.25

        _GroundEnable ("Ground Enable", Range(0,1)) = 1
        _GroundColorDay ("Ground Color (Day)", Color) = (0.35, 0.35, 0.35, 1)
        _GroundColorNight ("Ground Color (Night)", Color) = (0.03, 0.03, 0.03, 1)
        _GroundTwilightLift ("Ground Twilight Lift", Range(0,1)) = 0.35
        _GroundDayAltDeg ("Ground Day Alt (deg)", Range(0, 45)) = 10

        _HorizonHeight ("Horizon Height", Range(-0.2, 0.2)) = 0.0
        _HorizonSoftness ("Horizon Softness", Range(0.001, 0.5)) = 0.08

        _RefractionEnable ("Refraction Enable", Range(0,1)) = 1
        _RefractionStrength ("Refraction Strength", Range(0,2)) = 1.0
        _RefractionMaxDeg ("Refraction Max (deg)", Range(0, 2)) = 0.8

        _DiskBelowHorizonDeg ("Disk Below Horizon (deg)", Range(0, 2)) = 0.6
        _FlattenMin ("Flatten Min (fraction)", Range(0.3, 1.0)) = 0.55

        _DitherAmp ("Dither Amp (1/255 units)", Range(0, 2)) = 0.1


        // --- Visibility suppression driven by sun-lit sky brightness ---
        _StarSunDimEnable ("Star/Sun Dimming Enable", Range(0,1)) = 1
        _StarSunDimStrength ("Star Dim Strength", Range(0,100)) = 4.0
        _StarSunDimPower ("Star Dim Power", Range(0.5,6)) = 2.0
        _StarSunDimFloor ("Star Dim Floor", Range(0,1)) = 0.0

        // Optional: extra “sunset holds stars back longer” driven by Mie
        _StarSunMieWeight ("Star Dim Mie Weight", Range(0,500)) = 1.0

        _MoonSunDimEnable ("Moon Dim by SunSky", Range(0,1)) = 1
        _MoonSunDimStrength ("Moon Dim Strength", Range(0,2)) = 0.35
        _MoonSunDimPower ("Moon Dim Power", Range(0.5,6)) = 1.5
        _MoonSunDimFloor ("Moon Dim Floor", Range(0,1)) = 0.6

        _StarSunDayFloor ("Star Day Floor", Range(0,5)) = 0.8
        _StarSunDayAltKneeDeg ("Star Day Knee Alt (deg)", Range(-10,20)) = 2.0

        // ================================
        // Sun perceptual optics
        // (glare, veiling, diffraction spikes)
        // ================================

        // --- Glare / veiling glare ---
        _SunPercept_GlareEnable        ("Sun Glare Enable", Range(0,1)) = 1
        _SunPercept_GlareStrength      ("Sun Glare Strength", Range(0,10)) = 2.0
        _SunPercept_GlareRadiusDeg     ("Sun Glare Radius (deg)", Range(0.1,10)) = 3.0
        _SunPercept_GlarePower         ("Sun Glare Falloff Power", Range(0.5,8)) = 2.5

        // How much glare remains when NOT staring directly at the sun
        // 0 = glare only when staring
        // 1 = glare equally strong when glancing
        _SunPercept_GlareGlanceScale   ("Sun Glare Glance Retention", Range(0,1)) = 0.35

        // How much glare washes out (veils) the sun disk when staring
        _SunPercept_VeilStrength       ("Sun Disk Veiling Strength", Range(0,2)) = 0.6


        // --- Diffraction spikes (eye / lens perception, not physical sun) ---
        _SunPercept_SpikeEnable        ("Sun Diffraction Spikes Enable", Range(0,1)) = 1
        _SunPercept_SpikeStrength      ("Sun Spike Strength", Range(0,5)) = 0.6

        // Typical values:
        // 4  = simple cross (very eye-like)
        // 6–8 = subtle starburst
        _SunPercept_SpikeCount         ("Sun Spike Count", Range(2,12)) = 4

        // Higher = thinner spikes
        _SunPercept_SpikeSharpness     ("Sun Spike Sharpness", Range(1,64)) = 14

        // Radial falloff from disk edge
        _SunPercept_SpikeFalloffPow    ("Sun Spike Falloff Power", Range(0.5,6)) = 2.0

        // Spike length measured in sun radii
        _SunPercept_SpikeOuterRadii    ("Sun Spike Length (radii)", Range(1,8)) = 3.0


        // --- Eye adaptation / staring response ---
        // Angular distance from sun center where "staring" begins/ends
        _SunPercept_StareStartDeg      ("Sun Stare Start Angle (deg)", Range(0,20)) = 3.0
        _SunPercept_StareEndDeg        ("Sun Stare End Angle (deg)", Range(0,20)) = 8.0

        // ================================
        // Lunar eclipse (Moon shading)
        // ================================
        _LunarEclipseEnable     ("Lunar Eclipse Enable", Range(0,1)) = 0

        // Angular radii of Earth shadow as seen on the sky around the Moon (deg)
        // Good starting points for visuals (tweak by eye)
        _LunarUmbraRadiusDeg    ("Lunar Umbra Radius (deg)", Range(0.1, 3.0)) = 0.70
        _LunarPenumbraRadiusDeg ("Lunar Penumbra Radius (deg)", Range(0.1, 10.0)) = 2.80

        // Extra softness in pixels on top of derivative AA
        _LunarEclipseSoftnessPx ("Lunar Eclipse Softness (px)", Range(0, 6)) = 2.0

        // Brightness floors inside penumbra/umbra (0 = black, 1 = unchanged)
        _LunarPenumbraMinLight  ("Lunar Penumbra Min Light", Range(0,1)) = 0.65
        _LunarUmbraMinLight     ("Lunar Umbra Min Light", Range(0,1)) = 0.08

        // Red tint in umbra (multiply albedo-lit color toward this tint)
        _LunarUmbraTint         ("Lunar Umbra Tint", Color) = (1.0, 0.35, 0.18, 1)
        _LunarUmbraTintStrength ("Lunar Umbra Tint Strength", Range(0,1)) = 0.85
        _LunarUmbraTintPower    ("Lunar Umbra Tint Power", Range(0.5, 6)) = 2.0

        // How much the moon “glow” features should diminish during eclipse (halo/glare/moon-sky)
        _LunarEclipseGlowDamp   ("Lunar Eclipse Glow Damp", Range(0,1)) = 0.85

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
            #include "Assets/Shaders/Includes/Helpers.cginc"
            #include "Assets/Shaders/Includes/Moon.cginc"
            #include "Assets/Shaders/Includes/Sun.cginc"
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

            float _UseSceneLightForSun;
            float4 _SunDirWS;
            float _FlipSun;



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
            

            float3 GetSunDirWS()
            {
                float3 sunDirWS = normalize(_SunDirWS.xyz);
                if (_UseSceneLightForSun > 0.5)
                    sunDirWS = normalize(_WorldSpaceLightPos0.xyz);
                if (_FlipSun > 0.5)
                    sunDirWS = -sunDirWS;
                return sunDirWS;
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

                MoonResult m = RenderMoon(dir, normalize(_SunDirWS.xyz));
                SunResult s = RenderSunSky(dir, normalize(_WorldSpaceLightPos0.xyz));

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
                return clamp((skyColor+color0+color1+color2+color3+color4+color5+color6+color7+color8)*(1-m.mask)*m.starMul*s.starMul,0,1)+(float4(m.addRGB,1)*s.moonMul)+(float4(s.addRGB,1));
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
