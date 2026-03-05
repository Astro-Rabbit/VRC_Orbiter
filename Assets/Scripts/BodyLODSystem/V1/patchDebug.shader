Shader "Orbiter/MoonTileDebug"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0,0,0,1)
        _LineColor("Line Color", Color) = (0,0,0,1)
        _LineWidthDeg("Line Width (deg)", Float) = 0.15

        // Tile bounds in degrees: (lonMin, lonMax, latMin, latMax)
        _Tile0("Tile0", Vector) = (0,0,0,0)
        _Tile1("Tile1", Vector) = (0,0,0,0)
        _Tile2("Tile2", Vector) = (0,0,0,0)
        _Tile3("Tile3", Vector) = (0,0,0,0)
        _Tile4("Tile4", Vector) = (0,0,0,0)
        _Tile5("Tile5", Vector) = (0,0,0,0)
        _Tile6("Tile6", Vector) = (0,0,0,0)
        _Tile7("Tile7", Vector) = (0,0,0,0)
        _Tile8("Tile8", Vector) = (0,0,0,0)
        _Tile9("Tile9", Vector) = (0,0,0,0)
        _Tile10("Tile10", Vector) = (0,0,0,0)
        _Tile11("Tile11", Vector) = (0,0,0,0)
        _Tile12("Tile12", Vector) = (0,0,0,0)
        _Tile13("Tile13", Vector) = (0,0,0,0)
        _Tile14("Tile14", Vector) = (0,0,0,0)
        _Tile15("Tile15", Vector) = (0,0,0,0)

        // Per-tile color (rgba)
        _C0("C0", Color) = (1,0,0,1)
        _C1("C1", Color) = (0,1,0,1)
        _C2("C2", Color) = (0,0,1,1)
        _C3("C3", Color) = (1,1,0,1)
        _C4("C4", Color) = (1,0,1,1)
        _C5("C5", Color) = (0,1,1,1)
        _C6("C6", Color) = (1,0.5,0,1)
        _C7("C7", Color) = (0.5,1,0,1)
        _C8("C8", Color) = (0,0.5,1,1)
        _C9("C9", Color) = (1,0,0.5,1)
        _C10("C10", Color) = (0.5,0,1,1)
        _C11("C11", Color) = (0,1,0.5,1)
        _C12("C12", Color) = (0.6,0.6,0.6,1)
        _C13("C13", Color) = (1,1,1,1)
        _C14("C14", Color) = (0.2,0.2,0.2,1)
        _C15("C15", Color) = (0.9,0.3,0.3,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            fixed4 _LineColor;
            float  _LineWidthDeg;

            float4 _Tile0,_Tile1,_Tile2,_Tile3,_Tile4,_Tile5,_Tile6,_Tile7,_Tile8,_Tile9,_Tile10,_Tile11,_Tile12,_Tile13,_Tile14,_Tile15;
            fixed4 _C0,_C1,_C2,_C3,_C4,_C5,_C6,_C7,_C8,_C9,_C10,_C11,_C12,_C13,_C14,_C15;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 nrmW : TEXCOORD0;
            };

            // Map world normal on unit sphere to lat/lon (deg).
            // Convention:
            //  - +Y is north
            //  - lon=0 at +X
            //  - lon=+90 at +Z
            void NormalToLatLonDeg(float3 n, out float latDeg, out float lonDeg)
            {
                n = normalize(n);
                float lat = asin(clamp(n.y, -1.0, 1.0));
                float lon = atan2(n.z, n.x); // [-pi, pi]
                latDeg = lat * 57.2957795;
                lonDeg = lon * 57.2957795;
            }

            // Check if lon is inside interval [a,b] with wrapping allowed, in degrees, where lon in [-180,180)
            bool LonInWrappedInterval(float lon, float a, float b)
            {
                // normalize all to [-180,180)
                // assume inputs already in that range
                if (a <= b) return (lon >= a && lon <= b);
                // wrapped interval: [a,180] U [-180,b]
                return (lon >= a || lon <= b);
            }

            bool InTile(float lonDeg, float latDeg, float4 tile)
            {
                float lonMin = tile.x;
                float lonMax = tile.y;
                float latMin = tile.z;
                float latMax = tile.w;

                if (latDeg < latMin || latDeg > latMax) return false;
                return LonInWrappedInterval(lonDeg, lonMin, lonMax);
            }

            // Distance to nearest boundary in degrees (rough; used to draw tile outlines)
            float DistToTileEdgeDeg(float lonDeg, float latDeg, float4 tile)
            {
                float lonMin = tile.x;
                float lonMax = tile.y;
                float latMin = tile.z;
                float latMax = tile.w;

                float dLat = min(abs(latDeg - latMin), abs(latDeg - latMax));

                // For lon, handle wrap; compute shortest distance to either boundary along lon axis.
                float d1 = abs(lonDeg - lonMin);
                float d2 = abs(lonDeg - lonMax);

                // wrap distances
                d1 = min(d1, 360.0 - d1);
                d2 = min(d2, 360.0 - d2);

                float dLon = min(d1, d2);
                return min(dLat, dLon);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);

                // For a sphere centered at origin with uniform scale, object normal ~ world normal.
                // Use object normal in world space:
                o.nrmW = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float latDeg, lonDeg;
                NormalToLatLonDeg(i.nrmW, latDeg, lonDeg);

                fixed4 col = _BaseColor;

                // Find the first matching tile (simple; good enough for debug)
                // Also draw outlines
                float bestEdge = 1e9;
                fixed4 bestCol = col;
                bool hit = false;

                float4 tiles[16] = { _Tile0,_Tile1,_Tile2,_Tile3,_Tile4,_Tile5,_Tile6,_Tile7,_Tile8,_Tile9,_Tile10,_Tile11,_Tile12,_Tile13,_Tile14,_Tile15 };
                fixed4 cols[16]  = { _C0,_C1,_C2,_C3,_C4,_C5,_C6,_C7,_C8,_C9,_C10,_C11,_C12,_C13,_C14,_C15 };

                [unroll] for (int t = 0; t < 16; t++)
                {
                    // Treat (0,0,0,0) as unused
                    if (tiles[t].x == 0 && tiles[t].y == 0 && tiles[t].z == 0 && tiles[t].w == 0) continue;

                    if (InTile(lonDeg, latDeg, tiles[t]))
                    {
                        hit = true;
                        bestCol = cols[t];
                        bestEdge = min(bestEdge, DistToTileEdgeDeg(lonDeg, latDeg, tiles[t]));
                    }
                }

                if (hit) col = bestCol;

                // Outline
                if (hit && bestEdge < _LineWidthDeg)
                {
                    col = _LineColor;
                }

                return col;
            }
            ENDCG
        }
    }
}