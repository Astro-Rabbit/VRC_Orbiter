Shader "Custom/DSKY_Segment_Interface"
{
    Properties
    {
        // Each Vector4 holds the bitmasks for 4 digits (X, Y, Z, W)
        // We can support up to 8 digits with just two properties
        _DigitData1 ("Digits 1-4 Masks", Vector) = (0,0,0,0)
        _DigitData2 ("Digits 5-8 Masks", Vector) = (0,0,0,0)
        _DigitData3 ("Digits 9-12 Masks", Vector) = (0,0,0,0)
        _DigitData4 ("Digits 13-16 Masks", Vector) = (0,0,0,0)
        _DigitData5 ("Digits 17-20 Masks", Vector) = (0,0,0,0)
        _DigitData6 ("Digits 21-24 Masks", Vector) = (0,0,0,0)
        
        _ActiveColor ("Active Color", Color) = (0, 1, 0.8, 1)
        _InactiveColor ("Inactive Color", Color) = (0.02, 0.02, 0.02, 1)
        _Emission ("Brightness", Float) = 5.0
        _Digits ("Digits", Float) = 5 
        _Division ("Division", Float) = 1
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

            float4 _DigitData1;
            float4 _DigitData2;
            float4 _DigitData3;
            float4 _DigitData4;
            float4 _DigitData5;
            float4 _DigitData6;
            float4 _ActiveColor;
            float4 _InactiveColor;
            float _Emission;
            int _Digits;
            float _Division;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                // 1. Determine which Digit slot we are looking at (0 to 7)
                //Changing this to temp 0 to 4
                int digitIndex = floor(((i.uv.x * _Division) * _Digits)); 
                
                // 2. Get the mask for this specific digit from the Vectors
                int mask = 0;
                if(digitIndex == 0) mask = (int)_DigitData1.x;
                else if(digitIndex == 1) mask = (int)_DigitData1.y;
                else if(digitIndex == 2) mask = (int)_DigitData1.z;
                else if(digitIndex == 3) mask = (int)_DigitData1.w;
                else if(digitIndex == 4) mask = (int)_DigitData2.x;
                else if(digitIndex == 5) mask = (int)_DigitData2.y;
                else if(digitIndex == 6) mask = (int)_DigitData2.z;
                else if(digitIndex == 7) mask = (int)_DigitData2.w;
                else if(digitIndex == 8) mask = (int)_DigitData3.x;
                else if(digitIndex == 9) mask = (int)_DigitData3.y;
                else if(digitIndex == 10) mask = (int)_DigitData3.z;
                else if(digitIndex == 11) mask = (int)_DigitData3.w;
                else if(digitIndex == 12) mask = (int)_DigitData4.x;
                else if(digitIndex == 13) mask = (int)_DigitData4.y;
                else if(digitIndex == 14) mask = (int)_DigitData4.z;
                else if(digitIndex == 15) mask = (int)_DigitData4.w;
                else if(digitIndex == 16) mask = (int)_DigitData5.x;
                else if(digitIndex == 17) mask = (int)_DigitData5.y;
                else if(digitIndex == 18) mask = (int)_DigitData5.z;
                else if(digitIndex == 19) mask = (int)_DigitData5.w;
                else if(digitIndex == 20) mask = (int)_DigitData6.x;
                else if(digitIndex == 21) mask = (int)_DigitData6.y;
                else if(digitIndex == 22) mask = (int)_DigitData6.z;
                else if(digitIndex == 23) mask = (int)_DigitData6.w;
                

                // 3. Determine which segment index we are (0-6) based on V height
                int segmentIndex = floor(((i.uv.y*_Division) * 7));

                // 4. Extract the bit: (mask >> segmentIndex) & 1
                // We use 2 to the power of segmentIndex
                bool isOn = (mask >> segmentIndex) & 1 && i.uv.y <0.5 &&i.uv.x <0.5;

                return isOn ? _ActiveColor * _Emission : _InactiveColor;
            }
            ENDCG
        }
    }
}