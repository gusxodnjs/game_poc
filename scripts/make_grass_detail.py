#!/usr/bin/env python3
"""Post-process PixelLab map objects into small, bottom-anchored 16x16 grass-detail sprites.

PixelLab's minimum canvas is 32x32. These map objects come back transparent already, but
centered in the 32 canvas at varying sizes. For dense overworld scatter we want each detail
to read as a TINY accent: cropped to content, scaled down, and bottom-anchored within a
16x16 RGBA canvas with lots of empty space around it.

Run from repo root:  python3 scripts/make_grass_detail.py
"""
import os
from PIL import Image

RAW_DIR = "/tmp/grass_raw"
OUT_DIR = "Assets/world/objects"
CANVAS = 16

# (raw_name, out_name, target_content_height, strip_dirt) — target height in px after
# downscale; strip_dirt recolors the brown map-object base to green (flowers only).
# Keep heights small so the sprites read as subtle accents over grass, not full tiles.
JOBS = [
    ("tuft_a",       "tuft_grass_a_16x16.png",  11, False),
    ("tuft_b",       "tuft_grass_b_16x16.png",  12, False),
    ("tuft_c",       "tuft_grass_c_16x16.png",   9, False),
    ("flower_white", "flower_white_16x16.png",  12, True),
    ("flower_red",   "flower_red_16x16.png",    12, True),
]

# Bottom margin: leave a couple transparent rows under the base so the anchor sits just
# above the very bottom edge (reads better when scattered on a tile grid).
BOTTOM_MARGIN = 1


def alpha_clip(im, thresh=24):
    """Force near-zero alpha to fully transparent (clean edges)."""
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < thresh:
                px[x, y] = (r, g, b, 0)
    return im


def degreen_dirt(im):
    """PixelLab map objects add a small brown 'dirt mound' base under the object.
    For flowers we want bright pops sitting directly on grass, not on a dirt speck.
    Recolor clearly-brown pixels (red-dominant, low blue, mid-dark) to a green stem tone.
    """
    px = im.load()
    w, h = im.size
    stem = (60, 120, 40)   # green stem replacement
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            # brown/soil heuristic: red noticeably > green > blue, and not very bright
            if r > g >= b and (r - b) > 25 and r < 200 and not (g > 110):
                px[x, y] = (stem[0], stem[1], stem[2], a)
    return im


def process(raw_name, out_name, target_h, strip_dirt=False):
    src = Image.open(os.path.join(RAW_DIR, raw_name + ".png")).convert("RGBA")
    src = alpha_clip(src)
    if strip_dirt:
        src = degreen_dirt(src)
    bbox = src.getbbox()
    content = src.crop(bbox)
    cw, ch = content.size

    # Scale content so its height == target_h, preserving aspect ratio.
    scale = target_h / ch
    new_w = max(1, round(cw * scale))
    new_h = max(1, round(ch * scale))
    # Never let it exceed the canvas.
    if new_w > CANVAS:
        new_w = CANVAS
    content = content.resize((new_w, new_h), Image.NEAREST)

    canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    x = (CANVAS - new_w) // 2                       # horizontally centered
    y = CANVAS - new_h - BOTTOM_MARGIN              # bottom-anchored
    if y < 0:
        y = 0
    canvas.alpha_composite(content, (x, y))
    canvas = alpha_clip(canvas)

    out_path = os.path.join(OUT_DIR, out_name)
    canvas.save(out_path)
    return out_path


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for raw_name, out_name, target_h, strip_dirt in JOBS:
        p = process(raw_name, out_name, target_h, strip_dirt)
        print("wrote", p)


if __name__ == "__main__":
    main()
