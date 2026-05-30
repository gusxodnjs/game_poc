#!/usr/bin/env python3
"""Post-process PixelLab map objects into game-ready tree/bush/stump sprites.

Source raw PNGs are produced by PixelLab create_map_object (transparent bg).
This script:
  1. Ensures RGBA + clean transparency (any near-zero-alpha -> alpha 0).
  2. Re-anchors content to the BOTTOM-center of a fixed canvas so objects can
     be planted on a tile by their base (canopy rises upward).
  3. Writes final PNGs under Assets/world/objects/.

Trees: 48x64 (bottom-anchored, a couple px of ground padding under trunk).
Bush / stump: 32x32 (bottom-anchored, small ground padding).

Re-run is idempotent given the same raw inputs in /tmp/treeobj.
"""
import os
from PIL import Image

RAW = "/tmp/treeobj"
OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "Assets", "world", "objects")

# (raw filename, output filename, canvas WxH, bottom padding px)
JOBS = [
    ("tree_a_raw.png", "tree_pine_a_48x64.png", (48, 64), 1),
    ("tree_b_raw.png", "tree_pine_b_48x64.png", (48, 64), 1),
    ("bush_raw.png",   "bush_32x32.png",        (32, 32), 1),
    ("stump_raw.png",  "stump_32x32.png",       (32, 32), 1),
]

ALPHA_CUTOFF = 16  # treat alpha below this as fully transparent (kill halos)


def clean_alpha(im: Image.Image) -> Image.Image:
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < ALPHA_CUTOFF:
                px[x, y] = (0, 0, 0, 0)
    return im


def bottom_anchor(im: Image.Image, canvas, pad_bottom: int) -> Image.Image:
    cw, ch = canvas
    bbox = im.getbbox()
    if bbox is None:
        return Image.new("RGBA", canvas, (0, 0, 0, 0))
    content = im.crop(bbox)
    cwid, chei = content.size
    # scale down only if content is larger than canvas (keep crisp, no upscale)
    if cwid > cw or chei > (ch - pad_bottom):
        scale = min(cw / cwid, (ch - pad_bottom) / chei)
        new = (max(1, int(cwid * scale)), max(1, int(chei * scale)))
        content = content.resize(new, Image.NEAREST)
        cwid, chei = content.size
    out = Image.new("RGBA", canvas, (0, 0, 0, 0))
    x = (cw - cwid) // 2
    y = ch - pad_bottom - chei
    out.alpha_composite(content, (max(0, x), max(0, y)))
    return out


def corner_alphas(im):
    w, h = im.size
    p = im.load()
    return [p[0, 0][3], p[w - 1, 0][3], p[0, h - 1][3], p[w - 1, h - 1][3]]


def main():
    os.makedirs(OUT, exist_ok=True)
    for raw, name, canvas, pad in JOBS:
        src = os.path.join(RAW, raw)
        im = Image.open(src)
        im = clean_alpha(im)
        im = bottom_anchor(im, canvas, pad)
        dst = os.path.join(OUT, name)
        im.save(dst)
        print(f"{name}: size={im.size} corners_alpha={corner_alphas(im)} "
              f"content_bbox={im.getbbox()}")


if __name__ == "__main__":
    main()
