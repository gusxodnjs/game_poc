#!/usr/bin/env python3
"""Assemble PixelLab corner-Wang topdown tilesets into 128x128 (4x4 of 32px)
autotile sheets for the #52 Earth tilemap renderer.

PixelLab generates CORNER-Wang tiles: each tile is defined by its 4 corners
(NW/NE/SW/SE), each either "upper" (=feature) or "lower" (=grass). 16 corners
combos -> 16 tiles -> a complete, lossless autotile set.

We lay tiles out in CORNER-INDEX order:
    cidx = NW*8 + NE*4 + SW*2 + SE*1   (0..15)
    cell  = (col = cidx % 4, row = cidx // 4)   row 0 = top
This same layout is used for ALL 5 sheets, so one MaskCell table works for all.

The renderer's documented 4-EDGE bitmask (N/E/S/W) is a DIFFERENT autotiling
scheme than corner-Wang and cannot be mapped 1:1 (see tileset_layout.md for the
honest explanation + a best-effort edge->cell fallback table). The faithful,
lossless integration is to drive the renderer from a 4-CORNER mask instead.

Usage:
    python3 scripts/assemble_autotiles.py            # download + assemble all 5
"""
import json, os, subprocess, sys
from PIL import Image

TILE = 32
COLS = 4
OUT_DIR = "Assets/world/tiles"
TMP = "/tmp/ts"

# name -> tileset id
TILESETS = {
    "path":     "bcfbc054-108a-4156-b8fc-fa029c4e620e",
    "road":     "231401df-40ca-405d-a04f-cc0b0f786fd9",
    "water":    "ec95c120-b713-402b-946b-f5ef3769bbdd",
    "forest":   "6ac349ba-dfe1-4d39-bd89-52ed5638a6f0",
    "building": "e37f141b-b699-40c5-b677-02c5b164d9ae",
}

def api_key():
    for line in open(".env.local"):
        if line.startswith("PIXELLAB_API_KEY="):
            return line.split("=", 1)[1].strip()
    raise SystemExit("PIXELLAB_API_KEY not in .env.local")

def download(name, tid, key):
    os.makedirs(TMP, exist_ok=True)
    base = f"https://api.pixellab.ai/mcp/tilesets/{tid}"
    img = f"{TMP}/{name}_image.png"
    meta = f"{TMP}/{name}_metadata.json"
    for ep, dst in (("image", img), ("metadata", meta)):
        subprocess.run(
            ["curl", "-sL", "-o", dst, "-H", f"Authorization: Bearer {key}", f"{base}/{ep}"],
            check=True,
        )
    return meta, img

def corner_index(corners):
    def b(v): return 1 if v == "upper" else 0
    return b(corners["NW"]) * 8 + b(corners["NE"]) * 4 + b(corners["SW"]) * 2 + b(corners["SE"])

def assemble(name, meta_path, img_path):
    meta = json.load(open(meta_path))
    sheet = Image.open(img_path).convert("RGBA")
    tiles = {}
    for t in meta["tileset_data"]["tiles"]:
        cidx = corner_index(t["corners"])
        bb = t["bounding_box"]
        crop = sheet.crop((bb["x"], bb["y"], bb["x"] + bb["width"], bb["y"] + bb["height"]))
        if crop.size != (TILE, TILE):
            crop = crop.resize((TILE, TILE), Image.NEAREST)
        tiles[cidx] = crop
    missing = [i for i in range(16) if i not in tiles]
    out = Image.new("RGBA", (TILE * COLS, TILE * COLS), (0, 0, 0, 0))
    for cidx in range(16):
        crop = tiles.get(cidx) or tiles.get(15) or tiles.get(0)
        out.paste(crop, ((cidx % COLS) * TILE, (cidx // COLS) * TILE))
    out_path = f"{OUT_DIR}/{name}_auto_128.png"
    out.save(out_path)
    return out_path, sorted(tiles.keys()), missing

if __name__ == "__main__":
    key = api_key()
    for name, tid in TILESETS.items():
        meta, img = download(name, tid, key)
        out_path, present, missing = assemble(name, meta, img)
        flag = "OK" if not missing else f"MISSING {missing}"
        print(f"{name}: corners={present} {flag} -> {out_path}")
