#!/usr/bin/env python3
"""Selective retry for splash v2 bigbang frames that failed visual QA.

Initial run produced 12/12 technically valid 256x256 RGBA PNGs, but visual
inspection found:
  - f06: missing the "12-16 debris chunks" — output was just one small dot.
  - f07: rendered as a fully formed grey sphere — too close to f10/f11 state,
         not the "debris closer to center, halo just forming" middle stage.
  - f08: model burned a watermark-like text glyph "Pi_E10G7" into bottom-right.
  - f11: model burned a watermark-like text glyph "FULERTE GR4PER" into bottom.

Style header is reinforced with explicit anti-text directives. Each retry
prompt also leans harder into the specific compositional issue (e.g. "many
small scattered chunks, not a single object" for f06).

Pattern follows gen_splash_v2_bigbang.py — REST direct to pixflux, PNG sig
check, 3 attempts, log to gen_splash_v2_bigbang_retry_result.json.
"""
import base64
import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ENV_FILE = ROOT / ".env.local"
OUT_DIR = ROOT / "Assets" / "AppIcon"
ENDPOINT = "https://api.pixellab.ai/v1/generate-image-pixflux"
SIZE = 256
PNG_SIG = b"\x89PNG\r\n\x1a\n"
MIN_BYTES = 1024

# Strengthened style header: extra anti-text directives (model burned text into
# f08 and f11 on the first pass despite the original "no text, no UI, no logo"
# directives — adding more aggressive negatives here).
STYLE_HEADER = (
    "pixel art, 256x256, dark cosmic background #080d1f, "
    "top-down centered composition, "
    "absolutely no text, no letters, no numbers, no watermark, "
    "no signature, no logo, no UI, no captions, no labels, "
    "limited palette, crisp pixel edges, no anti-aliasing"
)

# Retry prompts — only the frames that failed visual QA.
# Keys are frame indices; values are the stage-specific keyword prompts that
# will be combined with the strengthened STYLE_HEADER.
RETRY_PROMPTS = {
    7: (  # f07 — second retry; first retry gave a Saturn-like planet with text watermark.
        # Push hard against "any planet" and reinforce "scattered small chunks".
        "scene shows roughly 14 small separate irregular rocky debris chunks scattered "
        "across the canvas, each chunk only 4 to 8 pixels wide, in muted brown #5C4A3A "
        "and dark grey #4a4a52, arranged in a loose ring at 40 to 60 percent of canvas "
        "radius from the center, every chunk is a distinct individual piece NOT touching "
        "or merging with any other, a tiny faint circular halo glow only 30 pixels wide "
        "at exact center in dim grey-brown #3a3028, short motion-trail streaks behind "
        "each chunk pointing inward toward the center, "
        "ABSOLUTELY NO planet, NO sphere, NO rings around any object, NO Saturn, "
        "NO completed celestial body, the central area is mostly empty space with only "
        "the faint halo glow, debris pre-accretion state, "
        "no text, no letters, no numbers, no watermark, no signature anywhere"
    ),
    8: (  # f08 — had watermark text "Pi_E10G7"; reinforce no-text + specific shape
        "scene shows an irregular lumpy proto-mass at center about 50 pixels wide "
        "forming from merged debris, rough uneven silhouette NOT a perfect sphere, "
        "dark brown #4a3a2c surface with bright hot orange #d05030 magma cracks "
        "across it, glowing accretion rim, a few small remaining debris chunks still "
        "drifting inward at 30 percent radius from the proto-mass, dark cosmic "
        "background, absolutely no text, no letters, no numbers, no watermark anywhere"
    ),
    11: (  # f11 — had watermark text "FULERTE GR4PER"; reinforce no-text
        "scene shows a final primitive desolate planet at exact center about 160 "
        "pixels in diameter, base crust in grey-brown #5C4A3A, dark crack veins "
        "#2E2520 across the surface, deep crater shadows in #1A1410, no magma glow "
        "no atmosphere no clouds, subtle stone highlight #A89888 on the upper-left "
        "side hinting at distant starlight, dead silent static surface, "
        "scattered 18 background stars at low alpha across the dark cosmic backdrop, "
        "completely empty space around the planet, "
        "absolutely no text, no letters, no numbers, no watermark, no signature, "
        "no captions, no labels anywhere in the image"
    ),
}


