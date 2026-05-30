#!/usr/bin/env python3
"""
Make 4 SEAMLESS, low-contrast 32x32 grass variants for the Earth tilemap (#52).

Problem being fixed: Assets/world/tiles/grass_32.png is NOT seamless
(left<->right edge diff ~141) and is high-contrast (dark teal blobs,
luminance range ~129), so the single repeated tile reads as an obvious grid.

Look source: a pure-fill grass cell extracted from a PixelLab topdown Wang
tileset (tileset f4a8d3c7..., the "all-grass" tile, std~3.7 -> already calm/
low-contrast, luminance range ~21). PixelLab text->image is NOT reliably
seamless, so we DO NOT trust its edges and re-seam deterministically here.

Pure-PIL (no numpy) so it runs in the repo's externally-managed Python.

Pipeline (deterministic, reproducible):
  1. Load source grass, force exactly 32x32.
  2. Compress contrast toward mean + pull any dark/teal pixel back into the
     green family -> guarantees "calm plain grass" even if source drifts.
  3. Per-variant deterministic texture: sparse light/dark blades + a couple of
     tiny flowers/specks, tiny hue nudge. Seeded RNG -> reproducible. All 4
     stay in one palette so they blend when scattered.
  4. Make tileable via OFFSET-AND-HEAL:
        - roll the image by (W/2, H/2) with wrap (so the original seam, and any
          seam introduced by edits, moves to the interior center cross),
        - median-heal a small band over that central cross from nearby clean
          grass. After the roll the NEW outer edges are former interior pixels,
          which already wrap-match -> low edge diff.
  5. Re-seam is applied AFTER per-variant edits, so edits can't reintroduce a
     visible seam at the wrap boundary.

Verify: seam(path) = (H_edge_diff, V_edge_diff); acceptance requires both < 20.

Usage:
    python3 scripts/make_grass_variants.py [SOURCE_PNG]
Default SOURCE_PNG = /tmp/grass_src.png
Outputs: Assets/world/tiles/grass_v0_32.png .. grass_v3_32.png
"""
import random
import sys
from collections import Counter

from PIL import Image

OUT_DIR = "Assets/world/tiles"
SIZE = 32

# Target base green: close to current ~#82cf1c but a touch softer/desaturated.
BASE = (130, 198, 62)


def clamp(v):
    return 0 if v < 0 else (255 if v > 255 else int(round(v)))


def seam(path):
    im = Image.open(path).convert("RGB")
    W, H = im.size
    px = im.load()
    h = sum(sum(abs(px[0, y][i] - px[W - 1, y][i]) for i in range(3))
            for y in range(H)) / H
    v = sum(sum(abs(px[x, 0][i] - px[x, H - 1][i]) for i in range(3))
            for x in range(W)) / W
    return round(h, 1), round(v, 1)


def load_pixels(img):
    """Return a 2D list [y][x] of (r,g,b) and (W,H)."""
    img = img.convert("RGB")
    if img.size != (SIZE, SIZE):
        img = img.resize((SIZE, SIZE), Image.LANCZOS)
    px = img.load()
    grid = [[px[x, y] for x in range(SIZE)] for y in range(SIZE)]
    return grid


def save_grid(grid, path):
    im = Image.new("RGB", (SIZE, SIZE))
    flat = [grid[y][x] for y in range(SIZE) for x in range(SIZE)]
    im.putdata(flat)
    im.save(path)


def compress_contrast(grid, strength=0.45):
    """Pull pixels toward the mean (lower contrast); strongly pull dark/teal
    pixels back toward BASE green so no high-contrast blobs survive."""
    flat = [grid[y][x] for y in range(SIZE) for x in range(SIZE)]
    mr = sum(p[0] for p in flat) / len(flat)
    mg = sum(p[1] for p in flat) / len(flat)
    mb = sum(p[2] for p in flat) / len(flat)
    out = [[None] * SIZE for _ in range(SIZE)]
    for y in range(SIZE):
        for x in range(SIZE):
            r, g, b = grid[y][x]
            nr = mr + (r - mr) * (1 - strength)
            ng = mg + (g - mg) * (1 - strength)
            nb = mb + (b - mb) * (1 - strength)
            lum = 0.299 * nr + 0.587 * ng + 0.114 * nb
            greenish = ng - 0.5 * (nr + nb)
            if lum < 130 or greenish < 18:        # dark or teal -> green family
                pull = 0.8
                nr = nr * (1 - pull) + BASE[0] * pull
                ng = ng * (1 - pull) + BASE[1] * pull
                nb = nb * (1 - pull) + BASE[2] * pull
            out[y][x] = (clamp(nr), clamp(ng), clamp(nb))
    return out


