#!/usr/bin/env python3
"""Generate 3 planet card thumbnails (256x256) via PixelLab pixflux.

PlanetIntroScene displays one of these as a single SpriteRenderer card.
All three must read as the SAME primordial / barren visual era — no
buildings, no developed flora, no oases. Tone guard is enforced in the
prompt (negative-style descriptors) and in the post-generation log.

Concepts (locked, scenario v1/v2):
  1) volcano — cooled black lava + thin white steam + faint reddish-brown embers
  2) ice    — pale blue-gray frozen plain + silence + faint wind streaks
  3) desert — ochre cracked dry earth + sand plains + dry wind

Validates PNG signature on download (PixelLab MCP has been known to leak
polling/error response bodies into the PNG slot — see memory
pixellab_polling_quirk).
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
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: pip3 install Pillow")

ROOT = Path(__file__).resolve().parent.parent
ENV_FILE = ROOT / ".env.local"
WORLD_DIR = ROOT / "assets" / "world"
ENDPOINT = "https://api.pixellab.ai/v1/generate-image-pixflux"

PNG_SIG = b"\x89PNG\r\n\x1a\n"

# Jobs: (key, width, height, prompt, out_filename)
#
# All three cards must read as the SAME visual format: a planet SPHERE (round
# globe) centered on a transparent background, showing primordial barren
# surface details. This matches the existing `planet_grey/green_128.png`
# globe convention and the volcano output that was approved on first attempt.
JOBS = [
    (
        "volcano",
        256, 256,
        (
            "pixel art round planet sphere, primitive volcanic world, "
            "surface texture of cooled black lava with cracks glowing faintly red, "
            "thin wisps of white steam, dark gray and reddish-brown tones #5C3B33, "
            "isolated round planet centered on transparent background, "
            "no buildings no vegetation no people no creatures, "
            "primordial wasteland, soft pixel shading, simple sphere shading"
        ),
        "planet_card_volcano_256.png",
    ),
    (
        "ice",
        256, 256,
        (
            "pixel art round planet sphere, primitive frozen world, "
            "surface texture of pale blue-gray ice plains with faint cracks and wind streaks, "
            "deep silence, cold tones #6E8AA0, "
            "isolated round planet centered on transparent background, "
            "no buildings no vegetation no people no creatures, "
            "primordial frozen wasteland, soft pixel shading, simple sphere shading"
        ),
        "planet_card_ice_256.png",
    ),
    (
        "desert",
        256, 256,
        (
            "pixel art round planet sphere, primitive desert world, "
            "surface texture of ochre cracked dry earth and endless sand plains, "
            "warm sun, yellow-brown tones #B89968, "
            "isolated round planet centered on transparent background, "
            "no buildings no vegetation no palm trees no oasis no people no creatures, "
            "primordial barren wasteland, soft pixel shading, simple sphere shading"
        ),
        "planet_card_desert_256.png",
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


def alpha_clip_background(raw_png: bytes, tol: int = 12) -> tuple[bytes, dict]:
    """Force transparent background by alpha-clipping the corner color.

    PixelLab's `no_background: true` flag does not always produce real
    transparency — sometimes the model returns a solid pale background.
    We sample the four corner pixels (which should always be background),
    pick the most common corner color, then make any pixel within `tol`
    Manhattan distance fully transparent.

    Returns (new_png_bytes, info_dict).
    """
    img = Image.open(BytesIO(raw_png)).convert("RGBA")
    w, h = img.size
    corners = [
        img.getpixel((0, 0)),
        img.getpixel((w - 1, 0)),
        img.getpixel((0, h - 1)),
        img.getpixel((w - 1, h - 1)),
    ]
    # Pick the corner color that appears most often
    from collections import Counter
    bg_rgb = Counter([c[:3] for c in corners]).most_common(1)[0][0]
    br, bg_, bb = bg_rgb
    pixels = list(img.getdata())
    cleared = 0
    new_pixels = []
    for r, g, b, a in pixels:
        if a == 0:
            new_pixels.append((r, g, b, 0))
            continue
        if abs(r - br) + abs(g - bg_) + abs(b - bb) <= tol:
            new_pixels.append((r, g, b, 0))
            cleared += 1
        else:
            new_pixels.append((r, g, b, a))
    img.putdata(new_pixels)
    buf = BytesIO()
    img.save(buf, format="PNG", optimize=True)
    return buf.getvalue(), {
        "bg_rgb": list(bg_rgb),
        "cleared_pixels": cleared,
        "total_pixels": w * h,
        "cleared_pct": round(100 * cleared / (w * h), 2),
    }


def call_pixflux(api_key: str, prompt: str, width: int, height: int) -> tuple[dict, float]:
    payload = {
        "description": prompt,
        "image_size": {"width": width, "height": height},
        "no_background": True,
        "text_guidance_scale": 10.0,
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
    with urllib.request.urlopen(req, timeout=240) as resp:
        body = json.loads(resp.read().decode("utf-8"))
    return body, time.time() - start


def main() -> int:
    api_key = load_api_key()
    WORLD_DIR.mkdir(parents=True, exist_ok=True)
    results = []
    total_usd = 0.0
    total_calls = 0

    for key, w, h, prompt, fname in JOBS:
        out_path = WORLD_DIR / fname
        info = {
            "key": key,
            "out": str(out_path.relative_to(ROOT)),
            "size": f"{w}x{h}",
            "prompt": prompt,
            "attempts": [],
        }
        ok = False
        last_err = None
        for attempt in range(1, 3):
            try:
                print(f"[{key}] attempt {attempt} ({w}x{h})", flush=True)
                body, elapsed = call_pixflux(api_key, prompt, w, h)
                total_calls += 1
                b64 = body["image"]["base64"]
                raw = base64.b64decode(b64)
                if not raw.startswith(PNG_SIG):
                    raise RuntimeError(f"invalid PNG signature (len={len(raw)})")
                final_bytes, clip_info = alpha_clip_background(raw)
                if not final_bytes.startswith(PNG_SIG):
                    raise RuntimeError("alpha_clip produced non-PNG")
                out_path.write_bytes(final_bytes)
                usage = body.get("usage", {})
                cost = float(usage.get("usd", 0.0)) if usage.get("type") == "usd" else 0.0
                total_usd += cost
                info["attempts"].append({
                    "attempt": attempt,
                    "ok": True,
                    "elapsed_s": round(elapsed, 2),
                    "usage": usage,
                    "raw_bytes": len(raw),
                    "final_bytes": len(final_bytes),
                    "alpha_clip": clip_info,
                })
                print(f"  ok ({elapsed:.1f}s, ${cost}, raw={len(raw)}B, final={len(final_bytes)}B, bg_cleared={clip_info['cleared_pct']}%)", flush=True)
                ok = True
                break
            except urllib.error.HTTPError as e:
                last_err = f"HTTP {e.code}: {e.read().decode('utf-8', errors='replace')[:200]}"
                info["attempts"].append({"attempt": attempt, "ok": False, "error": last_err})
                print(f"  fail: {last_err}", flush=True)
                time.sleep(2)
            except Exception as e:
                last_err = f"{type(e).__name__}: {e}"
                info["attempts"].append({"attempt": attempt, "ok": False, "error": last_err})
                print(f"  fail: {last_err}", flush=True)
                time.sleep(2)
        info["success"] = ok
        if not ok:
            info["final_error"] = last_err
        results.append(info)
        time.sleep(1.0)

    summary = {
        "endpoint": ENDPOINT,
        "total_calls": total_calls,
        "total_usd": round(total_usd, 6),
        "results": results,
    }
    out_json = ROOT / "scripts" / "gen_planet_cards_result.json"
    out_json.write_text(json.dumps(summary, indent=2, ensure_ascii=False))
    print("\n=== planet cards summary ===")
    print(json.dumps(summary, indent=2, ensure_ascii=False))
    return 0 if all(r["success"] for r in results) else 1


if __name__ == "__main__":
    sys.exit(main())
