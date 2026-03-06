Shader "HUD/CollimatedOrbitV1"
{
    Properties
    {
        _HudMode ("HUD Mode", Float) = 2
        _HudHalfFovX ("HUD Half FOV X (rad)", Float) = 0.25
        _HudHalfFovY ("HUD Half FOV Y (rad)", Float) = 0.18

        _HudIntensity ("HUD Intensity", Float) = 1.0
        _HudColor ("HUD Color", Color) = (0.45, 1.0, 0.55, 1.0)

        _LineWidth ("Line Width (HUD units)", Float) = 0.010
        _Softness ("Edge Softness", Float) = 1.5

        _MarkerRadius ("Marker Radius", Float) = 0.035
        _MarkerThickness ("Marker Thickness", Float) = 0.008

        _TapeThickness ("Orbit Tape Thickness", Float) = 0.020

        _GlassTint ("Glass Tint", Color) = (0.15, 0.35, 0.18, 1.0)
        _GlassAlpha ("Glass Alpha", Range(0,1)) = 0.08
        _GlassFresnel ("Glass Fresnel", Float) = 1.5

        _ProgradeDir_B ("Prograde Dir (Body)", Vector) = (0,0,1,0)
        _RadialOutDir_B ("Radial Out Dir (Body)", Vector) = (1,0,0,0)
        _NormalDir_B ("Normal Dir (Body)", Vector) = (0,1,0,0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off

        // --------------------------------------------------
        // PASS 1: subtle glass tint
        // --------------------------------------------------
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

        // --------------------------------------------------
        // PASS 2: additive HUD symbology
        // --------------------------------------------------
        Pass
        {
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragHud
            #include "UnityCG.cginc"

            float _HudMode;
            float _HudHalfFovX;
            float _HudHalfFovY;
            float _HudIntensity;
            float4 _HudColor;

            float _LineWidth;
            float _Softness;
            float _MarkerRadius;
            float _MarkerThickness;
            float _TapeThickness;

            float4 _ProgradeDir_B;
            float4 _RadialOutDir_B;
            float4 _NormalDir_B;

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

            float aa_disc(float2 p, float radius, float softness)
            {
                float d = length(p);
                float fw = fwidth(d);
                return 1.0 - smoothstep(radius, radius + softness * fw, d);
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
                float2 p1 = float2(0.70710678, 0.70710678);
                float2 p2 = float2(0.70710678, -0.70710678);

                float d1 = abs(dot(p, float2(-p1.y, p1.x)));
                float d2 = abs(dot(p, float2(-p2.y, p2.x)));

                float len1 = abs(dot(p, p1));
                float len2 = abs(dot(p, p2));

                float a1 = aa_band(d1, thickness, softness) * (1.0 - smoothstep(halfWidth, halfWidth + 0.01, len1));
                float a2 = aa_band(d2, thickness, softness) * (1.0 - smoothstep(halfWidth, halfWidth + 0.01, len2));
                return max(a1, a2);
            }

            float aa_diamond(float2 p, float radius, float thickness, float softness)
            {
                float d = abs(p.x) + abs(p.y);
                return aa_band(abs(d - radius), thickness, softness);
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

            fixed4 fragHud(v2f i) : SV_Target
            {
                // Current local cockpit convention: world == body
                float3 ray_B = normalize(i.worldPos - _WorldSpaceCameraPos.xyz);

                float2 uvh = DirToHudUV(ray_B, _HudHalfFovX, _HudHalfFovY);

                float edge = max(abs(uvh.x), abs(uvh.y));
                float window = 1.0 - smoothstep(1.0, 1.05, edge);

                // Base boresight cross
                float aH = aa_band(abs(uvh.y), _LineWidth, _Softness);
                float aV = aa_band(abs(uvh.x), _LineWidth, _Softness);

                float crossLen = 0.10;
                float clipH = 1.0 - smoothstep(crossLen, crossLen + 0.01, abs(uvh.x));
                float clipV = 1.0 - smoothstep(crossLen, crossLen + 0.01, abs(uvh.y));
                float cross = aH * clipH + aV * clipV;

                float hud = cross;

                // --------------------------------------------------
                // ORBIT MODE
                // --------------------------------------------------
                if (_HudMode > 1.5 && _HudMode < 2.5)
                {
                    float3 pro_B = normalize(_ProgradeDir_B.xyz);
                    float3 rad_B = normalize(_RadialOutDir_B.xyz);
                    float3 nor_B = normalize(_NormalDir_B.xyz);

                    // Orbit plane tape: directions in the plane satisfy dot(ray, normal)=0
                    float planeErr = dot(ray_B, nor_B);
                    float tape = aa_band(abs(planeErr), _TapeThickness, _Softness);

                    // Fade tape a bit near extreme HUD edges
                    tape *= (1.0 - smoothstep(0.92, 1.03, edge));

                    hud += 0.45 * tape;

                    // Markers
                    hud += MarkerOpenCircle(uvh,  pro_B,  _MarkerRadius, _MarkerThickness, _Softness);   // prograde
                    hud += MarkerRetrograde(uvh, -pro_B,  _MarkerRadius, _MarkerThickness, _Softness);   // retrograde

                    hud += 0.90 * MarkerDiamond(uvh,  rad_B,  _MarkerRadius * 0.85, _MarkerThickness, _Softness); // radial out
                    hud += 0.90 * MarkerDiamond(uvh, -rad_B,  _MarkerRadius * 0.85, _MarkerThickness, _Softness); // radial in

                    hud += 0.80 * MarkerBox(uvh,  nor_B,  _MarkerRadius * 0.70, _MarkerThickness, _Softness); // normal
                    hud += 0.80 * MarkerBox(uvh, -nor_B,  _MarkerRadius * 0.70, _MarkerThickness, _Softness); // anti-normal
                }

                hud *= window;

                float3 color = _HudColor.rgb * (hud * _HudIntensity);
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}