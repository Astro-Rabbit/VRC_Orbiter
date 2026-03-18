Shader "Orbiter/ADI/ADI_Ball_UnlitCubemap"
{
    Properties
    {
        _GlobeCube ("Globe Cubemap", CUBE) = "" {}
        _Tint ("Tint", Color) = (1,1,1,1)

        // Quaternion sent from script as (x,y,z,w)
        _BallRot ("Ball Rotation", Vector) = (0,0,0,1)

        // Optional quick debug axis flips
        _FlipX ("Flip X", Float) = 0
        _FlipY ("Flip Y", Float) = 0
        _FlipZ ("Flip Z", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _GlobeCube;
            float4 _Tint;
            float4 _BallRot;
            float _FlipX;
            float _FlipY;
            float _FlipZ;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalOS : TEXCOORD0;
            };

            float4 QuatNormalize(float4 q)
            {
                return q / max(length(q), 1e-8);
            }

            float3 RotateByQuat(float3 v, float4 q)
            {
                // q = (x, y, z, w)
                float3 t = 2.0 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normalOS = normalize(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dirOS = normalize(i.normalOS);

                // Optional debug flips
                if (_FlipX > 0.5) dirOS.x = -dirOS.x;
                if (_FlipY > 0.5) dirOS.y = -dirOS.y;
                if (_FlipZ > 0.5) dirOS.z = -dirOS.z;

                float4 q = QuatNormalize(_BallRot);

                // Fake rotating the globe by rotating the lookup direction
                float3 dirRot = normalize(RotateByQuat(dirOS, q));

                fixed4 col = texCUBE(_GlobeCube, dirRot) * _Tint;
                return col;
            }
            ENDCG
        }
    }
}