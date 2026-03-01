Shader "Unlit/MFDGraphicsShader"
{
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

            // Must match value in MFD.cs
            #define MAX_SHAPES 256

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
                    float s = sin(omega);
                    float c = cos(omega);

                    // rotate coords so conic vertex is on the bottom
                    float2 pr = float2(p.x*c - p.y*s, p.y*c + p.x*s);

                    if (1 - e > 0) {
                        float ap = (1 + e) / (1 - e) * pe;
                        float major = (ap + pe) * 0.5;
                        float minor = sqrt(ap * pe);

                        float2 center = (major - pe) * float2(-s, c);

                        return sdfEllipse(pr - offset - center, float2(minor, major));
                    } else if (e > 1) {
                        return 1;
                    } else {
                        return 1;
                    }
                }
            }

            fixed4 frag (v2f i) : SV_Target
            {
                const float lineWidth = .005;

                float2 p = 2*(i.uv - .5);
                float mag = abs(sdfEllipse(p, float2(.5,.25)));
                mag = mag < lineWidth ? 1 : 0;

                float3 col = float3(0, 0, 0);
                for (int i = 0; i < shapeCount; i++) {
                    if (abs(shape(p, i)) < lineWidth) {
                        col = shapeColors[i];
                    }
                }

                fixed4 res = fixed4(col, 0);
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, res);
                return res;
            }
            ENDCG
        }
    }
}