def apply_variant(grid, idx):
    """Deterministic per-variant texture. Seeded -> reproducible."""
    rng = random.Random(1000 + idx)
    g = [row[:] for row in grid]
    # Keep hue nudges TINY so all 4 variants stay tonally unified -> when the
    # renderer scatters them they read as one field, not brightness blocks.
    hue = [(0, 0, 0), (2, 1, -1), (-2, 2, 1), (1, -1, 2)][idx]
    for y in range(SIZE):
        for x in range(SIZE):
            r, gg, b = g[y][x]
            g[y][x] = (clamp(r + hue[0]), clamp(gg + hue[1]), clamp(b + hue[2]))

    light = (clamp(BASE[0] + 22 + hue[0]),
             clamp(BASE[1] + 24 + hue[1]),
             clamp(BASE[2] + 16 + hue[2]))
    dark = (clamp(BASE[0] - 20), clamp(BASE[1] - 16), clamp(BASE[2] - 12))

    n_blades = [10, 14, 8, 12][idx]
    for _ in range(n_blades):
        x = rng.randrange(SIZE)
        y = rng.randrange(SIZE)
        g[y][x] = light
        if rng.random() < 0.5:
            yy = (y - 1) % SIZE
            r0, g0, b0 = g[yy][x]
            g[yy][x] = (clamp(light[0] * .65 + r0 * .35),
                        clamp(light[1] * .65 + g0 * .35),
                        clamp(light[2] * .65 + b0 * .35))
        if rng.random() < 0.4:
            xx = (x + 1) % SIZE
            r0, g0, b0 = g[y][xx]
            g[y][xx] = (clamp(dark[0] * .5 + r0 * .5),
                        clamp(dark[1] * .5 + g0 * .5),
                        clamp(dark[2] * .5 + b0 * .5))

    flowers = [[], [(8, 9)], [(20, 6), (5, 22)], [(14, 18)]][idx]
    fcol = [None, (232, 226, 122), (235, 235, 235), (224, 182, 218)][idx]
    for (fx, fy) in flowers:
        g[fy % SIZE][fx % SIZE] = fcol
        xx = (fx + 1) % SIZE
        r0, g0, b0 = g[fy % SIZE][xx]
        g[fy % SIZE][xx] = (clamp(fcol[0] * .6 + r0 * .4),
                            clamp(fcol[1] * .6 + g0 * .4),
                            clamp(fcol[2] * .6 + b0 * .4))
    return g


def median_heal_cross(grid, band=3):
    """Hide the central +-shaped seam left by the roll. Replace pixels within
    `band` of the center cross with the median of nearby clean-grass samples."""
    cx = cy = SIZE // 2
    src = [row[:] for row in grid]
    out = [row[:] for row in grid]
    for y in range(SIZE):
        for x in range(SIZE):
            if abs(x - cx) <= band or abs(y - cy) <= band:
                samp = []
                for dy in (-5, -4, 4, 5):
                    for dx in (-5, -4, 4, 5):
                        xx = (x + dx) % SIZE
                        yy = (y + dy) % SIZE
                        if abs(xx - cx) > band and abs(yy - cy) > band:
                            samp.append(src[yy][xx])
                if samp:
                    rs = sorted(p[0] for p in samp)
                    gs = sorted(p[1] for p in samp)
                    bs = sorted(p[2] for p in samp)
                    m = len(samp) // 2
                    out[y][x] = (rs[m], gs[m], bs[m])
    return out


def roll(grid, dy, dx):
    return [[grid[(y - dy) % SIZE][(x - dx) % SIZE]
             for x in range(SIZE)] for y in range(SIZE)]


def make_seamless(grid):
    rolled = roll(grid, SIZE // 2, SIZE // 2)
    return median_heal_cross(rolled, band=3)


def main():
    src_path = sys.argv[1] if len(sys.argv) > 1 else "/tmp/grass_src.png"
    src = Image.open(src_path)
    base = compress_contrast(load_pixels(src), strength=0.45)
    for idx in range(4):
        v = apply_variant(base, idx)
        v = make_seamless(v)            # re-seam AFTER edits
        out = f"{OUT_DIR}/grass_v{idx}_32.png"
        save_grid(v, out)
        print(out, "seam H/V:", seam(out))


if __name__ == "__main__":
    main()