def load_api_key() -> str:
    if not ENV_FILE.exists():
        sys.exit(f"Missing {ENV_FILE}")
    for line in ENV_FILE.read_text().splitlines():
        line = line.strip()
        if line.startswith("PIXELLAB_API_KEY="):
            return line.split("=", 1)[1].strip()
    sys.exit("PIXELLAB_API_KEY not found in .env.local")


def call_pixflux(api_key: str, description: str) -> tuple[dict, float]:
    payload = {
        "description": description,
        "image_size": {"width": SIZE, "height": SIZE},
        "no_background": False,
        "text_guidance_scale": 12.0,  # slightly higher to push the anti-text directives
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


def generate_frame(api_key: str, idx: int, base_prompt: str) -> dict:
    out_path = OUT_DIR / f"splash_anim_v2_bigbang_256_f{idx:02d}.png"
    prompt = f"{STYLE_HEADER}, {base_prompt}"
    info = {
        "index": idx,
        "file": str(out_path.relative_to(ROOT)),
        "prompt": prompt,
        "attempts": [],
        "success": False,
    }
    last_err = None
    for attempt in range(1, 4):
        try:
            print(f"[f{idx:02d}] retry attempt {attempt} ...", flush=True)
            body, elapsed = call_pixflux(api_key, prompt)
            img = base64.b64decode(body["image"]["base64"])
            if not img.startswith(PNG_SIG):
                raise RuntimeError(f"invalid PNG signature (len={len(img)})")
            if len(img) < MIN_BYTES:
                raise RuntimeError(f"too small: {len(img)}B < {MIN_BYTES}B")
            out_path.write_bytes(img)
            usage = body.get("usage", {})
            cost = float(usage.get("usd", 0.0)) if usage.get("type") == "usd" else 0.0
            info["attempts"].append({
                "attempt": attempt, "ok": True,
                "elapsed_s": round(elapsed, 2),
                "usage": usage, "size_bytes": len(img),
            })
            info["success"] = True
            info["final_size_bytes"] = len(img)
            info["final_usd"] = cost
            print(f"  ok ({elapsed:.1f}s, ${cost}, {len(img)}B)", flush=True)
            return info
        except urllib.error.HTTPError as e:
            last_err = f"HTTP {e.code}: {e.read().decode('utf-8', errors='replace')[:300]}"
        except Exception as e:
            last_err = f"{type(e).__name__}: {e}"
        info["attempts"].append({"attempt": attempt, "ok": False, "error": last_err})
        print(f"  fail: {last_err}", flush=True)
        time.sleep(3)
    info["final_error"] = last_err
    return info


def main() -> int:
    if not OUT_DIR.exists():
        sys.exit(f"output dir not found: {OUT_DIR}")
    api_key = load_api_key()
    results = []
    total_calls = 0
    total_usd = 0.0
    for idx, prompt in RETRY_PROMPTS.items():
        info = generate_frame(api_key, idx, prompt)
        results.append(info)
        total_calls += len(info["attempts"])
        if info["success"]:
            total_usd += info.get("final_usd", 0.0)
        time.sleep(1.5)
    summary = {
        "endpoint": ENDPOINT,
        "size": SIZE,
        "retry_frame_count": len(RETRY_PROMPTS),
        "total_calls": total_calls,
        "total_usd": round(total_usd, 6),
        "results": results,
    }
    (ROOT / "scripts" / "gen_splash_v2_bigbang_retry_result.json").write_text(
        json.dumps(summary, indent=2, ensure_ascii=False)
    )
    ok = sum(1 for r in results if r["success"])
    print(f"\nretries ok: {ok}/{len(RETRY_PROMPTS)}  total_usd=${summary['total_usd']}")
    return 0 if ok == len(RETRY_PROMPTS) else 1


if __name__ == "__main__":
    sys.exit(main())
