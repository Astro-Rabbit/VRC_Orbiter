Shader "HUD/CollimatedDock"
{
    Properties
    {
        _HudMode ("HUD Mode", Float) = 3

        _HudHalfFovX ("HUD Half FOV X (rad)", Float) = 0.25
        _HudHalfFovY ("HUD Half FOV Y (rad)", Float) = 0.18

        _HudIntensity ("HUD Intensity", Float) = 1.0
        _HudColor ("HUD Color", Color) = (0.45, 1.0, 0.55, 1.0)

        _LineWidth ("Center Cross Thickness", Float) = 0.004
        _Softness ("Edge Softness", Float) = 1.5

        _GlassTint ("Glass Tint", Color) = (0.15, 0.35, 0.18, 1.0)
        _GlassAlpha ("Glass Alpha", Range(0,1)) = 0.08
        _GlassFresnel ("Glass Fresnel", Float) = 1.5

        _FontAtlas ("Font Atlas", 2D) = "white" {}
        _FontSdfEdge ("Font SDF Edge", Float) = 0.5
        _FontSdfSoftness ("Font SDF Softness", Float) = 0.06

        _FontUV_0 ("Font UV 0", Vector) = (0,0,1,1)
        _FontUV_1 ("Font UV 1", Vector) = (0,0,1,1)
        _FontUV_2 ("Font UV 2", Vector) = (0,0,1,1)
        _FontUV_3 ("Font UV 3", Vector) = (0,0,1,1)
        _FontUV_4 ("Font UV 4", Vector) = (0,0,1,1)
        _FontUV_5 ("Font UV 5", Vector) = (0,0,1,1)
        _FontUV_6 ("Font UV 6", Vector) = (0,0,1,1)
        _FontUV_7 ("Font UV 7", Vector) = (0,0,1,1)
        _FontUV_8 ("Font UV 8", Vector) = (0,0,1,1)
        _FontUV_9 ("Font UV 9", Vector) = (0,0,1,1)
        _FontUV_Minus ("Font UV Minus", Vector) = (0,0,1,1)
        _FontUV_Plus ("Font UV Plus", Vector) = (0,0,1,1)

        _FontAspect_0 ("Font Aspect 0", Float) = 0.60
        _FontAspect_1 ("Font Aspect 1", Float) = 0.60
        _FontAspect_2 ("Font Aspect 2", Float) = 0.60
        _FontAspect_3 ("Font Aspect 3", Float) = 0.60
        _FontAspect_4 ("Font Aspect 4", Float) = 0.60
        _FontAspect_5 ("Font Aspect 5", Float) = 0.60
        _FontAspect_6 ("Font Aspect 6", Float) = 0.60
        _FontAspect_7 ("Font Aspect 7", Float) = 0.60
        _FontAspect_8 ("Font Aspect 8", Float) = 0.60
        _FontAspect_9 ("Font Aspect 9", Float) = 0.60
        _FontAspect_Minus ("Font Aspect Minus", Float) = 0.20
        _FontAspect_Plus ("Font Aspect Plus", Float) = 0.50

        _FontUV_A ("Font UV A", Vector) = (0,0,1,1)
        _FontUV_B ("Font UV B", Vector) = (0,0,1,1)
        _FontUV_C ("Font UV C", Vector) = (0,0,1,1)
        _FontUV_D ("Font UV D", Vector) = (0,0,1,1)
        _FontUV_E ("Font UV E", Vector) = (0,0,1,1)
        _FontUV_F ("Font UV F", Vector) = (0,0,1,1)
        _FontUV_G ("Font UV G", Vector) = (0,0,1,1)
        _FontUV_H ("Font UV H", Vector) = (0,0,1,1)
        _FontUV_I ("Font UV I", Vector) = (0,0,1,1)
        _FontUV_J ("Font UV J", Vector) = (0,0,1,1)
        _FontUV_K ("Font UV K", Vector) = (0,0,1,1)
        _FontUV_L ("Font UV L", Vector) = (0,0,1,1)
        _FontUV_M ("Font UV M", Vector) = (0,0,1,1)
        _FontUV_N ("Font UV N", Vector) = (0,0,1,1)
        _FontUV_O ("Font UV O", Vector) = (0,0,1,1)
        _FontUV_P ("Font UV P", Vector) = (0,0,1,1)
        _FontUV_Q ("Font UV Q", Vector) = (0,0,1,1)
        _FontUV_R ("Font UV R", Vector) = (0,0,1,1)
        _FontUV_S ("Font UV S", Vector) = (0,0,1,1)
        _FontUV_T ("Font UV T", Vector) = (0,0,1,1)
        _FontUV_U ("Font UV U", Vector) = (0,0,1,1)
        _FontUV_V ("Font UV V", Vector) = (0,0,1,1)
        _FontUV_W ("Font UV W", Vector) = (0,0,1,1)
        _FontUV_X ("Font UV X", Vector) = (0,0,1,1)
        _FontUV_Y ("Font UV Y", Vector) = (0,0,1,1)
        _FontUV_Z ("Font UV Z", Vector) = (0,0,1,1)

        _FontAspect_A ("Font Aspect A", Float) = 0.60
        _FontAspect_B ("Font Aspect B", Float) = 0.60
        _FontAspect_C ("Font Aspect C", Float) = 0.60
        _FontAspect_D ("Font Aspect D", Float) = 0.60
        _FontAspect_E ("Font Aspect E", Float) = 0.60
        _FontAspect_F ("Font Aspect F", Float) = 0.60
        _FontAspect_G ("Font Aspect G", Float) = 0.60
        _FontAspect_H ("Font Aspect H", Float) = 0.60
        _FontAspect_I ("Font Aspect I", Float) = 0.60
        _FontAspect_J ("Font Aspect J", Float) = 0.60
        _FontAspect_K ("Font Aspect K", Float) = 0.60
        _FontAspect_L ("Font Aspect L", Float) = 0.60
        _FontAspect_M ("Font Aspect M", Float) = 0.60
        _FontAspect_N ("Font Aspect N", Float) = 0.60
        _FontAspect_O ("Font Aspect O", Float) = 0.60
        _FontAspect_P ("Font Aspect P", Float) = 0.60
        _FontAspect_Q ("Font Aspect Q", Float) = 0.60
        _FontAspect_R ("Font Aspect R", Float) = 0.60
        _FontAspect_S ("Font Aspect S", Float) = 0.60
        _FontAspect_T ("Font Aspect T", Float) = 0.60
        _FontAspect_U ("Font Aspect U", Float) = 0.60
        _FontAspect_V ("Font Aspect V", Float) = 0.60
        _FontAspect_W ("Font Aspect W", Float) = 0.60
        _FontAspect_X ("Font Aspect X", Float) = 0.60
        _FontAspect_Y ("Font Aspect Y", Float) = 0.60
        _FontAspect_Z ("Font Aspect Z", Float) = 0.60

        _FontUV_Dot ("Font UV Dot", Vector) = (0,0,1,1)
        _FontAspect_Dot ("Font Aspect Dot", Float) = 0.20

        _FontSignWidthScale ("Font Sign Width Scale", Float) = 0.55
        _FontSignHeightScale ("Font Sign Height Scale", Float) = 0.35
        _FontDotWidthScale ("Font Dot Width Scale", Float) = 0.45
        _FontDotHeightScale ("Font Dot Height Scale", Float) = 0.28
        _FontDotBaselineOffset ("Font Dot Baseline Offset", Float) = -0.22

        _DockValid ("Dock Valid", Float) = 0
        _DockRangeMeters ("Dock Range Meters", Float) = 0.0
        _DockClosureMps ("Dock Closure Mps", Float) = 0.0
        _DockTextHeight ("Dock Text Height", Float) = 0.014

        _DockRelVelValid ("Dock RelVel Valid", Float) = 0
        _DockRelVelProg_HUD ("Dock RelVel Prograde HUD", Vector) = (0,0,0,0)
        _DockRelVelRetro_HUD ("Dock RelVel Retro HUD", Vector) = (0,0,0,0)
        _DockRelSpeedMps ("Dock Rel Speed Mps", Float) = 0.0
        _DockRelMarkerRadius ("Dock Rel Marker Radius", Float) = 0.022
        _DockRelMarkerThickness ("Dock Rel Marker Thickness", Float) = 0.003
        
        // Dock alignment mode / reveal selection
        _DockReticleMode ("Dock Reticle Mode (0=Std,1=AlignT)", Float) = 0
        _DockAlignStencilMode ("Dock Align Stencil Mode (0=None,1=SeatA,2=SeatB)", Float) = 0

        // Tunable docking T reticle
        _DockTReticleScale ("Dock T Reticle Scale", Float) = 1.0
        _DockTReticleThickness ("Dock T Reticle Thickness", Float) = 0.004
        _DockTReticleHalfWidth ("Dock T Reticle Half Width", Float) = 0.055
        _DockTReticleStemLen ("Dock T Reticle Stem Length", Float) = 0.050
        _DockTReticleEndcapWidth ("Dock T Endcap Width", Float) = 0.010
        _DockTReticleEndcapHeight ("Dock T Endcap Height", Float) = 0.010
        _DockTReticleYOffset ("Dock T Reticle Y Offset", Float) = 0.000

        _DockPortIndex ("Dock Port Index", Float) = -1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGBA



            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragGlass
            #include "UnityCG.cginc"

            float4 _GlassTint;
            float _GlassAlpha;
            float _GlassFresnel;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldN   : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldN = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 fragGlass(v2f i) : SV_Target
            {
                float3 N = normalize(i.worldN);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);

                float ndotv = saturate(dot(N, V));
                float fres = pow(1.0 - ndotv, max(_GlassFresnel, 0.001));

                float alpha = _GlassAlpha * (0.65 + 0.35 * fres);
                return fixed4(_GlassTint.rgb, alpha);
            }
            ENDCG
        }

        // --------------------------------------------------
        // PASS: gate stencil reveal mask
        // Writes gate stencil class for 3D gate objects.
        // --------------------------------------------------
        Pass
        {
            ColorMask 0
            ZWrite Off

            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
                WriteMask 1                
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragStencilGate
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 fragStencilGate(v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }

        // --------------------------------------------------
        // PASS: dock alignment stencil A
        // Active only when _DockAlignStencilMode ~= 1
        // --------------------------------------------------
        Pass
        {
            ColorMask 0
            ZWrite Off

            Stencil
            {
                Ref 2
                Comp Always
                Pass Replace
                WriteMask 2                
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragStencilAlignA
            #include "UnityCG.cginc"

            float _DockAlignStencilMode;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 fragStencilAlignA(v2f i) : SV_Target
            {
                clip(0.5 - abs(_DockAlignStencilMode - 1.0));
                return 0;
            }
            ENDCG
        }

        // --------------------------------------------------
        // PASS: dock alignment stencil B
        // Active only when _DockAlignStencilMode ~= 2
        // --------------------------------------------------
        Pass
        {
            ColorMask 0
            ZWrite Off
            
            Stencil
            {
                Ref 4
                Comp Always
                Pass Replace
                WriteMask 4
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragStencilAlignB
            #include "UnityCG.cginc"

            float _DockAlignStencilMode;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 fragStencilAlignB(v2f i) : SV_Target
            {
                clip(0.5 - abs(_DockAlignStencilMode - 2.0));
                return 0;
            }
            ENDCG
        }


        Pass
        {
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragHud
            #include "UnityCG.cginc"

            #define PI 3.14159265359

            float _HudHalfFovX;
            float _HudHalfFovY;
            float _HudIntensity;
            float4 _HudColor;
            float _LineWidth;
            float _Softness;

            sampler2D _FontAtlas;
            float _FontSdfEdge;
            float _FontSdfSoftness;

            float4 _FontUV_0;
            float4 _FontUV_1;
            float4 _FontUV_2;
            float4 _FontUV_3;
            float4 _FontUV_4;
            float4 _FontUV_5;
            float4 _FontUV_6;
            float4 _FontUV_7;
            float4 _FontUV_8;
            float4 _FontUV_9;
            float4 _FontUV_Minus;
            float4 _FontUV_Plus;
            float4 _FontUV_Dot;

            float _FontAspect_0;
            float _FontAspect_1;
            float _FontAspect_2;
            float _FontAspect_3;
            float _FontAspect_4;
            float _FontAspect_5;
            float _FontAspect_6;
            float _FontAspect_7;
            float _FontAspect_8;
            float _FontAspect_9;
            float _FontAspect_Minus;
            float _FontAspect_Plus;
            float _FontAspect_Dot;

            float4 _FontUV_A, _FontUV_B, _FontUV_C, _FontUV_D, _FontUV_E, _FontUV_F, _FontUV_G;
            float4 _FontUV_H, _FontUV_I, _FontUV_J, _FontUV_K, _FontUV_L, _FontUV_M, _FontUV_N;
            float4 _FontUV_O, _FontUV_P, _FontUV_Q, _FontUV_R, _FontUV_S, _FontUV_T, _FontUV_U;
            float4 _FontUV_V, _FontUV_W, _FontUV_X, _FontUV_Y, _FontUV_Z;

            float _FontAspect_A, _FontAspect_B, _FontAspect_C, _FontAspect_D, _FontAspect_E, _FontAspect_F, _FontAspect_G;
            float _FontAspect_H, _FontAspect_I, _FontAspect_J, _FontAspect_K, _FontAspect_L, _FontAspect_M, _FontAspect_N;
            float _FontAspect_O, _FontAspect_P, _FontAspect_Q, _FontAspect_R, _FontAspect_S, _FontAspect_T, _FontAspect_U;
            float _FontAspect_V, _FontAspect_W, _FontAspect_X, _FontAspect_Y, _FontAspect_Z;

            float _FontSignWidthScale;
            float _FontSignHeightScale;
            float _FontDotWidthScale;
            float _FontDotHeightScale;
            float _FontDotBaselineOffset;

            float _DockValid;
            float _DockRangeMeters;
            float _DockClosureMps;
            float _DockTextHeight;

            float _DockRelVelValid;
            float4 _DockRelVelProg_HUD;
            float4 _DockRelVelRetro_HUD;
            float _DockRelSpeedMps;
            float _DockRelMarkerRadius;
            float _DockRelMarkerThickness;

            float _DockReticleMode;
            float _DockAlignStencilMode;

            float _DockTReticleScale;
            float _DockTReticleThickness;
            float _DockTReticleHalfWidth;
            float _DockTReticleStemLen;
            float _DockTReticleEndcapWidth;
            float _DockTReticleEndcapHeight;
            float _DockTReticleYOffset;

            float _DockPortIndex;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float aa_band(float dist, float halfWidth, float softness)
            {
                float fw = fwidth(dist);
                float w = halfWidth + softness * fw;
                return 1.0 - smoothstep(halfWidth, w, dist);
            }

            float2 DirToHudUV(float3 dir_B, float halfFovX, float halfFovY)
            {
                dir_B = normalize(dir_B);

                float az = atan2(dir_B.x, dir_B.z);
                float el = atan2(dir_B.y, dir_B.z);

                float2 uvh;
                uvh.x = az / max(halfFovX, 1e-6);
                uvh.y = el / max(halfFovY, 1e-6);
                return uvh;
            }

            float SampleFontSdf(float2 atlasUV)
            {
                return tex2D(_FontAtlas, atlasUV).a;
            }

            float DrawGlyphRect(float2 uvh, float2 center, float glyphWidth, float glyphHeight, float4 uvRect)
            {
                float2 local = uvh - center;

                float gx = (local.x / glyphWidth) + 0.5;
                float gy = (local.y / glyphHeight) + 0.5;

                if (gx < 0.0 || gx > 1.0 || gy < 0.0 || gy > 1.0)
                    return 0.0;

                float2 atlasUV;
                atlasUV.x = lerp(uvRect.x, uvRect.z, gx);
                atlasUV.y = lerp(uvRect.y, uvRect.w, gy);

                float sdf = SampleFontSdf(atlasUV);

                return smoothstep(_FontSdfEdge - _FontSdfSoftness,
                                  _FontSdfEdge + _FontSdfSoftness,
                                  sdf);
            }

            float4 GetDigitUV(int digit)
            {
                if (digit == 0) return _FontUV_0;
                if (digit == 1) return _FontUV_1;
                if (digit == 2) return _FontUV_2;
                if (digit == 3) return _FontUV_3;
                if (digit == 4) return _FontUV_4;
                if (digit == 5) return _FontUV_5;
                if (digit == 6) return _FontUV_6;
                if (digit == 7) return _FontUV_7;
                if (digit == 8) return _FontUV_8;
                if (digit == 9) return _FontUV_9;
                return _FontUV_0;
            }

            float GetDigitAspect(int digit)
            {
                if (digit == 0) return _FontAspect_0;
                if (digit == 1) return _FontAspect_1;
                if (digit == 2) return _FontAspect_2;
                if (digit == 3) return _FontAspect_3;
                if (digit == 4) return _FontAspect_4;
                if (digit == 5) return _FontAspect_5;
                if (digit == 6) return _FontAspect_6;
                if (digit == 7) return _FontAspect_7;
                if (digit == 8) return _FontAspect_8;
                if (digit == 9) return _FontAspect_9;
                return _FontAspect_0;
            }

            float4 GetUpperUV(int idx)
            {
                if (idx == 0) return _FontUV_A;
                if (idx == 1) return _FontUV_B;
                if (idx == 2) return _FontUV_C;
                if (idx == 3) return _FontUV_D;
                if (idx == 4) return _FontUV_E;
                if (idx == 5) return _FontUV_F;
                if (idx == 6) return _FontUV_G;
                if (idx == 7) return _FontUV_H;
                if (idx == 8) return _FontUV_I;
                if (idx == 9) return _FontUV_J;
                if (idx == 10) return _FontUV_K;
                if (idx == 11) return _FontUV_L;
                if (idx == 12) return _FontUV_M;
                if (idx == 13) return _FontUV_N;
                if (idx == 14) return _FontUV_O;
                if (idx == 15) return _FontUV_P;
                if (idx == 16) return _FontUV_Q;
                if (idx == 17) return _FontUV_R;
                if (idx == 18) return _FontUV_S;
                if (idx == 19) return _FontUV_T;
                if (idx == 20) return _FontUV_U;
                if (idx == 21) return _FontUV_V;
                if (idx == 22) return _FontUV_W;
                if (idx == 23) return _FontUV_X;
                if (idx == 24) return _FontUV_Y;
                if (idx == 25) return _FontUV_Z;
                return _FontUV_A;
            }

            float GetUpperAspect(int idx)
            {
                if (idx == 0) return _FontAspect_A;
                if (idx == 1) return _FontAspect_B;
                if (idx == 2) return _FontAspect_C;
                if (idx == 3) return _FontAspect_D;
                if (idx == 4) return _FontAspect_E;
                if (idx == 5) return _FontAspect_F;
                if (idx == 6) return _FontAspect_G;
                if (idx == 7) return _FontAspect_H;
                if (idx == 8) return _FontAspect_I;
                if (idx == 9) return _FontAspect_J;
                if (idx == 10) return _FontAspect_K;
                if (idx == 11) return _FontAspect_L;
                if (idx == 12) return _FontAspect_M;
                if (idx == 13) return _FontAspect_N;
                if (idx == 14) return _FontAspect_O;
                if (idx == 15) return _FontAspect_P;
                if (idx == 16) return _FontAspect_Q;
                if (idx == 17) return _FontAspect_R;
                if (idx == 18) return _FontAspect_S;
                if (idx == 19) return _FontAspect_T;
                if (idx == 20) return _FontAspect_U;
                if (idx == 21) return _FontAspect_V;
                if (idx == 22) return _FontAspect_W;
                if (idx == 23) return _FontAspect_X;
                if (idx == 24) return _FontAspect_Y;
                if (idx == 25) return _FontAspect_Z;
                return _FontAspect_A;
            }



            float aa_ring(float2 p, float radius, float thickness, float softness)
            {
                float d = abs(length(p) - radius);
                return aa_band(d, thickness, softness);
            }

            float aa_xshape(float2 p, float halfWidth, float thickness, float softness)
            {
                float2 d1u = normalize(float2(1.0, 1.0));
                float2 d2u = normalize(float2(1.0, -1.0));

                float d1 = abs(dot(p, float2(-d1u.y, d1u.x)));
                float d2 = abs(dot(p, float2(-d2u.y, d2u.x)));

                float l1 = abs(dot(p, d1u));
                float l2 = abs(dot(p, d2u));

                float a1 = aa_band(d1, thickness, softness) * (1.0 - smoothstep(halfWidth, halfWidth + 0.01, l1));
                float a2 = aa_band(d2, thickness, softness) * (1.0 - smoothstep(halfWidth, halfWidth + 0.01, l2));
                return max(a1, a2);
            }

            float aa_segment_local(float2 p, float2 axisAlong, float2 axisAcross, float halfLen, float halfThick, float softness)
            {
                float along = dot(p, axisAlong);
                float across = dot(p, axisAcross);

                float core = aa_band(abs(across), halfThick, softness);
                float lenGate = step(abs(along), halfLen);
                return core * lenGate;
            }

            float MarkerTargetRelVel(float2 uvh, float2 center, float radius, float thickness, float softness, bool retro)
            {
                float2 dp = uvh - center;

                float ring = aa_ring(dp, radius, thickness, softness);

                float crossH = aa_segment_local(dp, float2(1.0, 0.0), float2(0.0, 1.0), radius * 0.65, thickness * 0.85, softness);
                float crossV = aa_segment_local(dp, float2(0.0, 1.0), float2(1.0, 0.0), radius * 0.65, thickness * 0.85, softness);

                float a = max(ring, max(crossH, crossV));

                if (retro)
                {
                    float x = aa_xshape(dp, radius * 0.70, thickness * 0.75, softness);
                    a = max(a, x);
                }

                return a;
            }



            float aa_box_filled(float2 p, float2 halfExt, float softness)
            {
                float2 d = abs(p) - halfExt;
                float outside = length(max(d, 0.0));
                float inside = min(max(d.x, d.y), 0.0);
                float sd = outside + inside;

                float fw = fwidth(sd);
                return 1.0 - smoothstep(0.0, softness * fw, sd);
            }

            float DrawStandardBoresight(float2 uvh, float lineWidth, float softness)
            {
                float barThickness = lineWidth;
                float barLength = 0.05;
                float centerGap = 0.02;

                float hLine = aa_band(abs(uvh.y), barThickness, softness);
                float leftBar  = step(centerGap, -uvh.x) * step(-uvh.x, barLength);
                float rightBar = step(centerGap,  uvh.x) * step( uvh.x, barLength);
                float bars = hLine * (leftBar + rightBar);

                float2 p = uvh;
                float2 dL = normalize(float2(-1.0,  1.0));
                float2 dR = normalize(float2( 1.0,  1.0));

                float lDist = abs(dot(p, float2(-dL.y, dL.x)));
                float rDist = abs(dot(p, float2(-dR.y, dR.x)));

                float lLine = aa_band(lDist, lineWidth, softness);
                float rLine = aa_band(rDist, lineWidth, softness);

                float below = step(p.y, 0.0);
                float chevronLen = 0.03;
                float lLen = step(abs(dot(p, dL)), chevronLen);
                float rLen = step(abs(dot(p, dR)), chevronLen);

                float chevron = max(lLine * lLen, rLine * rLen) * below;

                return bars + chevron;
            }

            float DrawDockTReticle(float2 uvh)
            {
                float s = max(_DockTReticleScale, 1e-4);
                float halfWidth = _DockTReticleHalfWidth * s;
                float stemLen = _DockTReticleStemLen * s;
                float thick = _DockTReticleThickness;
                float endcapW = _DockTReticleEndcapWidth * s;
                float endcapH = _DockTReticleEndcapHeight * s;
                float yOff = _DockTReticleYOffset * s;

                float2 p = uvh - float2(0.0, yOff);

                // top horizontal bar
                float topBar = aa_segment_local(
                    p,
                    float2(1.0, 0.0),
                    float2(0.0, 1.0),
                    halfWidth,
                    thick,
                    _Softness
                );

                // center vertical stem, extending downward from top bar
                float2 stemCenter = float2(0.0, -0.5 * stemLen);
                float stem = aa_segment_local(
                    p - stemCenter,
                    float2(0.0, 1.0),
                    float2(1.0, 0.0),
                    0.5 * stemLen,
                    thick,
                    _Softness
                );

                // thick end caps on left/right ends of the top bar
                float leftCap = aa_box_filled(
                    p - float2(-halfWidth, 0.0),
                    float2(0.5 * endcapW, 0.5 * endcapH),
                    _Softness
                );

                float rightCap = aa_box_filled(
                    p - float2(halfWidth, 0.0),
                    float2(0.5 * endcapW, 0.5 * endcapH),
                    _Softness
                );

                return max(max(topBar, stem), max(leftCap, rightCap));
            }

            float DrawUpperGlyph(float2 uvh, float2 center, float glyphHeight, int upperIndex)
            {
                float glyphWidth = glyphHeight * GetUpperAspect(upperIndex);
                return DrawGlyphRect(uvh, center, glyphWidth, glyphHeight, GetUpperUV(upperIndex));
            }

            float DrawUpperLabel(float2 uvh, float2 center, float glyphHeight, int count, int c0, int c1, int c2, int c3, int c4)
            {
                float w0 = (count > 0 && c0 >= 0) ? glyphHeight * GetUpperAspect(c0) : 0.0;
                float w1 = (count > 1 && c1 >= 0) ? glyphHeight * GetUpperAspect(c1) : 0.0;
                float w2 = (count > 2 && c2 >= 0) ? glyphHeight * GetUpperAspect(c2) : 0.0;
                float w3 = (count > 3 && c3 >= 0) ? glyphHeight * GetUpperAspect(c3) : 0.0;
                float w4 = (count > 4 && c4 >= 0) ? glyphHeight * GetUpperAspect(c4) : 0.0;

                float totalWidth = w0 + w1 + w2 + w3 + w4;
                float cursor = -0.5 * totalWidth;
                float m = 0.0;

                if (count > 0 && c0 >= 0)
                {
                    float2 pos = center + float2(cursor + 0.5 * w0, 0.0);
                    m = max(m, DrawUpperGlyph(uvh, pos, glyphHeight, c0));
                    cursor += w0;
                }
                if (count > 1 && c1 >= 0)
                {
                    float2 pos = center + float2(cursor + 0.5 * w1, 0.0);
                    m = max(m, DrawUpperGlyph(uvh, pos, glyphHeight, c1));
                    cursor += w1;
                }
                if (count > 2 && c2 >= 0)
                {
                    float2 pos = center + float2(cursor + 0.5 * w2, 0.0);
                    m = max(m, DrawUpperGlyph(uvh, pos, glyphHeight, c2));
                    cursor += w2;
                }
                if (count > 3 && c3 >= 0)
                {
                    float2 pos = center + float2(cursor + 0.5 * w3, 0.0);
                    m = max(m, DrawUpperGlyph(uvh, pos, glyphHeight, c3));
                    cursor += w3;
                }
                if (count > 4 && c4 >= 0)
                {
                    float2 pos = center + float2(cursor + 0.5 * w4, 0.0);
                    m = max(m, DrawUpperGlyph(uvh, pos, glyphHeight, c4));
                }

                return m;
            }

            float GetDotAspect() { return _FontAspect_Dot; }
            float4 GetDotUV() { return _FontUV_Dot; }

            float DrawUnsignedFixed2Generic(float2 uvh, float2 center, float glyphHeight, float value)
            {
                float vClamped = max(0.0, value);
                int scaled = (int)floor(vClamped * 100.0 + 0.5);

                int frac = scaled % 100;
                int whole = scaled / 100;

                int d0 = whole % 10; whole /= 10;
                int d1 = whole % 10; whole /= 10;
                int d2 = whole % 10; whole /= 10;
                int d3 = whole % 10; whole /= 10;
                int d4 = whole % 10; whole /= 10;
                int d5 = whole % 10;

                bool show5 = ((scaled / 100) >= 100000);
                bool show4 = show5 || ((scaled / 100) >= 10000);
                bool show3 = show4 || ((scaled / 100) >= 1000);
                bool show2 = show3 || ((scaled / 100) >= 100);
                bool show1 = show2 || ((scaled / 100) >= 10);

                int f1 = frac / 10;
                int f0 = frac % 10;

                float refWidth = glyphHeight * GetDigitAspect(8);
                float dotWidth = glyphHeight * GetDotAspect() * _FontDotWidthScale;
                float dotHeight = glyphHeight * _FontDotHeightScale;
                float dotOffset = glyphHeight * _FontDotBaselineOffset;
                float adv = refWidth * 1.05;

                int wholeCount = 1;
                if (show1) wholeCount = 2;
                if (show2) wholeCount = 3;
                if (show3) wholeCount = 4;
                if (show4) wholeCount = 5;
                if (show5) wholeCount = 6;

                float totalSlots = (float)wholeCount + 1.0 + 2.0;
                float start = -0.5 * (totalSlots - 1.0) * adv;

                float a = 0.0;
                int slot = 0;

                #define DRAW_SLOT_DIGIT_SIMPLE(digitVal, showFlag) \
                    if (showFlag) \
                    { \
                        float2 c = center + float2(start + adv * slot, 0.0); \
                        a += DrawGlyphRect(uvh, c, glyphHeight * GetDigitAspect(digitVal), glyphHeight, GetDigitUV(digitVal)); \
                        slot++; \
                    }

                DRAW_SLOT_DIGIT_SIMPLE(d5, show5)
                DRAW_SLOT_DIGIT_SIMPLE(d4, show4)
                DRAW_SLOT_DIGIT_SIMPLE(d3, show3)
                DRAW_SLOT_DIGIT_SIMPLE(d2, show2)
                DRAW_SLOT_DIGIT_SIMPLE(d1, show1)

                {
                    float2 c = center + float2(start + adv * slot, 0.0);
                    a += DrawGlyphRect(uvh, c, glyphHeight * GetDigitAspect(d0), glyphHeight, GetDigitUV(d0));
                    slot++;
                }

                {
                    float2 c = center + float2(start + adv * slot, dotOffset);
                    a += DrawGlyphRect(uvh, c, dotWidth, dotHeight, GetDotUV());
                    slot++;
                }

                {
                    float2 c = center + float2(start + adv * slot, 0.0);
                    a += DrawGlyphRect(uvh, c, glyphHeight * GetDigitAspect(f1), glyphHeight, GetDigitUV(f1));
                    slot++;
                }

                {
                    float2 c = center + float2(start + adv * slot, 0.0);
                    a += DrawGlyphRect(uvh, c, glyphHeight * GetDigitAspect(f0), glyphHeight, GetDigitUV(f0));
                }

                #undef DRAW_SLOT_DIGIT_SIMPLE
                return a;
            }
            float DrawSignedFixed2Generic(float2 uvh, float2 center, float glyphHeight, float value)
            {
                float vAbs = abs(value);
                int scaled = (int)floor(vAbs * 100.0 + 0.5);

                int frac = scaled % 100;
                int whole = scaled / 100;

                int d0 = whole % 10; whole /= 10;
                int d1 = whole % 10; whole /= 10;
                int d2 = whole % 10; whole /= 10;
                int d3 = whole % 10; whole /= 10;
                int d4 = whole % 10; whole /= 10;
                int d5 = whole % 10;

                bool show5 = ((scaled / 100) >= 100000);
                bool show4 = show5 || ((scaled / 100) >= 10000);
                bool show3 = show4 || ((scaled / 100) >= 1000);
                bool show2 = show3 || ((scaled / 100) >= 100);
                bool show1 = show2 || ((scaled / 100) >= 10);

                int f1 = frac / 10;
                int f0 = frac % 10;

                float refWidth = glyphHeight * GetDigitAspect(8);
                float dotWidth = glyphHeight * GetDotAspect() * _FontDotWidthScale;
                float dotHeight = glyphHeight * _FontDotHeightScale;
                float dotOffset = glyphHeight * _FontDotBaselineOffset;

                float signWidth = glyphHeight * _FontSignWidthScale;
                float signHeight = glyphHeight * _FontSignHeightScale;

                float adv = refWidth * 1.05;

                int wholeCount = 1;
                if (show1) wholeCount = 2;
                if (show2) wholeCount = 3;
                if (show3) wholeCount = 4;
                if (show4) wholeCount = 5;
                if (show5) wholeCount = 6;

                float totalSlots = 1.0 + (float)wholeCount + 1.0 + 2.0; // sign + whole + dot + frac2
                float start = -0.5 * (totalSlots - 1.0) * adv;

                float a = 0.0;
                int slot = 0;

                // sign
                {
                    float2 c = center + float2(start + adv * slot, 0.0);
                    a += DrawGlyphRect(uvh, c, signWidth, signHeight, (value < 0.0) ? _FontUV_Minus : _FontUV_Plus);
                    slot++;
                }

                #define DRAW_SLOT_DIGIT_SIGNED(digitVal, showFlag) \
                    if (showFlag) \
                    { \
                        float2 c = center + float2(start + adv * slot, 0.0); \
                        a += DrawGlyphRect(uvh, c, glyphHeight * GetDigitAspect(digitVal), glyphHeight, GetDigitUV(digitVal)); \
                        slot++; \
                    }

                DRAW_SLOT_DIGIT_SIGNED(d5, show5)
                DRAW_SLOT_DIGIT_SIGNED(d4, show4)
                DRAW_SLOT_DIGIT_SIGNED(d3, show3)
                DRAW_SLOT_DIGIT_SIGNED(d2, show2)
                DRAW_SLOT_DIGIT_SIGNED(d1, show1)

                {
                    float2 c = center + float2(start + adv * slot, 0.0);
                    a += DrawGlyphRect(uvh, c, glyphHeight * GetDigitAspect(d0), glyphHeight, GetDigitUV(d0));
                    slot++;
                }

                {
                    float2 c = center + float2(start + adv * slot, dotOffset);
                    a += DrawGlyphRect(uvh, c, dotWidth, dotHeight, GetDotUV());
                    slot++;
                }

                {
                    float2 c = center + float2(start + adv * slot, 0.0);
                    a += DrawGlyphRect(uvh, c, glyphHeight * GetDigitAspect(f1), glyphHeight, GetDigitUV(f1));
                    slot++;
                }

                {
                    float2 c = center + float2(start + adv * slot, 0.0);
                    a += DrawGlyphRect(uvh, c, glyphHeight * GetDigitAspect(f0), glyphHeight, GetDigitUV(f0));
                }

                #undef DRAW_SLOT_DIGIT_SIGNED
                return a;
            }


            float DrawUnsignedIntGeneric(float2 uvh, float2 center, float glyphHeight, int value)
            {
                value = max(value, 0);

                int d0 = value % 10;
                int d1 = (value / 10) % 10;
                int d2 = (value / 100) % 10;

                bool show2 = (value >= 100);
                bool show1 = (value >= 10);

                float w2 = show2 ? glyphHeight * GetDigitAspect(d2) : 0.0;
                float w1 = show1 ? glyphHeight * GetDigitAspect(d1) : 0.0;
                float w0 = glyphHeight * GetDigitAspect(d0);

                float totalWidth = w2 + w1 + w0;
                float cursor = -0.5 * totalWidth;

                float a = 0.0;

                if (show2)
                {
                    float2 c = center + float2(cursor + 0.5 * w2, 0.0);
                    a += DrawGlyphRect(uvh, c, w2, glyphHeight, GetDigitUV(d2));
                    cursor += w2;
                }

                if (show1)
                {
                    float2 c = center + float2(cursor + 0.5 * w1, 0.0);
                    a += DrawGlyphRect(uvh, c, w1, glyphHeight, GetDigitUV(d1));
                    cursor += w1;
                }

                {
                    float2 c = center + float2(cursor + 0.5 * w0, 0.0);
                    a += DrawGlyphRect(uvh, c, w0, glyphHeight, GetDigitUV(d0));
                }

                return a;
            }


            float DrawDockFixedReadout(float2 uvh)
            {
                float hud = 0.0;

                float2 anchor = float2(-0.50, 0.42);
                float rowH = 0.060;
                float labelValueGap = 0.090;

                float textH = _DockTextHeight;

                // ----------------------------
                // PORT row
                // ----------------------------
                float2 portLabelCenter = anchor;
                float2 portValueCenter = anchor + float2(labelValueGap + 0.010, 0.0);

                hud += DrawUpperLabel(
                    uvh,
                    portLabelCenter,
                    textH,
                    4,
                    15, 14, 17, 19, -1
                );

                if (_DockPortIndex >= 0.0)
                {
                    hud += DrawUnsignedIntGeneric(
                        uvh,
                        portValueCenter,
                        textH,
                        (int)(_DockPortIndex + 0.5)
                    );
                }

                // ----------------------------
                // CLS row
                // ----------------------------
                float2 clsLabelCenter = anchor + float2(0.0, -rowH);
                float2 clsValueCenter = anchor + float2(labelValueGap + 0.020, -rowH);

                hud += DrawUpperLabel(
                    uvh,
                    clsLabelCenter,
                    textH,
                    3,
                    2, 11, 18, -1, -1
                );

                hud += DrawSignedFixed2Generic(
                    uvh,
                    clsValueCenter,
                    textH,
                    _DockClosureMps
                );

                // ----------------------------
                // RNG row
                // ----------------------------
                float2 rngLabelCenter = anchor + float2(0.0, -2.0 * rowH);
                float2 rngValueCenter = anchor + float2(labelValueGap, -2.0 * rowH);

                hud += DrawUpperLabel(
                    uvh,
                    rngLabelCenter,
                    textH,
                    3,
                    17, 13, 6, -1, -1
                );

                hud += DrawUnsignedFixed2Generic(
                    uvh,
                    rngValueCenter,
                    textH,
                    _DockRangeMeters
                );

                return hud;
            }

            fixed4 fragHud(v2f i) : SV_Target
            {
                float3 ray_B = normalize(i.worldPos - _WorldSpaceCameraPos.xyz);
                float2 uvh = DirToHudUV(ray_B, _HudHalfFovX, _HudHalfFovY);

                float edge = max(abs(uvh.x), abs(uvh.y));
                float window = 1.0 - smoothstep(1.0, 1.05, edge);

                float hud = 0.0;

                if (_DockValid > 0.5)
                {
                    hud += DrawDockFixedReadout(uvh);
                }


                if (_DockReticleMode > 0.5)
                    hud += DrawDockTReticle(uvh);
                else
                    hud += DrawStandardBoresight(uvh, _LineWidth, _Softness);

                if (_DockValid > 0.5)
                {
                    // float2 rngLabelCenter = float2(-0.38, 0.28);
                    // float2 rngValueCenter = float2(-0.20, 0.28);

                    // float2 clsLabelCenter = float2(-0.38, 0.22);
                    // float2 clsValueCenter = float2(-0.20, 0.22);

                    // hud += DrawUpperLabel(uvh, rngLabelCenter, _DockTextHeight, 3, 17, 13, 6, -1, -1);
                    // hud += DrawUnsignedFixed2Generic(uvh, rngValueCenter, _DockTextHeight, _DockRangeMeters);

                    // hud += DrawUpperLabel(uvh, clsLabelCenter, _DockTextHeight, 3, 2, 11, 18, -1, -1);
                    // hud += DrawSignedFixed2Generic(uvh, clsValueCenter, _DockTextHeight, _DockClosureMps);

                    if (_DockRelVelValid > 0.5)
                    {
                        float2 relProg = _DockRelVelProg_HUD.xy;
                        float2 relRetro = _DockRelVelRetro_HUD.xy;

                        float relProgInside = step(max(abs(relProg.x), abs(relProg.y)), 1.08);
                        float relRetroInside = step(max(abs(relRetro.x), abs(relRetro.y)), 1.08);

                        float relProgMarker = MarkerTargetRelVel(
                            uvh,
                            relProg,
                            _DockRelMarkerRadius,
                            _DockRelMarkerThickness,
                            _Softness,
                            false
                        ) * relProgInside;

                        float relRetroMarker = MarkerTargetRelVel(
                            uvh,
                            relRetro,
                            _DockRelMarkerRadius,
                            _DockRelMarkerThickness,
                            _Softness,
                            true
                        ) * relRetroInside;

                        hud += 0.95 * relProgMarker;
                        hud += 0.95 * relRetroMarker;

                        float2 relProgTextCenter = relProg + float2(0.0, -(_DockRelMarkerRadius + 0.034));
                        float2 relRetroTextCenter = relRetro + float2(0.0, -(_DockRelMarkerRadius + 0.034));

                        hud += DrawUnsignedFixed2Generic(
                            uvh,
                            relProgTextCenter,
                            _DockTextHeight,
                            _DockRelSpeedMps
                        );

                        hud += DrawUnsignedFixed2Generic(
                            uvh,
                            relRetroTextCenter,
                            _DockTextHeight,
                            _DockRelSpeedMps
                        );

                    }                   


                }

                hud *= window;

                float3 color = _HudColor.rgb * (hud * _HudIntensity);
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}