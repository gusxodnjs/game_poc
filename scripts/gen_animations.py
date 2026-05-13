#!/usr/bin/env python3
"""Generate walking / flying animations via PixelLab.

Strategy:
  - Use /animate-with-text (skeleton-free, works on chibi sprites without manual rigging)
  - 4 frames per cycle (the model always emits 4)
  - For each subject we save:
      * per-frame PNGs: <subject>_anim_frame{0..3}_64x64.png
      * a single horizontal sheet: <subject>_anim_sheet_64x64.png  (256x64)
  - Walker side walk also saved as walker_side_walk_frame{N}_64x64.png + sheet

Subjects:
  1. walker side walk          (assets/characters/walker_side_64x64.png)
  2. walker front idle bob     (assets/characters/walker_front_64x64.png)
  3. ladybug top-down walk     (assets/sprites/ladybug_64x64.png)
  4. honeybee side wing flap   (assets/sprites/honeybee_64x64.png)

Retry policy: one retry on failure per subject. Stops if cumulative USD >= 5.00.
"""
import base64
import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ENV_FILE = ROOT / ".env.local"
CHARS_DIR = ROOT / "assets" / "characters"
SPRITES_DIR = ROOT / "assets" / "sprites"
ENDPOINT = "https://api.pixellab.ai/v1/animate-with-text"

COST_LIMIT_USD = 5.00


def load_api_key() -> str:
    if not ENV_FILE.exists():
        sys.exit(f"Missing {ENV_FILE}")
    for line in ENV_FILE.read_text().splitlines():
        line = line.strip()
        if line.startswith("PIXELLAB_API_KEY="):
            return line.split("=", 1)[1].strip()
    sys.exit("PIXELLAB_API_KEY not found")


def load_ref_b64(path: Path) -> str:
    return base64.b64encode(path.read_bytes()).decode("ascii")


