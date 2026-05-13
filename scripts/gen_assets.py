#!/usr/bin/env python3
"""Generate pixel art assets via PixelLab API.

Reads API key from .env.local (PIXELLAB_API_KEY=...).
Saves base64-decoded PNGs to the specified output paths.
Validates PNG signature. Tracks USD usage. Retries once on failure.
"""

import base64
import json
import os
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

# (out_path, width, height, prompt)
JOBS = [
    # 1) characters
    ("assets/characters/walker_front_64x64.png", 64, 64,
     "casual walker character, front view, standing, simple urban clothes, pixel art, transparent background"),
    ("assets/characters/walker_side_64x64.png", 64, 64,
     "casual walker character, side view, walking pose, simple urban clothes, pixel art, transparent background"),
    # 2) tiles
    ("assets/tiles/tile_grass_32x32.png", 32, 32,
     "seamless tileable grass tile, top-down view, pixel art"),
    ("assets/tiles/tile_sidewalk_32x32.png", 32, 32,
     "seamless tileable urban sidewalk tile with concrete blocks, top-down view, pixel art"),
    ("assets/tiles/tile_dirt_path_32x32.png", 32, 32,
     "seamless tileable dirt path tile, top-down view, pixel art, natural"),
    ("assets/tiles/tile_flower_field_32x32.png", 32, 32,
     "seamless tileable flower field tile with small pink and yellow flowers on grass, top-down view, pixel art"),
    # 3) world
    ("assets/world/planet_grey_128x128.png", 128, 128,
     "small grey planet sphere, isolated, simple shading, no atmosphere, pixel art, transparent background"),
    ("assets/world/planet_green_128x128.png", 128, 128,
     "small green planet sphere with light vegetation patches, isolated, simple shading, pixel art, transparent background"),
    # 4) ui
    ("assets/ui/net_icon_32x32.png", 32, 32,
     "butterfly net icon, side view, simple, pixel art, transparent background"),
    ("assets/ui/book_icon_32x32.png", 32, 32,
     "open notebook with bookmark icon, simple, pixel art, transparent background"),
    ("assets/ui/map_pin_32x32.png", 32, 32,
     "red map pin marker, simple, pixel art, transparent background"),
    ("assets/ui/light_spot_16x16.png", 16, 16,
     "glowing yellow light dot, bright center, simple, pixel art, transparent background"),
]


def call_api(width: int, height: int, prompt: str) -> dict:
    payload = json.dumps({
        "description": prompt,
        "image_size": {"width": width, "height": height},
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


def generate_one(out_rel: str, width: int, height: int, prompt: str) -> tuple[bool, float, str]:
    out_path = ROOT / out_rel
    out_path.parent.mkdir(parents=True, exist_ok=True)
    last_err = ""
    for attempt in (1, 2):
        try:
            data = call_api(width, height, prompt)
            b64 = data["image"]["base64"]
            usd = float(data.get("usage", {}).get("usd", 0.0))
            png_bytes = base64.b64decode(b64)
            if not png_bytes.startswith(PNG_SIG):
                raise ValueError("invalid PNG signature")
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
    for rel, w, h, prompt in JOBS:
        print(f"[gen] {rel} ({w}x{h})", flush=True)
        ok, usd, err = generate_one(rel, w, h, prompt)
        total_usd += usd
        results.append({"path": rel, "size": f"{w}x{h}", "ok": ok, "usd": usd, "err": err, "prompt": prompt})
        status = "OK" if ok else "FAIL"
        print(f"  -> {status}  usd={usd:.5f}  {err}", flush=True)
    summary_path = ROOT / "scripts" / "_gen_summary.json"
    summary_path.write_text(json.dumps({
        "total_usd": total_usd,
        "results": results,
    }, indent=2))
    print(f"\nTotal USD: {total_usd:.5f}")
    print(f"Summary: {summary_path}")
    failed = [r for r in results if not r["ok"]]
    if failed:
        print(f"\n{len(failed)} FAILED:")
        for r in failed:
            print(f"  {r['path']}: {r['err']}")
        sys.exit(1)


if __name__ == "__main__":
    main()
