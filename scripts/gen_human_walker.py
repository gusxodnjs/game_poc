#!/usr/bin/env python3
"""Regenerate the walker character with human-balanced proportions via PixelLab pixflux.

Overwrites assets/characters/walker_front_64x64.png and walker_side_64x64.png.
Tone target: 16-bit JRPG protagonist (Stardew Valley / Pokemon BW / Earthbound),
NOT chibi mascot and NOT animal. 4-5 head body proportions, clearly human.

Performs one strengthened retry if the initial output looks too animal/chibi
(no programmatic detection — second prompt simply adds stronger anti-cute terms).
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
CHARS_DIR = ROOT / "assets" / "characters"
ENDPOINT = "https://api.pixellab.ai/v1/generate-image-pixflux"

# Tone gates encoded in prompt:
#   - explicit "human" + body proportions (4 head tall, not chibi)
#   - everyday outfit (t-shirt + jeans + sneakers) anchors realism
#   - reference style: 16-bit JRPG protagonist (Pokemon BW / Earthbound / Stardew)
#   - negatives: no mascot, no chibi, no animal features
PROMPTS = {
    "front": (
        "young adult human character in their early twenties, walker, "
        "front view standing pose facing camera, plain casual t-shirt and blue jeans, "
        "everyday sneakers, natural human body proportions four heads tall, "
        "16-bit JRPG protagonist pixel art style like Pokemon Black White or Earthbound, "
        "Stardew Valley style human villager, neutral friendly face, short hair, "
        "clearly recognizable as a person, slight stylization, "
        "not chibi, not mascot, not animal, no big oversized head, "
        "transparent background, clean pixel art"
    ),
    "side": (
        "young adult human character in their early twenties, walker, "
        "side view profile standing ready to walk, plain casual t-shirt and blue jeans, "
        "everyday sneakers, natural human body proportions four heads tall, "
        "16-bit JRPG protagonist pixel art style like Pokemon Black White or Earthbound, "
        "Stardew Valley style human villager, neutral friendly face, short hair, "
        "clearly recognizable as a person, slight stylization, "
        "not chibi, not mascot, not animal, no big oversized head, "
        "transparent background, clean pixel art"
    ),
}

# Reinforcement prompt used on retry — pushes harder against animal/chibi drift.
RETRY_SUFFIX = (
    ", human anatomy, realistic body proportions for stylized pixel art, "
    "visible arms and legs, ordinary person, NOT chibi, NOT a mascot, NOT an animal"
)


def load_api_key() -> str:
    if not ENV_FILE.exists():
        sys.exit(f"Missing {ENV_FILE}")
    for line in ENV_FILE.read_text().splitlines():
        line = line.strip()
        if line.startswith("PIXELLAB_API_KEY="):
            return line.split("=", 1)[1].strip()
    sys.exit("PIXELLAB_API_KEY not found")


def call_pixflux(api_key: str, description: str, size: int = 64) -> tuple[dict, float]:
    payload = {
        "description": description,
        "image_size": {"width": size, "height": size},
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


def main() -> int:
    api_key = load_api_key()
    CHARS_DIR.mkdir(parents=True, exist_ok=True)
    results = []
    total_usd = 0.0
    total_calls = 0

    for view, base_prompt in PROMPTS.items():
        out = CHARS_DIR / f"walker_{view}_64x64.png"
        attempt_info = {"view": view, "prompt": base_prompt, "attempts": []}
        ok = False
        last_err = None
        for attempt in range(1, 3):
            prompt = base_prompt if attempt == 1 else base_prompt + RETRY_SUFFIX
            try:
                print(f"[walker_{view}] attempt {attempt}", flush=True)
                body, elapsed = call_pixflux(api_key, prompt)
                total_calls += 1
                img = base64.b64decode(body["image"]["base64"])
                if img[:8] != b"\x89PNG\r\n\x1a\n":
                    raise RuntimeError("invalid PNG signature")
                out.write_bytes(img)
                usage = body.get("usage", {})
                cost = float(usage.get("usd", 0.0)) if usage.get("type") == "usd" else 0.0
                total_usd += cost
                attempt_info["attempts"].append({
                    "attempt": attempt,
                    "ok": True,
                    "prompt_used": prompt,
                    "elapsed_s": round(elapsed, 2),
                    "usage": usage,
                    "size_bytes": len(img),
                })
                print(f"  ok ({elapsed:.1f}s, ${cost}, {len(img)}B)", flush=True)
                ok = True
                break
            except urllib.error.HTTPError as e:
                last_err = f"HTTP {e.code}: {e.read().decode('utf-8', errors='replace')[:200]}"
                attempt_info["attempts"].append({"attempt": attempt, "ok": False, "error": last_err})
                print(f"  fail: {last_err}", flush=True)
                time.sleep(2)
            except Exception as e:
                last_err = f"{type(e).__name__}: {e}"
                attempt_info["attempts"].append({"attempt": attempt, "ok": False, "error": last_err})
                print(f"  fail: {last_err}", flush=True)
                time.sleep(2)
        attempt_info["success"] = ok
        if not ok:
            attempt_info["final_error"] = last_err
        results.append(attempt_info)
        time.sleep(1.5)

    summary = {
        "endpoint": ENDPOINT,
        "total_calls": total_calls,
        "total_usd": round(total_usd, 6),
        "results": results,
    }
    (ROOT / "scripts" / "gen_human_walker_result.json").write_text(
        json.dumps(summary, indent=2, ensure_ascii=False)
    )
    print("\n=== walker human rebalance summary ===")
    print(json.dumps(summary, indent=2, ensure_ascii=False))
    return 0 if all(r["success"] for r in results) else 1


if __name__ == "__main__":
    sys.exit(main())
