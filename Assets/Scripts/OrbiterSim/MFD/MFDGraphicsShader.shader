Shader "Unlit/MFDGraphicsShader"
{
    Properties 
    {
        _FontAtlas ("Font Atlas", 2D) = "white" {}
        _FontSdfEdge ("Font SDF Edge", Float) = 0.5
        _FontSdfSoftness ("Font SDF Softness", Float) = 0.06
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
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            // Must match values in MFD.cs
            #define TEXT_ROWS 24
            #define TEXT_COLUMNS 48
            #define MAX_SHAPES 256

            sampler2D _FontAtlas;
            float4 _FontAtlas_TexelSize;
            float _FontSdfEdge;
            float _FontSdfSoftness;

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

            // Copyright © 2015 Inigo Quilez
            // Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
            float sdfEllipse(float2 p, float2 ab)
            {
                // symmetry
                p = abs( p );

                // find root with Newton solver
                float2 q = ab*(p-ab);
                float w = (q.x<q.y)? 1.570796327 : 0.0;
                for (int i = 0; i < 5; i++) {
                    float2 cs = float2(cos(w),sin(w));
                    float2 u = ab*float2( cs.x,cs.y);
                    float2 v = ab*float2(-cs.y,cs.x);
                    w = w + dot(p-u,v)/(dot(p-u,u)+dot(v,v));
                }
                
                // compute final point and distance
                float d = length(p-ab*float2(cos(w),sin(w)));
                
                // return signed distance
                return (dot(p/ab,p/ab)>1.0) ? d : -d;
            }

            // Copyright © 2019 Inigo Quilez
            // Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
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

            // Copyright © 2023 Inigo Quilez
            // Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions: The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software. THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
            float sdfHyperbola(float2 p, float k) // k in (0,inf)
            {
                // symmetry and rotation
                p = abs(p);
                p = float2(p.x-p.y,p.x+p.y)/sqrt(2.0);

                // distance to y(x)=k/x by finding t in such that t⁴ - xt³ + kyt - k² = 0
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

                // distance from t
                float d = length(p-float2(t,k/t));

                // sign
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
                    // Line rendering
                    return sdfLine(p, data.xy, data.zw);
                } else {
                    // Conic rendering
                    float e = data.x;
                    float omega = data.y;
                    float2 offset = data.zw;
                    float s = sin(-omega);
                    float c = cos(-omega);

                    // rotate coords so conic vertex is on the bottom
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

            fixed4 frag (v2f input) : SV_Target
            {
                const float lineWidth = .005;

                float2 p = 2*(input.uv - .5);

                float3 color = float3(0, 0, 0);
                for (int i = 0; i < shapeCount; i++) {
                    float alpha = antialias(abs(shape(p, i)) - lineWidth);
                    color *= 1.0 - alpha;
                    color += shapeColors[i] * alpha;
                }

                int row = (int)((1.0 - input.uv.y) * TEXT_ROWS);
                int col = (int)(input.uv.x * TEXT_COLUMNS);

                int index = row*TEXT_COLUMNS + col;
                int c = (int)charGrid[index];
                if (c == 0) {
                    c = 0x20; // space
                }

                float gx = input.uv.x*TEXT_COLUMNS - col;
                float gy = (1.0 - input.uv.y)*TEXT_ROWS - row;

                float glyph = antialias(drawGlyph(c, gx, 1.0 - gy));
                color *= 1.0 - glyph;
                color += charColors[index] * glyph;

                fixed4 res = fixed4(color, 1.0);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, res);
                return res;
            }
            ENDCG
        }
    }
}