def call_animate_text(
    api_key: str,
    description: str,
    action: str,
    reference_b64: str,
    view: str = "side",
    direction: str = "east",
    n_frames: int = 4,
    size: int = 64,
) -> tuple[dict, float]:
    payload = {
        "description": description,
        "action": action,
        "reference_image": {"type": "base64", "base64": reference_b64},
        "image_size": {"width": size, "height": size},
        "view": view,
        "direction": direction,
        "n_frames": n_frames,
        "text_guidance_scale": 8.0,
        "image_guidance_scale": 1.5,
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
    with urllib.request.urlopen(req, timeout=300) as resp:
        body = json.loads(resp.read().decode("utf-8"))
    return body, time.time() - start


def write_frames_and_sheet(
    images_b64: list[str],
    out_dir: Path,
    base_name: str,
    size: int = 64,
) -> tuple[list[Path], Path]:
    out_dir.mkdir(parents=True, exist_ok=True)
    frame_paths: list[Path] = []
    pil_frames = []
    for i, b64 in enumerate(images_b64):
        raw = base64.b64decode(b64)
        if raw[:8] != b"\x89PNG\r\n\x1a\n":
            raise RuntimeError(f"frame {i}: invalid PNG signature")
        frame_path = out_dir / f"{base_name}_frame{i}_64x64.png"
        frame_path.write_bytes(raw)
        frame_paths.append(frame_path)
        pil_frames.append(Image.open(frame_path).convert("RGBA"))
    # build horizontal sheet (N x size)  width = N*size  height = size
    sheet = Image.new("RGBA", (size * len(pil_frames), size), (0, 0, 0, 0))
    for i, frame in enumerate(pil_frames):
        sheet.paste(frame, (i * size, 0), frame)
    sheet_path = out_dir / f"{base_name}_sheet_64x64.png"
    sheet.save(sheet_path, "PNG", optimize=True)
    return frame_paths, sheet_path


SUBJECTS = [
    {
        "key": "walker_side_walk",
        "ref": CHARS_DIR / "walker_side_64x64.png",
        "out_dir": CHARS_DIR,
        "base_name": "walker_side_walk",
        "description": "super cute adorable chibi walker character mascot, big head, large sparkling eyes, tiny body, soft pastel casual outfit, kawaii pixel art",
        "action": "walking cycle",
        "view": "side",
        "direction": "east",
    },
    {
        "key": "walker_front_idle",
        "ref": CHARS_DIR / "walker_front_64x64.png",
        "out_dir": CHARS_DIR,
        "base_name": "walker_front_idle",
        "description": "super cute adorable chibi walker character mascot, big head, large sparkling eyes, tiny body, soft pastel casual outfit, kawaii pixel art",
        "action": "gentle idle bob, slight head bounce",
        "view": "side",  # animate-with-text doesn't have explicit front; we keep view=side per schema enum
        "direction": "south",
    },
    {
        "key": "ladybug_walk",
        "ref": SPRITES_DIR / "ladybug_64x64.png",
        "out_dir": SPRITES_DIR,
        "base_name": "ladybug_anim",
        "description": "red ladybug with black spots, pixel art",
        "action": "walking cycle, legs moving",
        "view": "side",
        "direction": "east",
    },
    {
        "key": "honeybee_flap",
        "ref": SPRITES_DIR / "honeybee_64x64.png",
        "out_dir": SPRITES_DIR,
        "base_name": "honeybee_anim",
        "description": "yellow and black honeybee with wings, pixel art",
        "action": "flying in place, rapid wing flap",
        "view": "side",
        "direction": "east",
    },
]


def main() -> int:
    api_key = load_api_key()
    results = []
    total_usd = 0.0
    total_calls = 0

    for sub in SUBJECTS:
        if total_usd >= COST_LIMIT_USD:
            print(f"[stop] cost limit reached: ${total_usd:.4f} >= ${COST_LIMIT_USD}", flush=True)
            results.append({"key": sub["key"], "skipped": True, "reason": "cost_limit"})
            continue
        if not sub["ref"].exists():
            print(f"[skip] missing reference: {sub['ref']}", flush=True)
            results.append({"key": sub["key"], "skipped": True, "reason": "missing_reference"})
            continue
        ref_b64 = load_ref_b64(sub["ref"])
        info = {
            "key": sub["key"],
            "reference": str(sub["ref"].relative_to(ROOT)),
            "description": sub["description"],
            "action": sub["action"],
            "view": sub["view"],
            "direction": sub["direction"],
            "attempts": [],
        }
        ok = False
        last_err = None
        for attempt in range(1, 3):
            try:
                print(f"[{sub['key']}] attempt {attempt} POST /animate-with-text", flush=True)
                body, elapsed = call_animate_text(
                    api_key,
                    description=sub["description"],
                    action=sub["action"],
                    reference_b64=ref_b64,
                    view=sub["view"],
                    direction=sub["direction"],
                )
                total_calls += 1
                imgs = body.get("images", [])
                images_b64 = [img["base64"] for img in imgs if img and img.get("base64")]
                if len(images_b64) < 2:
                    raise RuntimeError(f"only {len(images_b64)} frames returned")
                frame_paths, sheet_path = write_frames_and_sheet(
                    images_b64, sub["out_dir"], sub["base_name"]
                )
                usage = body.get("usage", {})
                cost = float(usage.get("usd", 0.0)) if usage.get("type") == "usd" else 0.0
                total_usd += cost
                info["attempts"].append({
                    "attempt": attempt,
                    "ok": True,
                    "elapsed_s": round(elapsed, 2),
                    "usage": usage,
                    "n_frames": len(images_b64),
                    "frames": [str(p.relative_to(ROOT)) for p in frame_paths],
                    "sheet": str(sheet_path.relative_to(ROOT)),
                })
                print(
                    f"  ok {len(images_b64)} frames "
                    f"({elapsed:.1f}s, ${cost})  sheet={sheet_path.name}",
                    flush=True,
                )
                ok = True
                break
            except urllib.error.HTTPError as e:
                body = e.read().decode("utf-8", errors="replace")[:400]
                last_err = f"HTTP {e.code}: {body}"
                info["attempts"].append({"attempt": attempt, "ok": False, "error": last_err})
                print(f"  fail: {last_err}", flush=True)
                time.sleep(3)
            except Exception as e:
                last_err = f"{type(e).__name__}: {e}"
                info["attempts"].append({"attempt": attempt, "ok": False, "error": last_err})
                print(f"  fail: {last_err}", flush=True)
                time.sleep(3)
        info["success"] = ok
        if not ok:
            info["final_error"] = last_err
        results.append(info)
        time.sleep(2)

    summary = {
        "endpoint": ENDPOINT,
        "total_calls": total_calls,
        "total_usd": round(total_usd, 6),
        "subjects": results,
    }
    (ROOT / "scripts" / "gen_animations_result.json").write_text(
        json.dumps(summary, indent=2, ensure_ascii=False)
    )
    print("\n=== animation summary ===")
    print(json.dumps(summary, indent=2, ensure_ascii=False))
    return 0 if all(r.get("success") for r in results if not r.get("skipped")) else 1


if __name__ == "__main__":
    sys.exit(main())
