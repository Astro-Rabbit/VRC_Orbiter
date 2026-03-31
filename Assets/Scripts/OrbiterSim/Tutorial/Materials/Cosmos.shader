Shader "Unlit/CosmosShader"
{
	Properties
	{
		_Background ("Background", Cube) = "black" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Geometry" "VRCFallback"="Standard"}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				UNITY_FOG_COORDS(1)
				float4 vertex : SV_POSITION;
				float4 pos : TEXCOORD0;

			};

			samplerCUBE _Background;
			uniform sampler2D _AudioTexture;
			uniform float4 _AudioTexture_TexelSize;

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = v.vertex;
				o.vertex = UnityObjectToClipPos(v.vertex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float r = 1.0 / 64.0;
				r *= 1.0 + tex2Dlod(_AudioTexture, float4(0.0, 0.0, 0.0, 0.0)).r;

				float3 dir = -normalize(ObjSpaceViewDir(i.pos));

				float3 offset = i.pos - float3(0.0, 0.15, 0.0);
				offset = offset - dir * dot(dir, offset);

				float b = length(offset);
				float3 offsetDir = offset / b;
				float d = r / b;
				d *= 3.0; // Unrealistic, but the background we're using isn't detailed enough to look good without this
				float angle = d * (d * (0.75 + 0.375 * UNITY_PI) + 2.0);
				float3 newDir = cos(angle) * dir + sin(angle) * offsetDir;

				float4 col = texCUBE(_Background, (mul(unity_ObjectToWorld, float4(newDir, 0.0))).xyz);
				//float4 col = float4(cubemap(newDir), 1.0);
				col *= float4(0.0, 0.75, 1.0, 1.0);
				if (b < r * 2.5) {
					col *= 0.0;
				}

				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
}
