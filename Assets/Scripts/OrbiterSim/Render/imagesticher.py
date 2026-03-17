from PIL import Image
import os

# ---------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------
tile_size = 512

level7_path = r"H:\Orbiter-2024\Textures\Earth\Surf\07"
level8_path = r"H:\Orbiter-2024\Textures\Earth\Surf\08"
output_file = r"H:\3d objects\VRC_Orbiter\Assets\Scripts\OrbiterSim\Earth_16k.png"

# Orbiter quadtree counts
# level 7 -> 16 x 8 tiles  = 8192 x 4096
# level 8 -> 32 x 16 tiles = 16384 x 8192
lvl7_lon_tiles = 16
lvl7_lat_tiles = 8
lvl8_lon_tiles = 32
lvl8_lat_tiles = 16

# ---------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------
def load_tile(tile_path):
    if not os.path.exists(tile_path):
        return None
    try:
        return Image.open(tile_path).convert("RGB")
    except Exception as e:
        print(f"Failed to load {tile_path}: {e}")
        return None

def paste_level(level_path, lat_tiles, lon_tiles, atlas, label):
    found = 0
    for lat in range(lat_tiles):
        lat_dir = os.path.join(level_path, f"{lat:06d}")
        for lon in range(lon_tiles):
            tile_path = os.path.join(lat_dir, f"{lon:06d}.dds")
            tile = load_tile(tile_path)
            if tile is None:
                continue

            x = lon * tile_size
            y = lat * tile_size
            atlas.paste(tile, (x, y))
            found += 1

    print(f"{label}: pasted {found} tiles")
    return found

# ---------------------------------------------------------------------
# Pass 1: build full atlas from level 7
# ---------------------------------------------------------------------
lvl7_atlas = Image.new("RGB", (lvl7_lon_tiles * tile_size, lvl7_lat_tiles * tile_size))
paste_level(level7_path, lvl7_lat_tiles, lvl7_lon_tiles, lvl7_atlas, "Level 7")

# Upscale level 7 atlas to level 8 size
atlas = lvl7_atlas.resize(
    (lvl8_lon_tiles * tile_size, lvl8_lat_tiles * tile_size),
    resample=Image.Resampling.BILINEAR
)

# ---------------------------------------------------------------------
# Pass 2: overwrite with actual level 8 detail where present
# ---------------------------------------------------------------------
paste_level(level8_path, lvl8_lat_tiles, lvl8_lon_tiles, atlas, "Level 8")

# ---------------------------------------------------------------------
# Save
# ---------------------------------------------------------------------
atlas.save(output_file)
print("Saved:", output_file)