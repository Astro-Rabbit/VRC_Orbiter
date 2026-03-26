Shader "Unlit/MFDGraphicsShader"
{
    Properties 
    {
        _FontAtlas ("Font Atlas", 2D) = "white" {}
        _FontSdfEdge ("Font SDF Edge", Float) = 0.5
        _FontSdfSoftness ("Font SDF Softness", Float) = 0.06

        _ImageTex ("Optional Image", 2D) = "black" {}
        _ImageEnabled ("Image Enabled", Float) = 0
        _ImageRect ("Image Rect UV", Vector) = (0,0,1,1)
        _ImageUvRect ("Image Source UV Rect", Vector) = (0,0,1,1)
        _ImageTint ("Image Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            #define TEXT_ROWS 24
            #define TEXT_COLUMNS 48
            #define MAX_SHAPES 256

            sampler2D _FontAtlas;
            float4 _FontAtlas_TexelSize;
            float _FontSdfEdge;
            float _FontSdfSoftness;

            sampler2D _ImageTex;
            float _ImageEnabled;
            float4 _ImageRect;   // xmin, ymin, xmax, ymax in display UV
            float4 _ImageUvRect; // umin, vmin, umax, vmax in source image UV
            float4 _ImageTint;
            float4 _ImageTex_TexelSize;


            float4 atlasRects[127 - 32 + 1];
            float4 charRects[127 - 32 + 1];

            uniform int charGrid[24 * 48];
            uniform float3 charColors[24 * 48];
            uniform int shapeCount;
            uniform float3 shapeColors[MAX_SHAPES];
            uniform float shapeData1[MAX_SHAPES];
            uniform float4 shapeData2[MAX_SHAPES];

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            float sdfEllipse(float2 p, float2 ab)
            {
                p = abs(p);

                float2 q = ab*(p-ab);
                float w = (q.x<q.y)? 1.570796327 : 0.0;
                for (int i = 0; i < 5; i++) {
                    float2 cs = float2(cos(w),sin(w));
                    float2 u = ab*float2(cs.x,cs.y);
                    float2 v = ab*float2(-cs.y,cs.x);
                    w = w + dot(p-u,v)/(dot(p-u,u)+dot(v,v));
                }

                float d = length(p-ab*float2(cos(w),sin(w)));
                return (dot(p/ab,p/ab)>1.0) ? d : -d;
            }

            float sdParabola(float2 pos, float k )
            {
                pos.x = abs(pos.x);
                float p = (pos.y*k - 0.5)/3.0;
                float q = pos.x*k/4.0;
                float h = q*q - p*p*p;
                float x;
                if(h > 0.0) {
                    float r = pow(q+sqrt(h),1.0/3.0);
                    x = r + p/r;
                } else {
                    float r = sqrt(p);
                    x = 2.0*r*cos(acos(q/(p*r))/3.0);
                }
                float2 d = pos - float2(x,x*x)/k;
                return length(d) * sign(d.x);
            }

            float sdfHyperbola(float2 p, float k)
            {
                p = abs(p);
                p = float2(p.x-p.y,p.x+p.y)/sqrt(2.0);

                float x2 = p.x*p.x/16.0;
                float y2 = p.y*p.y/16.0;
                float r = k*(4.0*k - p.x*p.y)/12.0;
                float q = (x2 - y2)*k*k;
                float h = q*q + r*r*r;
                float u;
                if(h < 0.0) {
                    float m = sqrt(-r);
                    u = m*cos(acos(q/(r*m))/3.0);
                } else {
                    float m = pow(sqrt(h)-q,1.0/3.0);
                    u = (m - r/m)/2.0;
                }
                float w = sqrt(u + x2);
                float b = k*p.y - x2*p.x*2.0;
                float t = p.x/4.0 - w + sqrt(2.0*x2 - u + b/w/4.0);

                float d = length(p-float2(t,k/t));
                return p.x*p.y < k ? d : -d;
            }

            float sdfLine(float2 p, float2 a, float2 b)
            {
                float2 dir = b - a;
                float2 offset = p - a;
                float proj = dot(offset, dir);
                if (proj < 0) {
                    return length(offset);
                } else if (proj > dot(dir, dir)) {
                    return length(p - b);
                } else {
                    return abs(dot(offset, float2(dir.y, -dir.x)) / length(dir));
                }
            }

            float shape(float2 p, int index) 
            {
                float pe = shapeData1[index];
                float4 data = shapeData2[index];

                if (pe <= 0) {
                    return sdfLine(p, data.xy, data.zw);
                } else {
                    float e = data.x;
                    float omega = data.y;
                    float2 offset = data.zw;
                    float s = sin(-omega);
                    float c = cos(-omega);

                    p -= offset;
                    float2 pr = float2(p.x*c - p.y*s, p.y*c + p.x*s);

                    if (1 - e > 0) {
                        float ap = (1 + e) / (1 - e) * pe;
                        float major = (ap + pe) * 0.5;
                        float minor = sqrt(ap * pe);

                        float2 center = float2(0, major - pe);
                        return sdfEllipse(pr - center, float2(minor, major));
                    } else if (e > 1) {
                        return 1;
                    } else {
                        return 1;
                    }
                }
            }

            float drawGlyph(int c, float gx, float gy)
            {
                if (c == 0x5E) {
                    // special case hack to be able to make ^ look like an upside down V for drawing arrows
                    c = 0x56;
                    gy = 1.0 - gy;
                } else if (c == 0xB0) {
                    // degree symbol
                    c = 0xFF;
                }

                float4 atlasRect = atlasRects[c - 32];
                float4 charRect = charRects[c - 32];

                gy -= 0.1;
                gy *= 1.2;

                float cx = (gx - charRect.x) / charRect.z;
                float cy = 1.0 - (charRect.y - gy)/charRect.w;
                if (cx < 0 || cx > 1 || cy < 0 || cy > 1) {
                    return _FontSdfEdge;
                }

                float2 atlasUv;
                atlasUv.x = lerp(atlasRect.x, atlasRect.z, cx);
                atlasUv.y = lerp(atlasRect.y, atlasRect.w, cy);

                float sdf = tex2D(_FontAtlas, atlasUv).a;

                return _FontSdfEdge - sdf;
            }

            float antialias(float sdf)
            {
                float2 grad = float2(ddx(sdf), ddy(sdf));
                float pixelDist = sdf / length(grad);
                return clamp(0.5 - pixelDist, 0.0, 1.0);
            }

            float4 SampleOptionalImage(float2 uv)
            {
                if (_ImageEnabled < 0.5)
                    return float4(0,0,0,0);

                float2 rectMin = _ImageRect.xy;
                float2 rectMax = _ImageRect.zw;

                if (uv.x < rectMin.x || uv.x > rectMax.x || uv.y < rectMin.y || uv.y > rectMax.y)
                    return float4(0,0,0,0);

                float2 localUv = (uv - rectMin) / max(rectMax - rectMin, float2(1e-6, 1e-6));
                float2 imageUv = lerp(_ImageUvRect.xy, _ImageUvRect.zw, localUv);

                // Pull sampling half a texel inward to avoid border bleed
                float2 halfTexel = 0.5 * _ImageTex_TexelSize.xy;
                imageUv = clamp(imageUv, _ImageUvRect.xy + halfTexel, _ImageUvRect.zw - halfTexel);

                float4 img = tex2D(_ImageTex, imageUv) * _ImageTint;
                return img;
            }

            fixed4 frag (v2f input) : SV_Target
            {
                const float lineWidth = .005;

                float2 p = 2*(input.uv - .5);

                // Base background stays black unless image contributes.
                float4 accum = float4(0, 0, 0, 1);

                // Optional generic image layer
                float4 img = SampleOptionalImage(input.uv);
                accum.rgb = lerp(accum.rgb, img.rgb, saturate(img.a));

                // Existing shape path
                for (int i = 0; i < shapeCount; i++) {
                    float alpha = antialias(abs(shape(p, i)) - lineWidth);
                    color *= 1.0 - alpha;
                    color += shapeColors[i] * alpha;
                }

                // Existing text path
                int row = (int)((1.0 - input.uv.y) * TEXT_ROWS);
                int col = (int)(input.uv.x * TEXT_COLUMNS);

                int index = row*TEXT_COLUMNS + col;
                int c = (int)charGrid[index];
                if (c == 0) {
                    c = 0x20;
                }

                float gx = input.uv.x*TEXT_COLUMNS - col;
                float gy = (1.0 - input.uv.y)*TEXT_ROWS - row;

                float glyph = antialias(drawGlyph(c, gx, 1.0 - gy));
                color *= 1.0 - glyph;
                color += charColors[index] * glyph;

                fixed4 res = fixed4(accum.rgb, 1.0);
                UNITY_APPLY_FOG(input.fogCoord, res);
                return res;
            }
            ENDCG
        }
    }
}