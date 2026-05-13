#!/usr/bin/env python3
"""Generate 5 species sprites via PixelLab pixflux API, then downsample to 16x16."""
import base64
import json
import os
import sys
import time
import urllib.request
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ENV_FILE = ROOT / ".env.local"
SPRITES_DIR = ROOT / "assets" / "sprites"
ENDPOINT = "https://api.pixellab.ai/v1/generate-image-pixflux"


def load_api_key() -> str:
    if not ENV_FILE.exists():
        sys.exit(f"Missing {ENV_FILE}")
    for line in ENV_FILE.read_text().splitlines():
        line = line.strip()
        if line.startswith("PIXELLAB_API_KEY="):
            return line.split("=", 1)[1].strip()
    sys.exit("PIXELLAB_API_KEY not found in .env.local")


SPECIES = [
    {
        "species_id": "foxtail_grass",
        "prompt": "green foxtail grass plant, fluffy seed spike, side view, pixel art, transparent background",
    },
    {
        "species_id": "white_clover",
        "prompt": "white clover plant with three green leaves, small white flower, top-down view, pixel art, transparent background",
    },
    {
        "species_id": "cherry_blossom",
        "prompt": "pink cherry blossom flower with branch, spring, side view, pixel art, transparent background",
    },
    {
        "species_id": "ladybug",
        "prompt": "red ladybug with black spots, top-down view, pixel art, transparent background",
    },
    {
        "species_id": "honeybee",
        "prompt": "yellow and black honeybee with wings, side view, pixel art, transparent background",
    },
]


def call_pixflux(api_key: str, description: str) -> tuple[dict, float]:
    payload = {
        "description": description,
        "image_size": {"width": 64, "height": 64},
        "no_background": True,
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
    start = time.time()
    with urllib.request.urlopen(req, timeout=180) as resp:
        body = json.loads(resp.read().decode("utf-8"))
    elapsed = time.time() - start
    return body, elapsed


def decode_b64_image(b64: str) -> bytes:
    if b64.startswith("data:"):
        b64 = b64.split(",", 1)[1]
    return base64.b64decode(b64)


def verify_png(data: bytes) -> bool:
    return data[:8] == b"\x89PNG\r\n\x1a\n"


def downsample_pixel_art(src: Path, dst: Path, size: int = 16) -> None:
    """Downsample using PIL with NEAREST for sharpness, with mode mode-aware handling."""
    img = Image.open(src).convert("RGBA")
    # Use LANCZOS for better quality on alpha-heavy pixel art at 64->16 (4x downscale)
    img = img.resize((size, size), Image.LANCZOS)
    img.save(dst, "PNG", optimize=True)


def main() -> int:
    api_key = load_api_key()
    SPRITES_DIR.mkdir(parents=True, exist_ok=True)
    results = []
    total_usd = 0.0
    total_calls = 0

    for spec in SPECIES:
        sid = spec["species_id"]
        prompt = spec["prompt"]
        out64 = SPRITES_DIR / f"{sid}_64x64.png"
        out16 = SPRITES_DIR / f"{sid}_16x16.png"
        attempt_results = {"species_id": sid, "prompt": prompt, "attempts": []}

        success = False
        last_err = None
        for attempt in range(1, 3):
            try:
                print(f"[{sid}] attempt {attempt} — POST pixflux", flush=True)
                body, elapsed = call_pixflux(api_key, prompt)
                total_calls += 1
                img_data = decode_b64_image(body["image"]["base64"])
                if not verify_png(img_data):
                    raise RuntimeError("PNG signature invalid")
                out64.write_bytes(img_data)
                usage = body.get("usage", {})
                cost = float(usage.get("usd", 0.0)) if usage.get("type") == "usd" else 0.0
                total_usd += cost
                attempt_results["attempts"].append({
                    "attempt": attempt, "ok": True, "elapsed_s": round(elapsed, 2),
                    "usage": usage, "size_bytes": len(img_data),
                })
                print(f"  ok ({elapsed:.1f}s, ${cost}, {len(img_data)}B)", flush=True)
                # Downsample
                downsample_pixel_art(out64, out16, 16)
                print(f"  -> 16x16 saved: {out16.name}", flush=True)
                success = True
                break
            except Exception as e:
                last_err = str(e)
                attempt_results["attempts"].append({"attempt": attempt, "ok": False, "error": last_err})
                print(f"  fail: {last_err}", flush=True)
                time.sleep(2)

        attempt_results["success"] = success
        if not success:
            attempt_results["final_error"] = last_err
        results.append(attempt_results)
        time.sleep(1.5)

    summary = {
        "total_calls": total_calls,
        "total_usd": round(total_usd, 6),
        "species": results,
    }
    (ROOT / "scripts" / "generate_sprites_result.json").write_text(
        json.dumps(summary, indent=2, ensure_ascii=False)
    )
    print("\n=== SUMMARY ===")
    print(json.dumps(summary, indent=2, ensure_ascii=False))
    return 0 if all(r["success"] for r in results) else 1


if __name__ == "__main__":
    sys.exit(main())
