Shader "Custom/OverlayPointer"
{
	Properties
	{
		_Color("Main Color", Color) = (1,1,1,1)
		_EmissionColor("Emission Color", Color) = (0,0,0,1)
	}
		SubShader
	{
		Tags { "Queue" = "Overlay+100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

		ZTest Always
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			struct appdata {
				float4 vertex : POSITION;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
			};

			float4 _Color;
			float4 _EmissionColor;

			v2f vert(appdata v) {
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target {
				fixed4 col = _Color;
				col.rgb += _EmissionColor.rgb; // Additive emission
				col.a = _Color.a;              // Use alpha from main color
				return col;
			}
			ENDCG
		}
	}
}