Shader "Orbiter/ADI/ADI_Ball_LightmapAndRealtime"
{
    Properties
    {
        _GlobeCube ("Globe Cubemap", CUBE) = "" {}
        _Tint ("Tint", Color) = (1,1,1,1)

        _BallRot ("Ball Rotation", Vector) = (0,0,0,1)

        _FlipX ("Flip X", Float) = 0
        _FlipY ("Flip Y", Float) = 0
        _FlipZ ("Flip Z", Float) = 0

        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.08
        _DiffuseStrength ("Realtime Diffuse Strength", Range(0,2)) = 1.0
        _ADISpecColor ("Specular Color", Color) = (1,1,1,1)
        _Shininess ("Shininess", Range(1,128)) = 32
        _SpecStrength ("Specular Strength", Range(0,2)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        // ======================================================
        // BASE PASS
        // lightmap + ambient + main forward light
        // ======================================================
        Pass
        {
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBase
            #pragma multi_compile LIGHTMAP_OFF LIGHTMAP_ON
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            samplerCUBE _GlobeCube;
            float4 _Tint;
            float4 _BallRot;
            float _FlipX;
            float _FlipY;
            float _FlipZ;

            float _AmbientStrength;
            float _DiffuseStrength;
            float4 _ADISpecColor;
            float _Shininess;
            float _SpecStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv1 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float2 lightmapUV : TEXCOORD3;
                LIGHTING_COORDS(4,5)
            };

            float4 QuatNormalize(float4 q)
            {
                return q / max(length(q), 1e-8);
            }

            float3 RotateByQuat(float3 v, float4 q)
            {
                float3 t = 2.0 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            fixed3 SampleBallAlbedo(float3 normalOS)
            {
                float3 dirOS = normalize(normalOS);

                if (_FlipX > 0.5) dirOS.x = -dirOS.x;
                if (_FlipY > 0.5) dirOS.y = -dirOS.y;
                if (_FlipZ > 0.5) dirOS.z = -dirOS.z;

                float4 q = QuatNormalize(_BallRot);
                float3 dirRot = normalize(RotateByQuat(dirOS, q));

                // Cubemap orientation correction
                dirRot.z = -dirRot.z;
                dirRot.x = -dirRot.x;
                return texCUBE(_GlobeCube, dirRot).rgb * _Tint.rgb;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normalOS = normalize(v.normal);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.lightmapUV = v.uv1 * unity_LightmapST.xy + unity_LightmapST.zw;
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 fragBase(v2f i) : SV_Target
            {
                fixed3 albedo = SampleBallAlbedo(i.normalOS);

                fixed3 bakedLighting = 0.0;
                #ifdef LIGHTMAP_ON
                    fixed4 lmTex = UNITY_SAMPLE_TEX2D(unity_Lightmap, i.lightmapUV);
                    bakedLighting = DecodeLightmap(lmTex);
                #endif

                float3 N = normalize(i.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);

                float3 L;
                if (_WorldSpaceLightPos0.w == 0.0)
                    L = normalize(_WorldSpaceLightPos0.xyz);
                else
                    L = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);

                float atten = LIGHT_ATTENUATION(i);

                float3 ambient = _AmbientStrength.xxx;

                float NdotL = max(dot(N, L), 0.0);
                float3 realtimeDiffuse = NdotL * _LightColor0.rgb * _DiffuseStrength * atten;

                float3 H = normalize(L + V);
                float NdotH = max(dot(N, H), 0.0);
                float spec = pow(NdotH, _Shininess) * _SpecStrength * atten;
                float3 specular = spec * _ADISpecColor.rgb * _LightColor0.rgb;

                float3 lighting = bakedLighting + ambient + realtimeDiffuse;
                float3 finalColor = albedo * lighting + specular;

                return fixed4(finalColor, _Tint.a);
            }
            ENDCG
        }

        // ======================================================
        // ADD PASS
        // extra point/spot lights
        // ======================================================
        Pass
        {
            Tags { "LightMode"="ForwardAdd" }
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAdd
            #pragma multi_compile_fwdadd_fullshadows

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            samplerCUBE _GlobeCube;
            float4 _Tint;
            float4 _BallRot;
            float _FlipX;
            float _FlipY;
            float _FlipZ;

            float _DiffuseStrength;
            float4 _ADISpecColor;
            float _Shininess;
            float _SpecStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                LIGHTING_COORDS(3,4)
            };

            float4 QuatNormalize(float4 q)
            {
                return q / max(length(q), 1e-8);
            }

            float3 RotateByQuat(float3 v, float4 q)
            {
                float3 t = 2.0 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            fixed3 SampleBallAlbedo(float3 normalOS)
            {
                float3 dirOS = normalize(normalOS);

                if (_FlipX > 0.5) dirOS.x = -dirOS.x;
                if (_FlipY > 0.5) dirOS.y = -dirOS.y;
                if (_FlipZ > 0.5) dirOS.z = -dirOS.z;

                float4 q = QuatNormalize(_BallRot);
                float3 dirRot = normalize(RotateByQuat(dirOS, q));

                // Cubemap orientation correction
                dirRot.z = -dirRot.z;
                dirRot.x = -dirRot.x;

                return texCUBE(_GlobeCube, dirRot).rgb * _Tint.rgb;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normalOS = normalize(v.normal);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 fragAdd(v2f i) : SV_Target
            {
                fixed3 albedo = SampleBallAlbedo(i.normalOS);

                float3 N = normalize(i.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 L = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);

                float atten = LIGHT_ATTENUATION(i);

                float NdotL = max(dot(N, L), 0.0);
                float3 diffuse = albedo * NdotL * _LightColor0.rgb * _DiffuseStrength * atten;

                float3 H = normalize(L + V);
                float NdotH = max(dot(N, H), 0.0);
                float spec = pow(NdotH, _Shininess) * _SpecStrength * atten;
                float3 specular = spec * _ADISpecColor.rgb * _LightColor0.rgb;

                return fixed4(diffuse + specular, 0);
            }
            ENDCG
        }
    }
}