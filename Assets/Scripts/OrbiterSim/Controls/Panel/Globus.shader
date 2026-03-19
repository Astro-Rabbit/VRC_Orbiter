Shader "Orbiter/Globus/StaticLightmappedEpaper"
{
    Properties
    {
        _GlobeTex ("Current Globe Texture", 2D) = "white" {}
        _PrevGlobeTex ("Previous Globe Texture", 2D) = "white" {}

        _Tint ("Tint", Color) = (1,1,1,1)
        _GlobeRot ("Globe Rotation Quaternion", Vector) = (0,0,0,1)

        _EPaperSaturation ("E-Paper Saturation", Range(0,1)) = 0.35
        _EPaperContrast ("E-Paper Contrast", Range(0.5,2.0)) = 1.08
        _PaperBrightness ("Paper Brightness", Range(0.5,1.5)) = 1.0
        _PaperColor ("Paper Color", Color) = (0.88,0.89,0.86,1)
        _InkColor ("Ink Color", Color) = (0.20,0.20,0.20,1)

        _RefreshPhase ("Refresh Phase", Range(0,1)) = 1
        _RefreshNoiseScale ("Refresh Noise Scale", Range(8,512)) = 96
        _RefreshFlashStrength ("Refresh Flash Strength", Range(0,1)) = 0.32
        _RefreshGhostStrength ("Refresh Ghost Strength", Range(0,1)) = 0.18

        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.08
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1.0

        _FlipU ("Flip U", Float) = 0
        _FlipV ("Flip V", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        #include "UnityCG.cginc"

        sampler2D _GlobeTex;
        sampler2D _PrevGlobeTex;

        fixed4 _Tint;
        float4 _GlobeRot;

        half _EPaperSaturation;
        half _EPaperContrast;
        half _PaperBrightness;
        fixed4 _PaperColor;
        fixed4 _InkColor;

        half _RefreshPhase;
        half _RefreshNoiseScale;
        half _RefreshFlashStrength;
        half _RefreshGhostStrength;

        half _Metallic;
        half _Glossiness;
        half _OcclusionStrength;

        half _FlipU;
        half _FlipV;

        #define ORBITER_PI 3.14159265359

        struct Input
        {
            float3 objPos;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objPos = v.vertex.xyz;
        }

        float3 RotateByQuaternion(float3 v, float4 qRaw)
        {
            float lenQ = length(qRaw);
            float4 q = qRaw;
            if (lenQ > 1e-8)
                q /= lenQ;
            else
                q = float4(0,0,0,1);

            return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
        }

        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 34.345);
            return frac(p.x * p.y);
        }

        float2 DirToLatLongUV(float3 d)
        {
            d = normalize(d);

            float u = atan2(d.x, d.z) / (2.0 * ORBITER_PI) + 0.5;
            float v = asin(clamp(d.y, -1.0, 1.0)) / ORBITER_PI + 0.5;

            if (_FlipU > 0.5) u = 1.0 - u;
            if (_FlipV > 0.5) v = 1.0 - v;

            return float2(u, v);
        }

        float3 ApplyEPaperLook(float3 c)
        {
            c *= _Tint.rgb;

            float lum = dot(c, float3(0.299, 0.587, 0.114));
            float3 sat = lerp(lum.xxx, c, _EPaperSaturation);

            sat = (sat - 0.5) * _EPaperContrast + 0.5;
            sat *= _PaperBrightness;

            float inkMix = saturate(dot(sat, float3(0.299, 0.587, 0.114)));
            float3 paperized = lerp(_InkColor.rgb, _PaperColor.rgb, inkMix);

            return lerp(paperized, sat, 0.45);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 dirOS = normalize(IN.objPos);
            float3 dirTex = RotateByQuaternion(dirOS, _GlobeRot);

            float2 uv = DirToLatLongUV(dirTex);

            fixed4 texCurr = tex2D(_GlobeTex, uv);
            fixed4 texPrev = tex2D(_PrevGlobeTex, uv);

            float3 curr = ApplyEPaperLook(texCurr.rgb);
            float3 prev = ApplyEPaperLook(texPrev.rgb);

            float phase = saturate(_RefreshPhase);

            float2 noiseCell = floor(uv * _RefreshNoiseScale);
            float n = Hash21(noiseCell + phase * 37.17);

            float rewriteMask = step(n, phase);
            float3 baseCol = lerp(prev, curr, rewriteMask);

            baseCol = lerp(baseCol, prev, _RefreshGhostStrength * (1.0 - phase) * 0.35);

            float flashEnv = saturate(1.0 - abs(phase * 2.0 - 1.0) * 2.0);
            float flashAmt = flashEnv * _RefreshFlashStrength;

            float flashPattern = step(0.5, Hash21(noiseCell + 91.7));
            float3 flashCol = lerp(_PaperColor.rgb, _InkColor.rgb, flashPattern);

            baseCol = lerp(baseCol, flashCol, flashAmt);

            o.Albedo = saturate(baseCol);
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Occlusion = _OcclusionStrength;
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Standard"
}