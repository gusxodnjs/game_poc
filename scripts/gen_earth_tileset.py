#!/usr/bin/env python3
"""Generate the 4 base terrain tiles for the Earth-walk Pikmin tilemap (#52).

Produces 4 individual seamless 32x32 top-down terrain tiles:
  - grass_32.png  : lush green grass
  - path_32.png   : light-brown packed-earth dirt path
  - water_32.png  : shallow blue water with gentle ripples
  - forest_32.png : dense dark-green forest canopy (top-down treetops)

GBA-Pokemon / Stardew Valley aesthetic, harmonized palette (one game world),
each tile must look good tiled in a solid block and be roughly seamless.

Reads API key from .env.local (PIXELLAB_API_KEY=...).
Calls the pixflux REST endpoint directly (project convention), base64-decodes,
validates the PNG signature, and writes one PNG per tile. Retries once on failure.

PixelLab minimum canvas is 32x32, which is exactly our target, so no downscale
is needed here.
"""

import base64
import json
import pathlib
import sys
import time
import urllib.error
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parent.parent
ENV_PATH = ROOT / ".env.local"


def load_api_key() -> str:
    for line in ENV_PATH.read_text().splitlines():
        line = line.strip()
        if line.startswith("PIXELLAB_API_KEY="):
            return line.split("=", 1)[1].strip()
    raise SystemExit("PIXELLAB_API_KEY not found in .env.local")


API_KEY = load_api_key()
ENDPOINT = "https://api.pixellab.ai/v1/generate-image-pixflux"
PNG_SIG = b"\x89PNG\r\n\x1a\n"

STYLE = (
    "16-bit GBA Pokemon overworld / Stardew Valley pixel art style, "
    "top-down map terrain tile, seamless tileable repeating texture filling the "
    "entire canvas edge to edge, flat even lighting, no outline frame, "
    "single uniform terrain type only"
)

# (out_path, width, height, text_guidance_scale, prompt) — output under the shared
# assets/ tree. Prompts are tuned to force a *uniform* base terrain (no scene
# decoration): at 32x32 PixelLab tends to add bushes/grass borders/foliage, so
# grass and path use higher guidance + explicit negatives. Even so, PixelLab may
# leave a few sparse green specks on the dirt tile — this is the best achievable
# bare-dirt result at this size (see gen_earth_tileset_result.json).
JOBS = [
    ("assets/world/tiles/grass_32.png", 32, 32, 11.0,
     "uniform short lush green lawn grass, even fine grass-blade speckle texture, "
     "no bushes, no shrubs, no flowers, no rocks, just plain green grass, " + STYLE),
    ("assets/world/tiles/path_32.png", 32, 32, 11.0,
     "solid packed-earth dirt ground completely filling the tile, light warm brown "
     "tan soil with fine pebble and dirt speckles, NO grass, NO green, NO trail "
     "running through, just uniform bare dry dirt, " + STYLE),
    ("assets/world/tiles/water_32.png", 32, 32, 9.0,
     "shallow calm blue water surface with gentle small ripples, soft highlights, " + STYLE),
    ("assets/world/tiles/forest_32.png", 32, 32, 9.0,
     "dense dark green forest canopy seen from directly above, rounded clustered "
     "treetops, " + STYLE),
]


def call_api(width: int, height: int, guidance: float, prompt: str) -> dict:
    payload = json.dumps({
        "description": prompt,
        "image_size": {"width": width, "height": height},
        "text_guidance_scale": guidance,
        # base terrain tiles must be opaque and fill the canvas (no transparency).
        "no_background": False,
    }).encode("utf-8")
    req = urllib.request.Request(
        ENDPOINT,
        data=payload,
        headers={
            "Authorization": f"Bearer {API_KEY}",
            "Content-Type": "application/json",
        },
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=180) as resp:
        body = resp.read().decode("utf-8")
    return json.loads(body)


def generate_one(out_rel: str, width: int, height: int, guidance: float, prompt: str):
    out_path = ROOT / out_rel
    out_path.parent.mkdir(parents=True, exist_ok=True)
    last_err = ""
    for attempt in (1, 2):
        try:
            data = call_api(width, height, guidance, prompt)
            b64 = data["image"]["base64"]
            usd = float(data.get("usage", {}).get("usd", 0.0))
            png_bytes = base64.b64decode(b64)
            if not png_bytes.startswith(PNG_SIG):
                raise ValueError("invalid PNG signature (got non-PNG body)")
            out_path.write_bytes(png_bytes)
            return True, usd, ""
        except (urllib.error.HTTPError, urllib.error.URLError, ValueError, KeyError) as e:
            last_err = f"{type(e).__name__}: {e}"
            if isinstance(e, urllib.error.HTTPError):
                try:
                    last_err += " | body=" + e.read().decode("utf-8", errors="replace")[:300]
                except Exception:
                    pass
            if attempt == 1:
                time.sleep(2)
            continue
    return False, 0.0, last_err


def main():
    results = []
    total_usd = 0.0
    for rel, w, h, guidance, prompt in JOBS:
        print(f"[gen] {rel} ({w}x{h})", flush=True)
        ok, usd, err = generate_one(rel, w, h, guidance, prompt)
        total_usd += usd
        results.append({"path": rel, "size": f"{w}x{h}", "ok": ok, "usd": usd, "err": err})
        print(f"  -> {'OK' if ok else 'FAIL'}  usd={usd:.5f}  {err}", flush=True)
    summary = ROOT / "scripts" / "gen_earth_tileset_result.json"
    summary.write_text(json.dumps({"total_usd": total_usd, "results": results}, indent=2))
    print(f"\nTotal USD: {total_usd:.5f}\nSummary: {summary}")
    if any(not r["ok"] for r in results):
        sys.exit(1)


if __name__ == "__main__":
    main()
