"""Build FLAT corner-Wang autotile sheets via MASK-COMPOSITE (issue #52).

PROBLEM: the previous *_auto_128.png sheets were made with PixelLab's
create_topdown_tileset, which renders each feature as an ELEVATED plateau with a
dark drop-shadow / ledge dropping to grass. In-game a dirt path looked like a
mesa with a cliff face. The user wants FLAT, same-ground-plane transitions like
classic Pokemon: the path is just a dirt patch ON the same level as the grass,
with a soft natural border. No cliff, no ledge, no drop shadow.

SOLUTION (this script): do NOT use create_topdown_tileset for the transitions.
Instead:
  1. Generate ONE flat 32x32 interior texture per feature via PixelLab pixflux
     (flat top-down, no outline, no shadow, flat lighting).
  2. Flatten it defensively (strip dark outline pixels, compress contrast) and
     make it roughly seamless (offset-and-heal) so it tiles without grid lines.
  3. Composite that flat feature over the existing grass base
     (Assets/world/tiles/grass_v0_32.png) using a soft corner mask per
     cornerIndex. The feathered boundary is a few pixels wide -> natural soft
     dirt/grass edge, structurally incapable of being a cliff.
  4. Assemble the 16 tiles into a 128x128 Layout A sheet.

Layout A (LOCKED — the renderer depends on it):
  cornerIndex = NW*8 + NE*4 + SW*2 + SE   (bit set => that corner is feature)
  col = ci % 4, row = ci // 4   (row 0 = top)
  cell(0,0)=all grass, cell(3,3)=solid feature.

Pure PIL (repo has no numpy). PNG signature validated on every download.
"""
import base64
import json
import sys
import time
import urllib.error
import urllib.request
from io import BytesIO
from pathlib import Path

try:
    from PIL import Image, ImageFilter
except ImportError:
    sys.exit("Pillow is required: pip3 install Pillow")

ROOT = Path(__file__).resolve().parent.parent
ENV_FILE = ROOT / ".env.local"
TILES_DIR = ROOT / "Assets" / "world" / "tiles"
GRASS_BASE = TILES_DIR / "grass_v0_32.png"
TMP = Path("/tmp")
ENDPOINT = "https://api.pixellab.ai/v1/generate-image-pixflux"
PNG_SIG = b"\x89PNG\r\n\x1a\n"

TILE = 32
SHEET = 128

# Flat interior textures. Keep the established good colors; force flat lighting,
# no outline, no shadow. We still post-flatten defensively in case the model
# bakes an outline or shading anyway.
FEATURES = [
    (
        "path",
        "flat top-down pixel art tile of plain tan dirt path ground, dry packed "
        "earth, subtle small pebbles, uniform flat lighting, no outline, no "
        "shadow, no elevation, no cliff, seamless tileable texture",
    ),
    (
        "road",
        "flat top-down pixel art tile of gray asphalt road surface with a faint "
        "pale dashed center line, uniform flat lighting, no outline, no shadow, "
        "no elevation, no cliff, seamless tileable texture",
    ),
    (
        "water",
        "flat top-down pixel art tile of shallow blue water surface, gentle calm "
        "ripples, uniform flat lighting, no outline, no shadow, no elevation, "
        "seamless tileable texture",
    ),
    (
        "forest",
        "flat top-down pixel art tile of dense dark green forest canopy, leafy "
        "treetops seen from directly above, uniform flat lighting, no outline, "
        "no shadow, no elevation, no cliff, seamless tileable texture",
    ),
    (
        "building",
        "flat top-down pixel art tile of gray-brown building roof, plain shingles, "
        "uniform flat lighting, no outline, no shadow, no elevation, no cliff, "
        "seamless tileable texture",
    ),
]


def load_api_key() -> str:
    if not ENV_FILE.exists():
        sys.exit(f"Missing {ENV_FILE}")
    for line in ENV_FILE.read_text().splitlines():
        line = line.strip()
        if line.startswith("PIXELLAB_API_KEY="):
            return line.split("=", 1)[1].strip()
    sys.exit("PIXELLAB_API_KEY not found")


