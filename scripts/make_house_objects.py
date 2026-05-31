#!/usr/bin/env python3
"""Post-process PixelLab map-object houses into bottom-anchored 64x64 sprites.

create_map_object returns clean transparent RGBA (corners alpha 0). This script:
  1. clips near-transparent stray pixels (alpha < 16 -> 0),
  2. crops to the content bounding box,
  3. re-centers horizontally and anchors the base to the bottom edge of a
     64x64 canvas (front wall sits on the bottom, roof rising up),
  4. never upscales (NEAREST downscale only if content exceeds the canvas).

Inputs are the raw downloads in /tmp/house/{a,b}_raw.png; outputs go to
Assets/world/objects/house_{a,b}_64x64.png.
"""
from PIL import Image
import os

CANVAS = 64
ALPHA_CLIP = 16
OUT_DIR = "Assets/world/objects"

JOBS = [
    ("/tmp/house/a_raw.png", "house_a_64x64.png"),
    ("/tmp/house/b_raw.png", "house_b_64x64.png"),
]


def clip_alpha(im: Image.Image) -> Image.Image:
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < ALPHA_CLIP:
                px[x, y] = (0, 0, 0, 0)
    return im


def process(src: str, out_name: str) -> None:
    im = Image.open(src).convert("RGBA")
    im = clip_alpha(im)
    bbox = im.getbbox()
    if bbox is None:
        raise SystemExit(f"{src}: empty after alpha clip")
    content = im.crop(bbox)
    cw, ch = content.size

    # Downscale if larger than canvas (preserve pixel crispness with NEAREST).
    if cw > CANVAS or ch > CANVAS:
        scale = min(CANVAS / cw, CANVAS / ch)
        content = content.resize(
            (max(1, round(cw * scale)), max(1, round(ch * scale))), Image.NEAREST
        )
        cw, ch = content.size

    canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    ox = (CANVAS - cw) // 2          # horizontal center
    oy = CANVAS - ch                 # bottom anchor
    canvas.alpha_composite(content, (ox, oy))

    os.makedirs(OUT_DIR, exist_ok=True)
    dst = os.path.join(OUT_DIR, out_name)
    canvas.save(dst)
    print(f"wrote {dst}  content={cw}x{ch}  offset=({ox},{oy})")


if __name__ == "__main__":
    for src, out_name in JOBS:
        process(src, out_name)
