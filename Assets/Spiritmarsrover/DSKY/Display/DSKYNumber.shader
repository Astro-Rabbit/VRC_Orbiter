Shader "Custom/DSKY_7Segment"
{
    Properties
    {
        _DisplayValue ("Display Value", Int) = 12345
        _DigitCount ("Total Digits in Row", Int) = 5
        _ActiveColor ("Active Color", Color) = (0, 1, 0.8, 1) // EL Cyan-Green
        _InactiveColor ("Inactive Color", Color) = (0.01, 0.05, 0.05, 1)
        _EmissionBoost ("Emission Boost", Float) = 5.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            int _DisplayValue;
            int _DigitCount;
            float4 _ActiveColor;
            float4 _InactiveColor;
            float _EmissionBoost;

            // 7-segment bitmask (A, B, C, D, E, F, G)
            // Segments map to bits 0 through 6
            static const int segmentMasks[10] = {
                126, // 0: 1111110
                48,  // 1: 0110000
                109, // 2: 1101101
                121, // 3: 1111001
                51,  // 4: 0110011
                91,  // 5: 1011011
                95,  // 6: 1011111
                112, // 7: 1110000
                127, // 8: 1111111
                123  // 9: 1111011
            };

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // 1. Identify which digit this is (0 to _DigitCount-1)
                // We assume U 0.0 is the LEFTmost digit
                int digitIndex = floor(i.uv.x * _DigitCount);
                
                // 2. Extract that specific digit from the full number
                // Example: Value 123, Digit Index 0 (hundreds) = 1
                int powerOfTen = pow(10, (_DigitCount - 1) - digitIndex);
                int currentDigitValue = (_DisplayValue / powerOfTen) % 10;
                
                // 3. Identify which segment this is (0 to 6) based on V height
                int segmentIndex = floor(i.uv.y * 7);

                // 4. Check the bitmask
                int mask = segmentMasks[currentDigitValue];
                
                // Shift the mask and check the specific bit for this segment
                bool isOn = (mask >> (6 - segmentIndex)) & 1;

                fixed4 col = isOn ? _ActiveColor * _EmissionBoost : _InactiveColor;
                return col;
            }
            ENDCG
        }
    }
}