def call_pixflux(api_key: str, prompt: str, w: int, h: int):
    payload = {
        "description": prompt,
        "image_size": {"width": w, "height": h},
        # We WANT a full opaque texture here (no transparency) — it's an
        # interior ground tile, not a sprite.
        "no_background": False,
        "text_guidance_scale": 9.0,
    }
    req = urllib.request.Request(
        ENDPOINT,
        data=json.dumps(payload).encode("utf-8"),
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=180) as resp:
        body = json.loads(resp.read().decode("utf-8"))
    b64 = body["image"]["base64"]
    raw = base64.b64decode(b64)
    if not raw.startswith(PNG_SIG):
        raise RuntimeError(f"invalid PNG signature (len={len(raw)})")
    return raw


# ----------------------------------------------------------------------------
# Flattening: strip baked dark outlines / drop-shadows and compress contrast so
# the feature reads as a flat ground patch, not an embossed plateau.
# ----------------------------------------------------------------------------
def flatten_texture(img: Image.Image) -> Image.Image:
    """Pull each pixel toward the texture mean to kill high-contrast outline/
    shadow pixels, while preserving enough variation to read as a texture.

    Dark near-black pixels (typical baked outline / drop-shadow) get pulled
    HARD toward the mean so a black ledge cannot survive into the composite.
    """
    img = img.convert("RGB")
    px = list(img.getdata())
    n = len(px)
    mr = sum(p[0] for p in px) / n
    mg = sum(p[1] for p in px) / n
    mb = sum(p[2] for p in px) / n

    out = []
    for r, g, b in px:
        lum = 0.299 * r + 0.587 * g + 0.114 * b
        # how dark vs the mean luminance
        mean_lum = 0.299 * mr + 0.587 * mg + 0.114 * mb
        if lum < mean_lum - 55:
            # likely an outline / drop-shadow pixel -> pull strongly to mean
            k = 0.85
        else:
            # general contrast compression toward mean (keep gentle texture)
            k = 0.45
        nr = int(r + (mr - r) * k)
        ng = int(g + (mg - g) * k)
        nb = int(b + (mb - b) * k)
        out.append((max(0, min(255, nr)), max(0, min(255, ng)), max(0, min(255, nb))))
    flat = Image.new("RGB", img.size)
    flat.putdata(out)
    return flat


def make_seamless(img: Image.Image) -> Image.Image:
    """Offset-and-heal: roll by (W/2,H/2) so the original wrap seam moves to a
    central cross, then blur-heal that cross band. New outer edges become old
    interior pixels => they wrap-match. Pure PIL."""
    w, h = img.size
    # PIL roll via paste
    rolled = Image.new("RGB", (w, h))
    ox, oy = w // 2, h // 2
    rolled.paste(img.crop((w - ox, h - oy, w, h)), (0, 0))
    rolled.paste(img.crop((0, h - oy, w - ox, h)), (ox, 0))
    rolled.paste(img.crop((w - ox, 0, w, h - oy)), (0, oy))
    rolled.paste(img.crop((0, 0, w - ox, h - oy)), (ox, oy))

    # heal the central cross (the moved seam) with a blurred copy
    blurred = rolled.filter(ImageFilter.GaussianBlur(1.4))
    band = 3
    healed = rolled.copy()
    # vertical band around x=ox
    healed.paste(blurred.crop((ox - band, 0, ox + band, h)), (ox - band, 0))
    # horizontal band around y=oy
    healed.paste(blurred.crop((0, oy - band, w, oy + band)), (0, oy - band))
    return healed


# ----------------------------------------------------------------------------
# Corner-mask compositing
# ----------------------------------------------------------------------------
def smoothstep_lut():
    # soft threshold: hard grass below 110, hard feature above 145, few-px
    # feather between. Natural soft edge, not a wide gradient.
    return [0 if v < 110 else (255 if v > 145 else int((v - 110) / 35 * 255)) for v in range(256)]


SMOOTH = smoothstep_lut()


def corner_mask(ci: int) -> Image.Image:
    """2x2 grayscale (NW,NE / SW,SE) for cornerIndex, upscaled+feathered to 32x32."""
    nw = 255 if (ci & 8) else 0
    ne = 255 if (ci & 4) else 0
    sw = 255 if (ci & 2) else 0
    se = 255 if (ci & 1) else 0
    m2 = Image.new("L", (2, 2))
    m2.putdata([nw, ne, sw, se])
    m = m2.resize((TILE, TILE), Image.BILINEAR)
    m = m.point(SMOOTH)
    return m


