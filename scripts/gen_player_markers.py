#!/usr/bin/env python3
"""Generate player marker assets (shadow + GPS accuracy ring) via PixelLab pixflux.

Two assets:
  1) assets/characters/player_shadow_32x16.png
     - top-down soft elliptical shadow under walker's feet
     - PixelLab minimum canvas = 32x32, so we generate 32x32 then crop/resize to 32x16
  2) assets/characters/player_accuracy_ring_64x64.png
     - thin blue circle outline shown around walker when GPS accuracy is low
     - generated directly at 64x64

Tone target matches the existing walker sprites (16-bit JRPG, Stardew Valley palette).
Both assets must have transparent backgrounds for Unity sprite usage.

Validates PNG signature on download (PixelLab MCP has been known to leak
polling/error response bodies into the PNG slot — see memory pixellab_polling_quirk).
"""
import base64
import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow is required: pip3 install Pillow")

ROOT = Path(__file__).resolve().parent.parent
ENV_FILE = ROOT / ".env.local"
CHARS_DIR = ROOT / "assets" / "characters"
ENDPOINT = "https://api.pixellab.ai/v1/generate-image-pixflux"

PNG_SIG = b"\x89PNG\r\n\x1a\n"

# Jobs: (key, generated_w, generated_h, final_w, final_h, prompt, out_filename)
JOBS = [
    (
        "shadow",
        32, 32,           # PixelLab generation size (min 32)
        32, 16,           # final stored size
        "pixel art top-down soft circular shadow ellipse, "
        "semi-transparent dark gray, 32x16 pixels, "
        "no background, transparent background, "
        "subtle edge fade, isolated, simple flat shape, "
        "centered horizontal oval",
        "player_shadow_32x16.png",
    ),
    (
        "accuracy_ring",
        64, 64,
        64, 64,
        "pixel art thin blue circle outline, transparent center, "
        "light blue ring, 64x64 pixels, "
        "no background, transparent background, "
        "soft glow on edge, isolated, "
        "1 to 2 pixel thick stroke, perfect circle, hollow center, no fill",
        "player_accuracy_ring_64x64.png",
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
    with urllib.request.urlopen(req, timeout=180) as resp:
        body = json.loads(resp.read().decode("utf-8"))
    return body, time.time() - start


def postprocess(raw_png: bytes, gen_w: int, gen_h: int, final_w: int, final_h: int) -> bytes:
    """If generated dims differ from final, resize/crop preserving transparency.

    Strategy: nearest-neighbor resize to preserve hard pixel edges. For the
    shadow case (32x32 -> 32x16) this squashes the ellipse vertically which is
    actually desirable: we want a horizontally-stretched top-down shadow.
    """
    if (gen_w, gen_h) == (final_w, final_h):
        return raw_png
    from io import BytesIO
    img = Image.open(BytesIO(raw_png)).convert("RGBA")
    if img.size != (gen_w, gen_h):
        # API returned different size than requested — log but proceed
        print(f"    [warn] PNG decoded size {img.size} != requested ({gen_w},{gen_h})", flush=True)
    resized = img.resize((final_w, final_h), Image.Resampling.NEAREST)
    buf = BytesIO()
    resized.save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def main() -> int:
    api_key = load_api_key()
    CHARS_DIR.mkdir(parents=True, exist_ok=True)
    results = []
    total_usd = 0.0
    total_calls = 0

    for key, gen_w, gen_h, final_w, final_h, prompt, fname in JOBS:
        out_path = CHARS_DIR / fname
        info = {
            "key": key,
            "out": str(out_path.relative_to(ROOT)),
            "generated_size": f"{gen_w}x{gen_h}",
            "final_size": f"{final_w}x{final_h}",
            "prompt": prompt,
            "attempts": [],
        }
        ok = False
        last_err = None
        for attempt in range(1, 3):
            try:
                print(f"[{key}] attempt {attempt} ({gen_w}x{gen_h})", flush=True)
                body, elapsed = call_pixflux(api_key, prompt, gen_w, gen_h)
                total_calls += 1
                b64 = body["image"]["base64"]
                raw = base64.b64decode(b64)
                if not raw.startswith(PNG_SIG):
                    raise RuntimeError(f"invalid PNG signature (len={len(raw)})")
                final_bytes = postprocess(raw, gen_w, gen_h, final_w, final_h)
                # Re-verify signature after postprocess
                if not final_bytes.startswith(PNG_SIG):
                    raise RuntimeError("postprocess produced non-PNG")
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
                })
                print(f"  ok ({elapsed:.1f}s, ${cost}, raw={len(raw)}B, final={len(final_bytes)}B)", flush=True)
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
    out_json = ROOT / "scripts" / "gen_player_markers_result.json"
    out_json.write_text(json.dumps(summary, indent=2, ensure_ascii=False))
    print("\n=== player markers summary ===")
    print(json.dumps(summary, indent=2, ensure_ascii=False))
    return 0 if all(r["success"] for r in results) else 1


if __name__ == "__main__":
    sys.exit(main())
