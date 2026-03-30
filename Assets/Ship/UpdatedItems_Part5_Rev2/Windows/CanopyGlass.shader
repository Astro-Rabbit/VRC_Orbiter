Shader "Orbiter/CanopyGlass_Base"
{
    Properties
    {
        [Header(Base Glass)]
        _GlassTint ("Glass Tint", Color) = (0.72, 0.90, 1.00, 1.0)
        _BaseAlpha ("Base Alpha", Range(0.0, 1.0)) = 0.22

        [Header(Fresnel)]
        _FresnelColor ("Fresnel Color", Color) = (0.85, 0.95, 1.0, 1.0)
        _FresnelPower ("Fresnel Power", Range(0.5, 8.0)) = 3.0
        _FresnelStrength ("Fresnel Strength", Range(0.0, 2.0)) = 0.65

        [Header(Reflection)]
        _ReflectionStrength ("Reflection Strength", Range(0.0, 2.0)) = 0.45
        _ReflectionFresnelBoost ("Reflection Fresnel Boost", Range(0.0, 2.0)) = 0.75

        [Header(Imperfections)]
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0.0, 2.0)) = 0.35

        _DetailNormalMap ("Detail Normal Map", 2D) = "bump" {}
        _DetailNormalStrength ("Detail Normal Strength", Range(0.0, 2.0)) = 0.15
        _DetailTiling ("Detail Tiling", Float) = 6.0

        _ImperfectionMask ("Imperfection Mask", 2D) = "white" {}
        _ImperfectionStrength ("Imperfection Strength", Range(0.0, 1.0)) = 0.2

        [Header(Extra)]
        _EdgeDarken ("Edge Darken", Range(0.0, 1.0)) = 0.08
        _AlphaFresnelBoost ("Alpha Fresnel Boost", Range(0.0, 1.0)) = 0.08

        [Header(Vignette Control)]
        _VignetteStrength ("Vignette Strength", Range(0.0, 1.0)) = 0.0
        _VignetteStart ("Vignette Start", Range(0.0, 1.0)) = 0.35
        _VignetteEnd ("Vignette End", Range(0.0, 1.0)) = 0.75
        _VignetteHardness ("Vignette Hardness", Range(0.25, 8.0)) = 2.0

        [Header(Dark State)]
        _DarkFinalColor ("Dark Final Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _DarkFinalAlpha ("Dark Final Alpha", Range(0.0, 1.0)) = 1.0
        _DarkReflectStrength ("Dark Reflect Strength", Range(0.0, 2.0)) = 0.0
        _DarkFresnelStrength ("Dark Fresnel Strength", Range(0.0, 2.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        LOD 200
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _NormalMap;
            float4 _NormalMap_ST;

            sampler2D _DetailNormalMap;
            float4 _DetailNormalMap_ST;

            sampler2D _ImperfectionMask;
            float4 _ImperfectionMask_ST;

            fixed4 _GlassTint;
            half _BaseAlpha;

            fixed4 _FresnelColor;
            half _FresnelPower;
            half _FresnelStrength;

            half _ReflectionStrength;
            half _ReflectionFresnelBoost;

            half _NormalStrength;
            half _DetailNormalStrength;
            half _DetailTiling;

            half _ImperfectionStrength;
            half _EdgeDarken;
            half _AlphaFresnelBoost;

            half _VignetteStrength;
            half _VignetteStart;
            half _VignetteEnd;
            half _VignetteHardness;

            fixed4 _DarkFinalColor;
            half _DarkFinalAlpha;
            half _DarkReflectStrength;
            half _DarkFresnelStrength;

            struct appdata
            {
                float4 vertex   : POSITION;
                float3 normal   : NORMAL;
                float4 tangent  : TANGENT;
                float2 uv       : TEXCOORD0;
                float2 uv1      : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos              : SV_POSITION;
                float2 uv               : TEXCOORD0;
                float2 uvDetail         : TEXCOORD1;
                float3 worldPos         : TEXCOORD2;
                float3 worldNormal      : TEXCOORD3;
                float3 worldTangent     : TEXCOORD4;
                float3 worldBinormal    : TEXCOORD5;
                float2 uvVignette       : TEXCOORD6;
                UNITY_FOG_COORDS(7)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                o.uv = TRANSFORM_TEX(v.uv, _NormalMap);
                o.uvDetail = v.uv * _DetailTiling;

                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                o.worldBinormal = cross(o.worldNormal, o.worldTangent) * v.tangent.w;
                o.uvVignette = v.uv1;

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float3 BlendNormalsRNM(float3 n1, float3 n2)
            {
                n1 = normalize(n1);
                n2 = normalize(n2);
                float3 t = n1 + float3(0,0,1);
                float3 u = n2 * float3(-1,-1,1);
                return normalize((t / t.z) * dot(t,u) - u);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 Nw = normalize(i.worldNormal);
                float3 Tw = normalize(i.worldTangent);
                float3 Bw = normalize(i.worldBinormal);
                float3x3 TBN = float3x3(Tw, Bw, Nw);

                float3 nMain = UnpackNormal(tex2D(_NormalMap, i.uv));
                nMain.xy *= _NormalStrength;

                float2 detailUV = TRANSFORM_TEX(i.uvDetail, _DetailNormalMap);
                float3 nDetail = UnpackNormal(tex2D(_DetailNormalMap, detailUV));
                nDetail.xy *= _DetailNormalStrength;

                float3 nTS = BlendNormalsRNM(nMain, nDetail);
                float3 N = normalize(mul(nTS, TBN));

                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);

                half NdotV = saturate(dot(N, V));
                half fresnel = pow(1.0h - NdotV, _FresnelPower);

                half imperfection = tex2D(_ImperfectionMask, TRANSFORM_TEX(i.uv, _ImperfectionMask)).r;
                half imperf = lerp(1.0h, imperfection, _ImperfectionStrength);

                float3 reflDir = reflect(-V, N);
                half4 env = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflDir);
                float3 envRefl = DecodeHDR(env, unity_SpecCube0_HDR);

                // ----- Normal glass state -----
                float3 reflectionNormal = envRefl * (_ReflectionStrength + fresnel * _ReflectionFresnelBoost);
                reflectionNormal *= lerp(0.9h, 1.1h, imperf);

                float3 fresColNormal = _FresnelColor.rgb * fresnel * _FresnelStrength;

                half edgeDark = fresnel * _EdgeDarken;

                float3 transmissionNormal = _GlassTint.rgb;
                transmissionNormal *= lerp(1.0h, imperfection, _ImperfectionStrength * 0.15h);
                transmissionNormal *= (1.0h - edgeDark);

                float3 normalColor = transmissionNormal + reflectionNormal + fresColNormal;

                half normalAlpha = _BaseAlpha;
                normalAlpha += fresnel * _AlphaFresnelBoost;
                normalAlpha *= lerp(1.0h, imperfection, _ImperfectionStrength * 0.25h);
                normalAlpha = saturate(normalAlpha);

                // ----- Dark replacement state -----
                float3 darkReflect = envRefl * _DarkReflectStrength;
                float3 darkFresnel = _FresnelColor.rgb * fresnel * _DarkFresnelStrength;
                float3 darkColor = _DarkFinalColor.rgb + darkReflect + darkFresnel;
                half darkAlpha = _DarkFinalAlpha;

                // ----- UV1 pane-edge vignette -----
                // 0 at pane center, 1 at pane edge if UV1 spans 0..1 per pane
                float2 centered = abs(i.uvVignette - 0.5) * 2.0;
                half edgeCoord = max(centered.x, centered.y);

                half mask01 = saturate((edgeCoord - _VignetteStart) / max(1e-5, (_VignetteEnd - _VignetteStart)));
                half tintLerp = saturate(pow(mask01, _VignetteHardness) * _VignetteStrength);

                // Hard cut to replacement state when fully tinted
                if (tintLerp >= 0.999h)
                {
                    fixed4 cHard = fixed4(darkColor, saturate(darkAlpha));
                    UNITY_APPLY_FOG(i.fogCoord, cHard);
                    return cHard;
                }

                float3 finalCol = lerp(normalColor, darkColor, tintLerp);
                half alpha = lerp(normalAlpha, darkAlpha, tintLerp);

                fixed4 c = fixed4(finalCol, saturate(alpha));
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }

    FallBack Off
}