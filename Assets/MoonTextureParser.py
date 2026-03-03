import math
import os
import subprocess

# ----------------------------
# CONFIG (edit these)
# ----------------------------
INPUT_TIF = r"H:/OrbiterTextures/Lunar_LRO_LROC-WAC_Mosaic_global_100m_June2013.tif"
OUT_ROOT  = r"H:\OrbiterTextures\Moon_Albedo_1024"

P     = 1024     # tile resolution (pixels)
N_MIN = 6
N_MAX = 6       # correct for W=109164 with P=1024

# Moon radius from your CRS
R_M = 1737400.0
K   = R_M * math.pi / 180.0  # metres per degree

# If you want .jpg instead of .png, change EXT and optionally add JPEG options later.
EXT = "png"

def run(cmd):
    subprocess.check_call(cmd, shell=False)

def tile_bounds_deg(n: int, ilat: int, ilng: int):
    # Orbiter quadtree convention
    nlng = 1 << (n - 3)
    nlat = 1 << (n - 4)

    dlon = 360.0 / nlng
    dlat = 180.0 / nlat

    lon_min = -180.0 + dlon * ilng
    lon_max = lon_min + dlon

    lat_max =  90.0 - dlat * ilat   # ilat=0 is northmost band
    lat_min =  lat_max - dlat

    return lon_min, lon_max, lat_min, lat_max

def deg_to_m(lon_deg: float, lat_deg: float):
    return lon_deg * K, lat_deg * K

def fmt3(i: int) -> str:
    return f"{i:03d}"

def fmt2(i: int) -> str:
    return f"{i:02d}"

def main():
    os.makedirs(OUT_ROOT, exist_ok=True)

    total = 0

    for n in range(N_MIN, N_MAX + 1):
        nlng = 1 << (n - 3)
        nlat = 1 << (n - 4)

        level_dir = os.path.join(OUT_ROOT, f"n{fmt2(n)}")
        os.makedirs(level_dir, exist_ok=True)

        print(f"Level n={n}: nlng={nlng} nlat={nlat} tiles={nlng*nlat}")

        for ilat in range(nlat):
            # latitude band folder
            band_dir = os.path.join(level_dir, fmt3(ilat))
            os.makedirs(band_dir, exist_ok=True)

            for ilng in range(nlng):
                lon_min, lon_max, lat_min, lat_max = tile_bounds_deg(n, ilat, ilng)

                x_min, y_min = deg_to_m(lon_min, lat_min)
                x_max, y_max = deg_to_m(lon_max, lat_max)

                out_path = os.path.join(band_dir, f"{fmt3(ilng)}.{EXT}")

                cmd = [
                    "gdal_translate",
                    "-projwin", f"{x_min}", f"{y_max}", f"{x_max}", f"{y_min}",
                    "-outsize", str(P), str(P),
                    "-a_nodata", "0",
                    INPUT_TIF,
                    out_path
                ]
                run(cmd)
                total += 1

        print(f"  done n={n}\n")

    print(f"Done. Wrote {total} tiles to: {OUT_ROOT}")

if __name__ == "__main__":
    main()