def build_sheet(feat_rgb: Image.Image, grass_rgba: Image.Image) -> Image.Image:
    feat = feat_rgb.convert("RGBA")
    grass = grass_rgba.convert("RGBA")
    sheet = Image.new("RGBA", (SHEET, SHEET), (0, 0, 0, 0))
    for ci in range(16):
        mask = corner_mask(ci)
        tile = Image.composite(feat, grass, mask)
        col = ci % 4
        row = ci // 4
        sheet.paste(tile, (col * TILE, row * TILE))
    return sheet


# ----------------------------------------------------------------------------
# Montage: simulate a feature region surrounded by grass on a 6x6 field. For
# each cell decide, per its 4 corners, whether that corner touches the
# 3x3 feature block, then pick the cornerIndex tile. This is exactly how the
# renderer would assemble it, so it proves the borders read flat.
# ----------------------------------------------------------------------------
def montage(sheet: Image.Image, feature_name: str) -> Image.Image:
    GRID = 6
    # mark a 3x3 block of "feature vertices". Vertex (vx,vy) is feature if it is
    # an inner corner of the 3x3 cell block (cells 2..3 -> vertices 2,3,4ish).
    # We define vertices on a (GRID+1)x(GRID+1) lattice; feature vertices form a
    # 3x3 inner square (vertices 2,3,4 in both axes -> covers center cells).
    feat_vertex = set()
    for vy in range(2, 5):
        for vx in range(2, 5):
            feat_vertex.add((vx, vy))

    out = Image.new("RGBA", (GRID * TILE, GRID * TILE), (0, 0, 0, 0))
    for cy in range(GRID):
        for cx in range(GRID):
            nw = (cx, cy) in feat_vertex
            ne = (cx + 1, cy) in feat_vertex
            sw = (cx, cy + 1) in feat_vertex
            se = (cx + 1, cy + 1) in feat_vertex
            ci = (8 if nw else 0) + (4 if ne else 0) + (2 if sw else 0) + (1 if se else 0)
            col, row = ci % 4, ci // 4
            tile = sheet.crop((col * TILE, row * TILE, col * TILE + TILE, row * TILE + TILE))
            out.paste(tile, (cx * TILE, cy * TILE))
    # upscale 4x nearest for human inspection
    return out.resize((GRID * TILE * 4, GRID * TILE * 4), Image.NEAREST)


def main() -> int:
    api_key = load_api_key()
    grass = Image.open(GRASS_BASE).convert("RGBA")
    if grass.size != (TILE, TILE):
        grass = grass.resize((TILE, TILE), Image.NEAREST)

    results = {"features": []}
    for name, prompt in FEATURES:
        print(f"[{name}] generating flat feature texture...", flush=True)
        raw = None
        last_err = None
        for attempt in range(1, 4):
            try:
                raw = call_pixflux(api_key, prompt, TILE, TILE)
                break
            except Exception as e:  # noqa: BLE001
                last_err = e
                print(f"  attempt {attempt} failed: {e}", flush=True)
                time.sleep(3)
        if raw is None:
            print(f"  [FAIL] {name}: {last_err}", flush=True)
            results["features"].append({"name": name, "ok": False, "error": str(last_err)})
            continue

        feat_raw = Image.open(BytesIO(raw)).convert("RGB")
        if feat_raw.size != (TILE, TILE):
            feat_raw = feat_raw.resize((TILE, TILE), Image.NEAREST)
        # save the raw flat texture for inspection
        feat_raw.save(TMP / f"feat_{name}_raw.png")

        flat = flatten_texture(feat_raw)
        seamless = make_seamless(flat)
        seamless.save(TMP / f"feat_{name}_flat.png")

        sheet = build_sheet(seamless, grass)
        out_path = TILES_DIR / f"{name}_auto_128.png"
        sheet.save(out_path, format="PNG", optimize=True)
        print(f"  wrote {out_path}", flush=True)

        mont = montage(sheet, name)
        mont.save(TMP / f"montage_{name}.png")
        print(f"  wrote /tmp/montage_{name}.png", flush=True)

        results["features"].append({"name": name, "ok": True, "out": str(out_path)})

    (TMP / "make_flat_autotiles_result.json").write_text(json.dumps(results, indent=2))
    print("\nDONE. Results:", json.dumps(results, indent=2), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
