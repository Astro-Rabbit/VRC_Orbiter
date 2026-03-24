Shader "Custom/TransparentEmission"
{
	Properties
	{
		_Color("Main Color", Color) = (1,1,1,1)
		_MainTex("Albedo (RGB)", 2D) = "white" {}
		[HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
	}
		SubShader
		{
			Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
			LOD 200
			ZTest Always
			ZWrite Off
			CGPROGRAM
			// alpha:fade tells Unity to use Alpha Blending
			#pragma surface surf Standard fullforwardshadows alpha:fade

			sampler2D _MainTex;

			struct Input
			{
				float2 uv_MainTex;
			};

			fixed4 _Color;
			fixed4 _EmissionColor;

			void surf(Input IN, inout SurfaceOutputStandard o)
			{
				fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
				o.Albedo = c.rgb;

				// Apply Emission
				o.Emission = _EmissionColor.rgb;

				// Apply Alpha from the Color property
				o.Alpha = c.a;
			}
			ENDCG
		}
			FallBack "Transparent/VertexLit"
}