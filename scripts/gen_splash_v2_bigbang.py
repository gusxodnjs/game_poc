#!/usr/bin/env python3
"""Generate the splash v2 big-bang 12-frame sequence via PixelLab pixflux REST API.

Source of truth: docs/splash_v2_bigbang.md (sec.2 frame table).
Output: Assets/AppIcon/splash_anim_v2_bigbang_256_f00.png .. f11.png (256x256 RGBA).

This script follows the project's REST-direct pattern (no MCP — see memory
`pixellab-generation-pattern` / `pixellab-polling-quirk`):
  1. POST https://api.pixellab.ai/v1/generate-image-pixflux with prompt + 256x256
  2. base64-decode response.image.base64 -> PNG bytes
  3. Verify PNG signature `\x89PNG\r\n\x1a\n` before writing (avoid 70B JSON-as-PNG bug)
  4. Verify file size >= 1KB after writing; retry up to 2 times if invalid
  5. Log all attempts + total usd cost to scripts/gen_splash_v2_bigbang_result.json

Each frame's prompt is the per-frame keywords from docs/splash_v2_bigbang.md,
prefixed with the shared style header so all 12 frames share identical
palette / composition / negative directives.

The sequence is a single-play, non-looping animation: empty void -> bigbang ->
debris reconvergence -> primitive desolate planet (f11 hold). f11 stays on
screen as the title/subcopy/start-button fade-in baseplate.
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
MIN_BYTES = 1024  # < 1KB is treated as suspicious per pixellab-polling-quirk memory

# Shared style header applied to every frame (sec.2 of the storyboard).
# `no_background: False` is intentional: this is a full-bleed cosmic scene; we
# want the dark cosmic background baked into the PNG, NOT transparent.
STYLE_HEADER = (
    "pixel art, 256x256, dark cosmic background #080d1f, "
    "top-down centered composition, no text, no UI, no logo, "
    "limited palette, crisp pixel edges, no anti-aliasing"
)

# Per-frame keyword prompts (transcribed verbatim from docs/splash_v2_bigbang.md
# sec.2 table, last column). Each will be prefixed with STYLE_HEADER + ", ".
FRAME_PROMPTS = [
    # f00 - 무 (어둠)
    "empty deep cosmic void, scattered 18 tiny faint stars in muted white #c8d2e0 "
    "at alpha 30%, absolute stillness, no central object, balanced negative space",
    # f01 - 작은 점
    "single 2px pure white #ffffff pinpoint of light at exact center, "
    "faint 4px soft halo glow, background stars unchanged from f00, "
    "sense of awakening singularity",
    # f02 - 임계 광원
    "swollen 8px bright white core at center with pale yellow #fff4c2 inner ring, "
    "surrounding stars stretched into thin radial streaks pulled toward center, "
    "accretion-pull tension, pre-explosion build-up",
    # f03 - 폭발 1 (코어)
    "supernova flash, intense 60px solid white #ffffff core, surrounding 96px "
    "concentric ring in saturated yellow #ffd755, hard pixel edges, "
    "first burst of light, stars washed out by overexposure",
    # f04 - 폭발 2 (확산)
    "expanding shockwave ring at 200px diameter, gradient from inner orange "
    "#ff8a3a to outer deep red #c0341a, white core dimmed to 80px, "
    "ring edge crisp pixel-by-pixel, debris particles starting to detach from ring",
    # f05 - 폭발 3 (잔광)
    "full-screen faint red-orange afterglow at #5a2418 low alpha, central core "
    "faded to 40px dull amber #a86030, dozens of small debris pixel dots "
    "scattering outward in all directions, post-explosion stillness creeping in",
    # f06 - 파편 비산
    "12 to 16 small irregular debris chunks in muted brown #5C4A3A and grey "
    "#4a4a52, scattered across the canvas at outer 60-80% radius, central area "
    "nearly empty with only faint embers, afterglow fading to background black, "
    "sense of cooling aftermath",
    # f07 - 응집 1 (먼 끌림)
    "same debris chunks as previous frame but now repositioned closer to center "
    "at 40-60% radius, faint circular gravitational halo glow at center in dim "
    "grey-brown #3a3028, subtle motion-trail hint behind each chunk pointing "
    "inward, accretion beginning",
    # f08 - 응집 2 (중간)
    "irregular 50px proto-mass at center forming from merged debris, rough "
    "lumpy silhouette in dark brown #4a3a2c with hot orange #d05030 magma "
    "cracks, a few remaining debris chunks still drifting inward at 30% radius, "
    "hot accretion glow rim",
    # f09 - 응집 3 (성형)
    "roughly spherical 100px primitive planetoid at center, surface still hot "
    "with bright magma cracks in orange-red #e05a28 across darker grey-brown "
    "crust #5C4A3A, faint outer heat haze, debris fully absorbed, "
    "planet not yet cooled",
    # f10 - 식어가는 행성
    "140px primitive planet at center, magma cracks dimmed from orange to deep "
    "ember #7a2818, crust dominant in grey-brown #5C4A3A with dark crack veins "
    "#2E2520, cooling surface, first crater shadows appearing, heat haze gone, "
    "background stars returning at low alpha",
    # f11 - 황폐 행성 (최종)
    "final 160px primitive desolate planet at center, base grey-brown crust "
    "#5C4A3A with dark crack veins #2E2520, deep crater shadows #1A1410, "
    "no magma glow, no atmosphere, subtle stone highlight #A89888 on one side "
    "hinting at distant starlight, dead silent surface, 18 background stars "
    "fully restored at original alpha, completely static and held",
]
assert len(FRAME_PROMPTS) == 12, "expected exactly 12 frames"


def load_api_key() -> str:
    if not ENV_FILE.exists():
        sys.exit(f"Missing {ENV_FILE}")
    for line in ENV_FILE.read_text().splitlines():
        line = line.strip()
        if line.startswith("PIXELLAB_API_KEY="):
            return line.split("=", 1)[1].strip()
    sys.exit("PIXELLAB_API_KEY not found in .env.local")


def call_pixflux(api_key: str, description: str) -> tuple[dict, float]:
    """POST to pixflux endpoint. Returns (response_json, elapsed_seconds)."""
    payload = {
        "description": description,
        "image_size": {"width": SIZE, "height": SIZE},
        # IMPORTANT: keep background baked in. This is a full-bleed cosmic scene,
        # not a transparent sprite. The storyboard relies on the dark cosmic
        # color #080d1f being part of every frame.
        "no_background": False,
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


def generate_frame(api_key: str, idx: int, base_prompt: str) -> dict:
    """Generate one frame with up to 3 attempts. Returns attempt log dict."""
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
            print(f"[f{idx:02d}] attempt {attempt} ...", flush=True)
            body, elapsed = call_pixflux(api_key, prompt)
            img = base64.b64decode(body["image"]["base64"])
            if not img.startswith(PNG_SIG):
                raise RuntimeError(
                    f"invalid PNG signature (len={len(img)}, head={img[:16]!r})"
                )
            if len(img) < MIN_BYTES:
                raise RuntimeError(f"suspiciously small file: {len(img)}B < {MIN_BYTES}B")
            out_path.write_bytes(img)
            usage = body.get("usage", {})
            cost = float(usage.get("usd", 0.0)) if usage.get("type") == "usd" else 0.0
            info["attempts"].append({
                "attempt": attempt,
                "ok": True,
                "elapsed_s": round(elapsed, 2),
                "usage": usage,
                "size_bytes": len(img),
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
    total_usd = 0.0
    total_calls = 0

    for idx, base_prompt in enumerate(FRAME_PROMPTS):
        info = generate_frame(api_key, idx, base_prompt)
        results.append(info)
        total_calls += len(info["attempts"])
        if info["success"]:
            total_usd += info.get("final_usd", 0.0)
        # gentle pacing between calls
        time.sleep(1.5)

    summary = {
        "endpoint": ENDPOINT,
        "size": SIZE,
        "frame_count": len(FRAME_PROMPTS),
        "total_calls": total_calls,
        "total_usd": round(total_usd, 6),
        "results": results,
    }
    log_path = ROOT / "scripts" / "gen_splash_v2_bigbang_result.json"
    log_path.write_text(json.dumps(summary, indent=2, ensure_ascii=False))
    print("\n=== splash v2 bigbang summary ===")
    print(f"total_calls={total_calls}  total_usd=${summary['total_usd']}")
    ok = sum(1 for r in results if r["success"])
    print(f"frames ok: {ok}/12")
    for r in results:
        status = "OK " if r["success"] else "FAIL"
        sz = r.get("final_size_bytes", "-")
        print(f"  {status} f{r['index']:02d}  {sz}B  {r['file']}")
    return 0 if ok == 12 else 1


if __name__ == "__main__":
    sys.exit(main())
