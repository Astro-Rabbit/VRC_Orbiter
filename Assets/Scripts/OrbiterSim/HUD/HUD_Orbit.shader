Shader "HUD/CollimatedOrbitV2"
{
    Properties
    {
        _HudMode ("HUD Mode", Float) = 2

        _HudHalfFovX ("HUD Half FOV X (rad)", Float) = 0.25
        _HudHalfFovY ("HUD Half FOV Y (rad)", Float) = 0.18

        _HudIntensity ("HUD Intensity", Float) = 1.0
        _HudColor ("HUD Color", Color) = (0.45, 1.0, 0.55, 1.0)

        _LineWidth ("Center Cross Thickness", Float) = 0.004
        _Softness ("Edge Softness", Float) = 1.5

        _MarkerRadius ("Marker Radius", Float) = 0.030
        _MarkerThickness ("Marker Thickness", Float) = 0.005

        _TapeCoreThickness ("Tape Core Thickness", Float) = 0.003
        _TickThickness ("Tick Thickness", Float) = 0.003
        _TickMinorLength ("Minor Tick Length", Float) = 0.015
        _TickMajorLength ("Major Tick Length", Float) = 0.028
        _YawLabelTracking ("Yaw Label Tracking", Float) = 1.55
        _YawLabelHeight ("Yaw Label Height", Float) = 0.010

        _TickMinorStepDeg ("Minor Tick Step (deg)", Float) = 5.0
        _TickMajorStepDeg ("Major Tick Step (deg)", Float) = 10.0

        _YawTapeLimitDeg ("Yaw Tape Half Range (deg)", Float) = 40
        _PitchLadderLimitDeg ("Pitch Ladder Half Range (deg)", Float) = 30

        _GlassTint ("Glass Tint", Color) = (0.15, 0.35, 0.18, 1.0)
        _GlassAlpha ("Glass Alpha", Range(0,1)) = 0.08
        _GlassFresnel ("Glass Fresnel", Float) = 1.5

        _ProgradeDir_B ("Prograde Dir (Body)", Vector) = (0,0,1,0)
        _RadialOutDir_B ("Radial Out Dir (Body)", Vector) = (1,0,0,0)
        _NormalDir_B ("Normal Dir (Body)", Vector) = (0,1,0,0)

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

        // Target / rendezvous overlay
        _TargetValid ("Target Valid", Float) = 0
        _TargetPos_HUD ("Target Pos HUD", Vector) = (0,0,0,0)
        _TargetBoxHalfSize ("Target Box Half Size", Float) = 0.040
        _TargetTextHeight ("Target Text Height", Float) = 0.014
        _TargetRangeMeters ("Target Range Meters", Float) = 0.0

        _TargetRelVelValid ("Target RelVel Valid", Float) = 0
        _TargetRelVelProg_HUD ("Target RelVel Prograde HUD", Vector) = (0,0,0,0)
        _TargetRelVelRetro_HUD ("Target RelVel Retro HUD", Vector) = (0,0,0,0)
        _TargetRelSpeedMps ("Target Rel Speed Mps", Float) = 0.0
        _TargetRelMarkerRadius ("Target Rel Marker Radius", Float) = 0.022
        _TargetRelMarkerThickness ("Target Rel Marker Thickness", Float) = 0.004
        _FontDotWidthScale ("Font Dot Width Scale", Float) = 0.45
        _FontDotHeightScale ("Font Dot Height Scale", Float) = 0.28
        _FontDotBaselineOffset ("Font Dot Baseline Offset", Float) = -0.22

        _TargetNameLen ("Target Name Length", Float) = 0
        _TargetNameC0 ("Target Name Char0", Float) = -1
        _TargetNameC1 ("Target Name Char1", Float) = -1
        _TargetNameC2 ("Target Name Char2", Float) = -1
        _TargetNameC3 ("Target Name Char3", Float) = -1
        _TargetNameC4 ("Target Name Char4", Float) = -1

    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

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

        Pass
        {
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragHud
            #include "UnityCG.cginc"

            #define PI 3.14159265359
            #define DEG2RAD 0.01745329252

            float _HudMode;
            float _HudHalfFovX;
            float _HudHalfFovY;
            float _HudIntensity;
            float4 _HudColor;

            float _LineWidth;
            float _Softness;

            float _MarkerRadius;
            float _MarkerThickness;

            float _TapeCoreThickness;
            float _TickThickness;
            float _TickMinorLength;
            float _TickMajorLength;
            float _TickMinorStepDeg;
            float _TickMajorStepDeg;
            float _YawLabelTracking;
            float _YawLabelHeight;
            float _YawTapeLimitDeg;
            float _PitchLadderLimitDeg;

            float4 _ProgradeDir_B;
            float4 _RadialOutDir_B;
            float4 _NormalDir_B;

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
            float4 _FontUV_Dot;
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
            float _TargetValid;
            float4 _TargetPos_HUD;
            float _TargetBoxHalfSize;
            float _TargetTextHeight;
            float _TargetRangeMeters;

            float _TargetRelVelValid;
            float4 _TargetRelVelProg_HUD;
            float4 _TargetRelVelRetro_HUD;
            float _TargetRelSpeedMps;
            float _TargetRelMarkerRadius;
            float _TargetRelMarkerThickness;

            float _FontDotWidthScale;
            float _FontDotHeightScale;
            float _FontDotBaselineOffset;

            float _TargetNameLen;
            float _TargetNameC0;
            float _TargetNameC1;
            float _TargetNameC2;
            float _TargetNameC3;
            float _TargetNameC4;

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

            float aa_ring(float2 p, float radius, float thickness, float softness)
            {
                float d = abs(length(p) - radius);
                return aa_band(d, thickness, softness);
            }

            float aa_box_outline(float2 p, float2 halfExt, float thickness, float softness)
            {
                float2 d = abs(p) - halfExt;
                float outside = length(max(d, 0.0));
                float inside = min(max(d.x, d.y), 0.0);
                float sd = outside + inside;
                return aa_band(abs(sd), thickness, softness);
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

            float aa_diamond(float2 p, float radius, float thickness, float softness)
            {
                float d = abs(p.x) + abs(p.y);
                return aa_band(abs(d - radius), thickness, softness);
            }

            float aa_segment_local(float2 p, float2 axisAlong, float2 axisAcross, float halfLen, float halfThick, float softness)
            {
                float along = dot(p, axisAlong);
                float across = dot(p, axisAcross);

                float core = aa_band(abs(across), halfThick, softness);
                float lenGate = step(abs(along), halfLen);
                return core * lenGate;
            }

            float WrapAngle(float a)
            {
                return atan2(sin(a), cos(a));
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

            float DrawGlyphRectOriented(
                float2 uvh,
                float2 center,
                float glyphWidth,
                float glyphHeight,
                float2 rightAxis,
                float2 upAxis,
                float4 uvRect)
            {
                float2 local = uvh - center;

                float gx = (dot(local, rightAxis) / glyphWidth) + 0.5;
                float gy = (dot(local, upAxis)    / glyphHeight) + 0.5;

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

            float4 GetSignUV(int signCode)
            {
                if (signCode < 0) return _FontUV_Minus;
                if (signCode > 0) return _FontUV_Plus;
                return float4(0,0,0,0);
            }
            float GetDotAspect()
            {
                return _FontAspect_Dot;
            }

            float4 GetDotUV()
            {
                return _FontUV_Dot;
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

            float2 EnsureUprightRight(float2 rightAxis, float2 upAxis)
            {
                return (upAxis.y < 0.0) ? -rightAxis : rightAxis;
            }

            float2 EnsureUprightUp(float2 rightAxis, float2 upAxis)
            {
                return (upAxis.y < 0.0) ? -upAxis : upAxis;
            }


            int GetTargetNameChar(int i)
            {
                if (i == 0) return (int)_TargetNameC0;
                if (i == 1) return (int)_TargetNameC1;
                if (i == 2) return (int)_TargetNameC2;
                if (i == 3) return (int)_TargetNameC3;
                if (i == 4) return (int)_TargetNameC4;
                return -1;
            }

            float DrawUpperGlyph(
                float2 uvh,
                float2 center,
                float glyphHeight,
                int upperIndex,
                float2 rightAxis,
                float2 upAxis,
                bool oriented)
            {
                float glyphWidth = glyphHeight * GetUpperAspect(upperIndex);
                if (oriented)
                    return DrawGlyphRectOriented(uvh, center, glyphWidth, glyphHeight, rightAxis, upAxis, GetUpperUV(upperIndex));
                else
                    return DrawGlyphRect(uvh, center, glyphWidth, glyphHeight, GetUpperUV(upperIndex));
            }

            float DrawUpperLabel(
                float2 uvh,
                float2 center,
                float glyphHeight,
                int count,
                int c0, int c1, int c2, int c3, int c4,
                float2 rightAxis,
                float2 upAxis,
                bool oriented)
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
                    float2 pos = center + rightAxis * (cursor + 0.5 * w0);
                    m = max(m, DrawUpperGlyph(uvh, pos, glyphHeight, c0, rightAxis, upAxis, oriented));
                    cursor += w0;
                }

                if (count > 1 && c1 >= 0)
                {
                    float2 pos = center + rightAxis * (cursor + 0.5 * w1);
                    m = max(m, DrawUpperGlyph(uvh, pos, glyphHeight, c1, rightAxis, upAxis, oriented));
                    cursor += w1;
                }

                if (count > 2 && c2 >= 0)
                {
                    float2 pos = center + rightAxis * (cursor + 0.5 * w2);
                    m = max(m, DrawUpperGlyph(uvh, pos, glyphHeight, c2, rightAxis, upAxis, oriented));
                    cursor += w2;
                }

                if (count > 3 && c3 >= 0)
                {
                    float2 pos = center + rightAxis * (cursor + 0.5 * w3);
                    m = max(m, DrawUpperGlyph(uvh, pos, glyphHeight, c3, rightAxis, upAxis, oriented));
                    cursor += w3;
                }

                if (count > 4 && c4 >= 0)
                {
                    float2 pos = center + rightAxis * (cursor + 0.5 * w4);
                    m = max(m, DrawUpperGlyph(uvh, pos, glyphHeight, c4, rightAxis, upAxis, oriented));
                }

                return m;
            }

            float DrawInt2NoPlusGeneric(
                float2 uvh,
                float2 center,
                float glyphHeight,
                int value,
                float2 rightAxis,
                float2 upAxis,
                bool oriented)
            {
                int absVal = value;
                bool negative = false;

                if (value < 0)
                {
                    negative = true;
                    absVal = -value;
                }

                if (absVal > 99) absVal = 99;

                int tens = absVal / 10;
                int ones = absVal - tens * 10;

                float tensWidth = glyphHeight * GetDigitAspect(tens);
                float onesWidth = glyphHeight * GetDigitAspect(ones);

                float signWidth  = glyphHeight * _FontSignWidthScale;
                float signHeight = glyphHeight * _FontSignHeightScale;

                float monoAdvance = glyphHeight * 0.70;
                float a = 0.0;

                if (negative)
                {
                    if (oriented)
                    {
                        a += DrawGlyphRectOriented(uvh, center + rightAxis * (-monoAdvance), signWidth, signHeight, rightAxis, upAxis, GetSignUV(-1));
                    }
                    else
                    {
                        a += DrawGlyphRect(uvh, center + float2(-monoAdvance, 0.0), signWidth, signHeight, GetSignUV(-1));
                    }
                }

                if (oriented)
                {
                    a += DrawGlyphRectOriented(uvh, center, tensWidth, glyphHeight, rightAxis, upAxis, GetDigitUV(tens));
                    a += DrawGlyphRectOriented(uvh, center + rightAxis * monoAdvance, onesWidth, glyphHeight, rightAxis, upAxis, GetDigitUV(ones));
                }
                else
                {
                    a += DrawGlyphRect(uvh, center, tensWidth, glyphHeight, GetDigitUV(tens));
                    a += DrawGlyphRect(uvh, center + float2(monoAdvance, 0.0), onesWidth, glyphHeight, GetDigitUV(ones));
                }

                return a;
            }

            float DrawWrapped3DigitGeneric(
                float2 uvh,
                float2 center,
                float glyphHeight,
                int value,
                float2 rightAxis,
                float2 upAxis,
                bool oriented)
            {
                int v = value;
                while (v < 0) v += 360;
                while (v >= 360) v -= 360;

                int hundreds = v / 100;
                int tens = (v / 10) % 10;
                int ones = v % 10;

                float refWidth = glyphHeight * GetDigitAspect(8);

                float hundWidth = glyphHeight * GetDigitAspect(hundreds);
                float tensWidth = glyphHeight * GetDigitAspect(tens);
                float onesWidth = glyphHeight * GetDigitAspect(ones);

                float advance = refWidth * _YawLabelTracking;
                float a = 0.0;

                if (oriented)
                {
                    a += DrawGlyphRectOriented(uvh, center + rightAxis * (-advance), hundWidth, glyphHeight, rightAxis, upAxis, GetDigitUV(hundreds));
                    a += DrawGlyphRectOriented(uvh, center,                         tensWidth, glyphHeight, rightAxis, upAxis, GetDigitUV(tens));
                    a += DrawGlyphRectOriented(uvh, center + rightAxis * advance,  onesWidth, glyphHeight, rightAxis, upAxis, GetDigitUV(ones));
                }
                else
                {
                    a += DrawGlyphRect(uvh, center + float2(-advance, 0.0), hundWidth, glyphHeight, GetDigitUV(hundreds));
                    a += DrawGlyphRect(uvh, center,                         tensWidth, glyphHeight, GetDigitUV(tens));
                    a += DrawGlyphRect(uvh, center + float2(advance, 0.0), onesWidth, glyphHeight, GetDigitUV(ones));
                }

                return a;
            }

            float DrawUnsignedFixed2Generic(
                float2 uvh,
                float2 center,
                float glyphHeight,
                float value,
                float2 rightAxis,
                float2 upAxis,
                bool oriented)
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

                // Visibility based on WHOLE part, not scaled hundredths
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

                #define DRAW_SLOT_DIGIT(digitVal, showFlag) \
                    if (showFlag) \
                    { \
                        float2 c = center + rightAxis * (start + adv * slot); \
                        if (oriented) a += DrawGlyphRectOriented(uvh, c, glyphHeight * GetDigitAspect(digitVal), glyphHeight, rightAxis, upAxis, GetDigitUV(digitVal)); \
                        else          a += DrawGlyphRect(uvh, c, glyphHeight * GetDigitAspect(digitVal), glyphHeight, GetDigitUV(digitVal)); \
                        slot++; \
                    }

                DRAW_SLOT_DIGIT(d5, show5)
                DRAW_SLOT_DIGIT(d4, show4)
                DRAW_SLOT_DIGIT(d3, show3)
                DRAW_SLOT_DIGIT(d2, show2)
                DRAW_SLOT_DIGIT(d1, show1)

                {
                    float2 c = center + rightAxis * (start + adv * slot);
                    if (oriented) a += DrawGlyphRectOriented(uvh, c, glyphHeight * GetDigitAspect(d0), glyphHeight, rightAxis, upAxis, GetDigitUV(d0));
                    else          a += DrawGlyphRect(uvh, c, glyphHeight * GetDigitAspect(d0), glyphHeight, GetDigitUV(d0));
                    slot++;
                }

                {
                    float2 c = center + rightAxis * (start + adv * slot) + upAxis * dotOffset;
                    if (oriented) a += DrawGlyphRectOriented(uvh, c, dotWidth, dotHeight, rightAxis, upAxis, GetDotUV());
                    else          a += DrawGlyphRect(uvh, c, dotWidth, dotHeight, GetDotUV());
                    slot++;
                }

                {
                    float2 c = center + rightAxis * (start + adv * slot);
                    if (oriented) a += DrawGlyphRectOriented(uvh, c, glyphHeight * GetDigitAspect(f1), glyphHeight, rightAxis, upAxis, GetDigitUV(f1));
                    else          a += DrawGlyphRect(uvh, c, glyphHeight * GetDigitAspect(f1), glyphHeight, GetDigitUV(f1));
                    slot++;
                }

                {
                    float2 c = center + rightAxis * (start + adv * slot);
                    if (oriented) a += DrawGlyphRectOriented(uvh, c, glyphHeight * GetDigitAspect(f0), glyphHeight, rightAxis, upAxis, GetDigitUV(f0));
                    else          a += DrawGlyphRect(uvh, c, glyphHeight * GetDigitAspect(f0), glyphHeight, GetDigitUV(f0));
                }

                #undef DRAW_SLOT_DIGIT
                return a;
            }

            float MarkerOpenCircle(float2 uvh, float3 dir_B, float radius, float thickness, float softness)
            {
                float2 m = DirToHudUV(dir_B, _HudHalfFovX, _HudHalfFovY);
                float inside = step(max(abs(m.x), abs(m.y)), 1.05);
                float2 dp = uvh - m;
                return aa_ring(dp, radius, thickness, softness) * inside;
            }

            float MarkerRetrograde(float2 uvh, float3 dir_B, float radius, float thickness, float softness)
            {
                float2 m = DirToHudUV(dir_B, _HudHalfFovX, _HudHalfFovY);
                float inside = step(max(abs(m.x), abs(m.y)), 1.05);
                float2 dp = uvh - m;

                float ring = aa_ring(dp, radius, thickness, softness);
                float x = aa_xshape(dp, radius * 0.75, thickness * 0.8, softness);
                return max(ring, x) * inside;
            }

            float MarkerDiamond(float2 uvh, float3 dir_B, float radius, float thickness, float softness)
            {
                float2 m = DirToHudUV(dir_B, _HudHalfFovX, _HudHalfFovY);
                float inside = step(max(abs(m.x), abs(m.y)), 1.05);
                float2 dp = uvh - m;
                return aa_diamond(dp, radius, thickness, softness) * inside;
            }



            float MarkerBox(float2 uvh, float3 dir_B, float radius, float thickness, float softness)
            {
                float2 m = DirToHudUV(dir_B, _HudHalfFovX, _HudHalfFovY);
                float inside = step(max(abs(m.x), abs(m.y)), 1.05);
                float2 dp = uvh - m;
                return aa_box_outline(dp, float2(radius, radius), thickness, softness) * inside;
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

            fixed4 fragHud(v2f i) : SV_Target
            {
                float3 ray_B = normalize(i.worldPos - _WorldSpaceCameraPos.xyz);

                float2 uvh = DirToHudUV(ray_B, _HudHalfFovX, _HudHalfFovY);

                float edge = max(abs(uvh.x), abs(uvh.y));
                float window = 1.0 - smoothstep(1.0, 1.05, edge);

                float barThickness = _LineWidth;
                float barLength = 0.05;
                float centerGap = 0.02;

                float hLine = aa_band(abs(uvh.y), barThickness, _Softness);

                float leftBar  = step(centerGap, -uvh.x) * step(-uvh.x, barLength);
                float rightBar = step(centerGap,  uvh.x) * step( uvh.x, barLength);

                float bars = hLine * (leftBar + rightBar);

                float2 p = uvh;
                float2 dL = normalize(float2(-1.0,  1.0));
                float2 dR = normalize(float2( 1.0,  1.0));

                float lDist = abs(dot(p, float2(-dL.y, dL.x)));
                float rDist = abs(dot(p, float2(-dR.y, dR.x)));

                float lLine = aa_band(lDist, _LineWidth, _Softness);
                float rLine = aa_band(rDist, _LineWidth, _Softness);

                float below = step(p.y, 0.0);
                float chevronLen = 0.03;

                float lLen = step(abs(dot(p, dL)), chevronLen);
                float rLen = step(abs(dot(p, dR)), chevronLen);

                float chevron = max(lLine * lLen, rLine * rLen) * below;

                float hud = bars + chevron;
                bool orbitMode = (_HudMode > 1.5 && _HudMode < 2.5);
                bool dockMode  = (_HudMode > 2.5 && _HudMode < 3.5);
                if (orbitMode)
                {
                    float3 F = float3(0.0, 0.0, 1.0);
                    float3 P = normalize(_ProgradeDir_B.xyz);
                    float3 R = normalize(_RadialOutDir_B.xyz);
                    float3 N = normalize(_NormalDir_B.xyz);

                    float3 pitchAxis3 = N - dot(N, F) * F;
                    float pitchAxis3Mag = length(pitchAxis3);

                    if (pitchAxis3Mag < 1e-5)
                    {
                        pitchAxis3 = R - dot(R, F) * F;
                        pitchAxis3Mag = length(pitchAxis3);
                    }

                    if (pitchAxis3Mag < 1e-5)
                    {
                        pitchAxis3 = float3(0.0, 1.0, 0.0);
                        pitchAxis3Mag = 1.0;
                    }

                    pitchAxis3 /= pitchAxis3Mag;

                    float3 yawAxis3 = normalize(cross(pitchAxis3, F));

                    float2 pitchAxisUV = DirToHudUV(normalize(F + 0.05 * pitchAxis3), _HudHalfFovX, _HudHalfFovY);
                    float2 yawAxisUV   = DirToHudUV(normalize(F + 0.05 * yawAxis3),   _HudHalfFovX, _HudHalfFovY);

                    float pitchAxisLen = length(pitchAxisUV);
                    float yawAxisLen   = length(yawAxisUV);

                    if (pitchAxisLen < 1e-5) pitchAxisUV = float2(0.0, 1.0);
                    else pitchAxisUV /= pitchAxisLen;

                    if (yawAxisLen < 1e-5) yawAxisUV = float2(1.0, 0.0);
                    else yawAxisUV /= yawAxisLen;

                    float boresightYaw   = atan2(dot(P, yawAxis3), dot(P, F));
                    float boresightPitch = asin(clamp(dot(F, N), -1.0, 1.0));

                    float yawLimitRad   = max(_YawTapeLimitDeg, 0.0) * DEG2RAD;
                    float pitchLimitRad = max(_PitchLadderLimitDeg, 0.0) * DEG2RAD;

                    float yawHalfSpan   = yawLimitRad   / max(_HudHalfFovX, 1e-6);
                    float pitchHalfSpan = pitchLimitRad / max(_HudHalfFovY, 1e-6);

                    float tapeCore = aa_segment_local(uvh,
                                                      yawAxisUV,
                                                      pitchAxisUV,
                                                      yawHalfSpan,
                                                      _TapeCoreThickness,
                                                      _Softness);

                    float minorStep = max(_TickMinorStepDeg, 0.1) * DEG2RAD;
                    float majorStep = max(_TickMajorStepDeg, 0.1) * DEG2RAD;

                    float minorTicks = 0.0;
                    float majorTicks = 0.0;

                    float firstMinor = floor((boresightYaw - yawLimitRad) / minorStep) * minorStep;
                    float firstMajor = floor((boresightYaw - yawLimitRad) / majorStep) * majorStep;

                    [loop]
                    for (int k = 0; k < 64; k++)
                    {
                        float tickYaw = firstMinor + k * minorStep;
                        float deltaMinor = tickYaw - boresightYaw;

                        if (deltaMinor > yawLimitRad + 1e-4)
                            break;

                        if (abs(deltaMinor) <= yawLimitRad + 1e-4)
                        {
                            float tickOffset = -deltaMinor / max(_HudHalfFovX, 1e-6);
                            float2 tickUV = yawAxisUV * tickOffset;

                            float nearestMajor = round(tickYaw / majorStep) * majorStep;
                            float majorPhase = abs(WrapAngle(tickYaw - nearestMajor));
                            float isMajor = step(majorPhase, 0.001);

                            float tick = aa_segment_local(uvh - tickUV,
                                                          pitchAxisUV,
                                                          yawAxisUV,
                                                          _TickMinorLength,
                                                          _TickThickness,
                                                          _Softness);

                            minorTicks += tick * (1.0 - isMajor);
                        }
                    }

                    [loop]
                    for (int j = 0; j < 64; j++)
                    {
                        float tickYaw = firstMajor + j * majorStep;
                        float deltaMajor = tickYaw - boresightYaw;

                        if (deltaMajor > yawLimitRad + 1e-4)
                            break;

                        if (abs(deltaMajor) <= yawLimitRad + 1e-4)
                        {
                            float tickOffset = -deltaMajor / max(_HudHalfFovX, 1e-6);
                            float2 tickUV = yawAxisUV * tickOffset;

                            float tick = aa_segment_local(uvh - tickUV,
                                                          pitchAxisUV,
                                                          yawAxisUV,
                                                          _TickMajorLength,
                                                          _TickThickness,
                                                          _Softness);

                            majorTicks += tick;

                            float headingDegF = -tickYaw * (180.0 / PI);
                            while (headingDegF < 0.0) headingDegF += 360.0;
                            while (headingDegF >= 360.0) headingDegF -= 360.0;

                            int headingDeg = (int)round(headingDegF);
                            if (headingDeg == 360) headingDeg = 0;

                            float2 labelCenter = tickUV + pitchAxisUV * 0.085;

                            float2 labelRight = EnsureUprightRight(yawAxisUV, pitchAxisUV);
                            float2 labelUp    = EnsureUprightUp(yawAxisUV, pitchAxisUV);

                            hud += DrawWrapped3DigitGeneric(
                                uvh,
                                labelCenter,
                                _YawLabelHeight,
                                headingDeg,
                                labelRight,
                                labelUp,
                                true
                            );
                        }
                    }

                    hud += 0.35 * tapeCore + 0.40 * minorTicks + 0.75 * majorTicks;

                    float3 G = F - dot(F, N) * N;
                    float gMag = length(G);

                    if (gMag < 1e-5) G = R;
                    else G /= gMag;

                    if (dot(G, P) < 0.0)
                        G = -G;

                    float planeHalfLen = min(yawHalfSpan, 0.16);
                    float rungHalfLen  = min(yawHalfSpan * 0.75, 0.11);
                    float hookHalfLen  = 0.012;
                    float localEps     = 1.0 * DEG2RAD;

                    [unroll]
                    for (int pIdx = -8; pIdx <= 8; pIdx++)
                    {
                        float rungPitch = pIdx * 10.0 * DEG2RAD;
                        int meridianCount = (pIdx == 0) ? 1 : 2;

                        [unroll]
                        for (int side = 0; side < 2; side++)
                        {
                            if (side >= meridianCount) break;

                            float meridianSign = (side == 0) ? 1.0 : -1.0;

                            float3 rungDir = normalize(meridianSign * cos(rungPitch) * G + sin(rungPitch) * N);

                            float rungPitchActual = asin(clamp(dot(rungDir, N), -1.0, 1.0));
                            float deltaPitch = WrapAngle(rungPitchActual - boresightPitch);

                            if (abs(deltaPitch) > pitchLimitRad + 1e-4)
                                continue;

                            float2 rungUV = DirToHudUV(rungDir, _HudHalfFovX, _HudHalfFovY);

                            float3 rungDirUp = normalize(meridianSign * cos(rungPitch + localEps) * G + sin(rungPitch + localEps) * N);
                            float3 rungDirDn = normalize(meridianSign * cos(rungPitch - localEps) * G + sin(rungPitch - localEps) * N);

                            float2 rungUVUp = DirToHudUV(rungDirUp, _HudHalfFovX, _HudHalfFovY);
                            float2 rungUVDn = DirToHudUV(rungDirDn, _HudHalfFovX, _HudHalfFovY);

                            float2 rungPitchAxisUV = rungUVUp - rungUVDn;
                            float rungPitchAxisLen = length(rungPitchAxisUV);
                            if (rungPitchAxisLen < 1e-5)
                                continue;
                            rungPitchAxisUV /= rungPitchAxisLen;

                            float2 rungYawAxisUV = float2(-rungPitchAxisUV.y, rungPitchAxisUV.x);

                            if (dot(rungYawAxisUV, yawAxisUV) < 0.0)
                                rungYawAxisUV = -rungYawAxisUV;

                            if (pIdx == 0)
                            {
                                float planeLine = aa_segment_local(uvh - rungUV,
                                                                   rungYawAxisUV,
                                                                   rungPitchAxisUV,
                                                                   planeHalfLen,
                                                                   _LineWidth,
                                                                   _Softness);

                                hud += planeLine;
                            }
                            else
                            {
                                float mainLine = aa_segment_local(uvh - rungUV,
                                                                  rungYawAxisUV,
                                                                  rungPitchAxisUV,
                                                                  rungHalfLen,
                                                                  _LineWidth,
                                                                  _Softness);

                                float hookSign = (rungPitch > 0.0) ? -1.0 : 1.0;

                                float2 leftHookCenter  = rungUV - rungYawAxisUV * rungHalfLen + rungPitchAxisUV * (hookSign * hookHalfLen);
                                float2 rightHookCenter = rungUV + rungYawAxisUV * rungHalfLen + rungPitchAxisUV * (hookSign * hookHalfLen);

                                float leftHook = aa_segment_local(uvh - leftHookCenter,
                                                                  rungPitchAxisUV,
                                                                  rungYawAxisUV,
                                                                  hookHalfLen,
                                                                  _LineWidth,
                                                                  _Softness);

                                float rightHook = aa_segment_local(uvh - rightHookCenter,
                                                                   rungPitchAxisUV,
                                                                   rungYawAxisUV,
                                                                   hookHalfLen,
                                                                   _LineWidth,
                                                                   _Softness);

                                hud += mainLine + leftHook + rightHook;

                                int rungLabelDeg = (int)round(rungPitch * (180.0 / PI));

                                float2 leftLabelCenter  = rungUV - rungYawAxisUV * (rungHalfLen + 0.040);
                                float2 rightLabelCenter = rungUV + rungYawAxisUV * (rungHalfLen + 0.040);

                                float2 labelRight = EnsureUprightRight(rungYawAxisUV, rungPitchAxisUV);
                                float2 labelUp    = EnsureUprightUp(rungYawAxisUV, rungPitchAxisUV);

                                hud += DrawInt2NoPlusGeneric(
                                    uvh,
                                    leftLabelCenter,
                                    0.020,
                                    rungLabelDeg,
                                    labelRight,
                                    labelUp,
                                    true
                                );

                                hud += DrawInt2NoPlusGeneric(
                                    uvh,
                                    rightLabelCenter,
                                    0.020,
                                    rungLabelDeg,
                                    labelRight,
                                    labelUp,
                                    true
                                );
                            }
                        }
                    }

                    float capAngle = 5.0 * DEG2RAD;

                    {
                        float plusDelta = (0.5 * PI) - boresightPitch;
                        if (abs(plusDelta) <= pitchLimitRad + capAngle)
                        {
                            float2 uvCap = DirToHudUV(N, _HudHalfFovX, _HudHalfFovY);
                            float3 edgeDir = normalize(cos(0.5 * PI - capAngle) * G + sin(0.5 * PI - capAngle) * N);
                            float2 uvEdge = DirToHudUV(edgeDir, _HudHalfFovX, _HudHalfFovY);

                            float capRadius = length(uvEdge - uvCap);
                            hud += aa_ring(uvh - uvCap, capRadius, _LineWidth, _Softness);
                        }
                    }

                    {
                        float minusDelta = (-0.5 * PI) - boresightPitch;
                        if (abs(minusDelta) <= pitchLimitRad + capAngle)
                        {
                            float2 uvCap = DirToHudUV(-N, _HudHalfFovX, _HudHalfFovY);
                            float3 edgeDir = normalize(cos(-0.5 * PI + capAngle) * G + sin(-0.5 * PI + capAngle) * N);
                            float2 uvEdge = DirToHudUV(edgeDir, _HudHalfFovX, _HudHalfFovY);

                            float capRadius = length(uvEdge - uvCap);
                            hud += aa_ring(uvh - uvCap, capRadius, _LineWidth, _Softness);
                        }
                    }

                    hud += MarkerOpenCircle(uvh,  P,  _MarkerRadius, _MarkerThickness, _Softness);
                    hud += MarkerRetrograde(uvh, -P, _MarkerRadius, _MarkerThickness, _Softness);

                    hud += 0.90 * MarkerDiamond(uvh,  R, _MarkerRadius * 0.85, _MarkerThickness, _Softness);
                    hud += 0.90 * MarkerDiamond(uvh, -R, _MarkerRadius * 0.85, _MarkerThickness, _Softness);

                    hud += 0.80 * MarkerBox(uvh,  N, _MarkerRadius * 0.70, _MarkerThickness, _Softness);
                    hud += 0.80 * MarkerBox(uvh, -N, _MarkerRadius * 0.70, _MarkerThickness, _Softness);
                }


                // --------------------------------
                // Mode-independent selected target overlay
                // --------------------------------
                if (_TargetValid > 0.5 && !dockMode)
                {
                    float2 targetPos = _TargetPos_HUD.xy;
                    float targetInside = step(max(abs(targetPos.x), abs(targetPos.y)), 1.08);

                    float targetBox = aa_box_outline(
                        uvh - targetPos,
                        float2(_TargetBoxHalfSize, _TargetBoxHalfSize),
                        _LineWidth,
                        _Softness
                    ) * targetInside;

                    hud += targetBox;

                    // ----------------------------
                    // Target short name above box
                    // ----------------------------
                    int targetNameLen = (int)_TargetNameLen;
                    if (targetNameLen > 0)
                    {
                        float2 targetNameCenter = targetPos + float2(0.0, _TargetBoxHalfSize + 0.030);

                        hud += DrawUpperLabel(
                            uvh,
                            targetNameCenter,
                            _TargetTextHeight,
                            targetNameLen,
                            GetTargetNameChar(0),
                            GetTargetNameChar(1),
                            GetTargetNameChar(2),
                            GetTargetNameChar(3),
                            GetTargetNameChar(4),
                            float2(1.0, 0.0),
                            float2(0.0, 1.0),
                            false
                        );
                    }

                    // ----------------------------
                    // RNG label + range below box
                    // ----------------------------
                    float2 rngLabelCenter = targetPos + float2(-0.040, -(_TargetBoxHalfSize + 0.040));
                    float2 rngValueCenter = targetPos + float2(0.038, -(_TargetBoxHalfSize + 0.040));

                    hud += DrawUpperLabel(
                        uvh,
                        rngLabelCenter,
                        _TargetTextHeight,
                        3,
                        17, // R
                        13, // N
                        6,  // G
                        -1,
                        -1,
                        float2(1.0, 0.0),
                        float2(0.0, 1.0),
                        false
                    );

                    hud += DrawUnsignedFixed2Generic(
                        uvh,
                        rngValueCenter,
                        _TargetTextHeight,
                        _TargetRangeMeters,
                        float2(1.0, 0.0),
                        float2(0.0, 1.0),
                        false
                    );
                }

                if (_TargetRelVelValid > 0.5 && !dockMode)
                {
                    float2 relProg = _TargetRelVelProg_HUD.xy;
                    float2 relRetro = _TargetRelVelRetro_HUD.xy;

                    float relProgInside = step(max(abs(relProg.x), abs(relProg.y)), 1.08);
                    float relRetroInside = step(max(abs(relRetro.x), abs(relRetro.y)), 1.08);

                    float relProgMarker = MarkerTargetRelVel(
                        uvh,
                        relProg,
                        _TargetRelMarkerRadius,
                        _TargetRelMarkerThickness,
                        _Softness,
                        false
                    ) * relProgInside;

                    float relRetroMarker = MarkerTargetRelVel(
                        uvh,
                        relRetro,
                        _TargetRelMarkerRadius,
                        _TargetRelMarkerThickness,
                        _Softness,
                        true
                    ) * relRetroInside;

                    hud += 0.95 * relProgMarker;
                    hud += 0.95 * relRetroMarker;

                    // RELV label + value only on prograde marker
                    float2 relvLabelCenter = relProg + float2(-0.050, -(_TargetRelMarkerRadius + 0.034));
                    float2 relvValueCenter = relProg + float2(0.040, -(_TargetRelMarkerRadius + 0.034));

                    hud += DrawUpperLabel(
                        uvh,
                        relvLabelCenter,
                        _TargetTextHeight,
                        4,
                        17, // R
                        4,  // E
                        11, // L
                        21, // V
                        -1,
                        float2(1.0, 0.0),
                        float2(0.0, 1.0),
                        false
                    );

                    hud += DrawUnsignedFixed2Generic(
                        uvh,
                        relvValueCenter,
                        _TargetTextHeight,
                        _TargetRelSpeedMps,
                        float2(1.0, 0.0),
                        float2(0.0, 1.0),
                        false
                    );
                }


                hud *= window;

                float3 color = _HudColor.rgb * (hud * _HudIntensity);
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}