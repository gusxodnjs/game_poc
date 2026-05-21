#!/usr/bin/env python3
"""Regenerate splash bigbang 12-frame sequence with TRANSPARENT background (v3).

Spec (SSOT): docs/superpowers/specs/2026-05-21-splash-merge-design.md §3
Frame meanings (timing / composition): docs/splash_v2_bigbang.md §2

Output: Assets/AppIcon/splash_anim_v2_bigbang_256_f00.png .. f11.png
(overwrites v2; .meta GUIDs are preserved by leaving .meta files untouched.)

Differences from gen_splash_v2_bigbang.py:
  - STYLE_HEADER drops the dark cosmic background + stars and asks for
    `transparent background`.
  - Per-frame prompts strip references to background stars / starfield /
    overexposure-washed stars (per design doc §3.3).
  - Payload sets `no_background: True` so PixelLab outputs RGBA with alpha
    on the empty regions.

Pattern (kept identical to v2 script):
  1. POST https://api.pixellab.ai/v1/generate-image-pixflux
  2. base64-decode response.image.base64 -> PNG bytes
  3. Verify PNG signature + file size >= 1KB
     (memory: pixellab-polling-quirk)
  4. Up to 3 attempts per frame
  5. Post-write corner-alpha check (4 corners + 4 edge midpoints):
     all <=32 means transparent background actually landed
  6. PixelLab no_background quirk fallback: if corner alpha is solid (255),
     auto-clip the corner color across the image
     (memory: pixellab-no-background-quirk).
"""
import base64
import json
import sys
import time
import urllib.error
import urllib.request
import zlib
from collections import Counter
from io import BytesIO
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ENV_FILE = ROOT / ".env.local"
OUT_DIR = ROOT / "Assets" / "AppIcon"
ENDPOINT = "https://api.pixellab.ai/v1/generate-image-pixflux"
SIZE = 256
PNG_SIG = b"\x89PNG\r\n\x1a\n"
MIN_BYTES = 1024
CORNER_ALPHA_MAX = 32

STYLE_HEADER = (
    "pixel art, 256x256, transparent background, no background fill, "
    "no stars, no cosmos, no starfield, "
    "top-down centered composition, no text, no UI, no logo, "
    "limited palette, crisp pixel edges, no anti-aliasing, "
    "alpha channel only for object content"
)

FRAME_PROMPTS = [
    # f00 - 무 (어둠) — completely empty alpha-clear frame
    "completely empty transparent canvas, no objects, no light sources, "
    "absolute stillness, balanced negative space, sequence start placeholder",
    # f01 - 작은 점
    "single 2px pure white #ffffff pinpoint of light at exact center, "
    "faint 4px soft halo glow, otherwise empty transparent canvas, "
    "sense of awakening singularity",
    # f02 - 임계 광원 (PM boost: dust/matter streaks, no point lights)
    "swollen 8px bright white core at center with pale yellow #fff4c2 inner ring, "
    "surrounding dust and matter streaks pulled toward center, "
    "no point lights in streaks, accretion-pull tension, pre-explosion build-up",
    # f03 - 폭발 1 (코어)
    "supernova flash, intense 60px solid white #ffffff core, surrounding 96px "
    "concentric ring in saturated yellow #ffd755, hard pixel edges, "
    "first burst of light, bright flash dominates the frame",
    # f04 - 폭발 2 (확산)
    "expanding shockwave ring at 200px diameter, gradient from inner orange "
    "#ff8a3a to outer deep red #c0341a, white core dimmed to 80px, "
    "ring edge crisp pixel-by-pixel, debris particles starting to detach from ring",
    # f05 - 폭발 3 (잔광)
    "faint red-orange afterglow at #5a2418 low alpha around the explosion site, "
    "central core faded to 40px dull amber #a86030, dozens of small debris pixel "
    "dots scattering outward in all directions, post-explosion stillness creeping in",
    # f06 - 파편 비산
    "12 to 16 small irregular debris chunks in muted brown #5C4A3A and grey "
    "#4a4a52, scattered across the canvas at outer 60-80% radius, central area "
    "nearly empty with only faint embers, afterglow fading to transparency, "
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
    "#2E2520, cooling surface, first crater shadows appearing, heat haze gone",
    # f11 - 황폐 행성 (최종)
    "final 160px primitive desolate planet at center, base grey-brown crust "
    "#5C4A3A with dark crack veins #2E2520, deep crater shadows #1A1410, "
    "no magma glow, no atmosphere, subtle stone highlight #A89888 on one side "
    "hinting at distant starlight, dead silent surface, completely static and held",
]
assert len(FRAME_PROMPTS) == 12


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


def sample_alpha(png_bytes: bytes, points):
    if not png_bytes.startswith(PNG_SIG):
        raise RuntimeError("not a PNG")
    idx = 8
    width = height = bit_depth = color_type = interlace = None
    idat = bytearray()
    while idx < len(png_bytes):
        length = int.from_bytes(png_bytes[idx : idx + 4], "big")
        ctype = png_bytes[idx + 4 : idx + 8].decode("ascii")
        data = png_bytes[idx + 8 : idx + 8 + length]
        idx += 8 + length + 4
        if ctype == "IHDR":
            width = int.from_bytes(data[0:4], "big")
            height = int.from_bytes(data[4:8], "big")
            bit_depth = data[8]
            color_type = data[9]
            interlace = data[12]
        elif ctype == "IDAT":
            idat.extend(data)
        elif ctype == "IEND":
            break
    if (width, height) != (SIZE, SIZE):
        raise RuntimeError(f"unexpected size {width}x{height}")
    if bit_depth != 8 or color_type != 6 or interlace != 0:
        raise RuntimeError(
            f"unexpected PNG fmt: bit_depth={bit_depth} color_type={color_type} "
            f"interlace={interlace}"
        )
    raw = zlib.decompress(bytes(idat))
    bpp = 4
    stride = width * bpp + 1
    out = bytearray(width * height * bpp)
    prev_row = bytearray(width * bpp)
    for y in range(height):
        row = raw[y * stride : (y + 1) * stride]
        ftype = row[0]
        cur = bytearray(row[1:])
        if ftype == 0:
            pass
        elif ftype == 1:
            for x in range(bpp, len(cur)):
                cur[x] = (cur[x] + cur[x - bpp]) & 0xFF
        elif ftype == 2:
            for x in range(len(cur)):
                cur[x] = (cur[x] + prev_row[x]) & 0xFF
        elif ftype == 3:
            for x in range(len(cur)):
                a = cur[x - bpp] if x >= bpp else 0
                b = prev_row[x]
                cur[x] = (cur[x] + (a + b) // 2) & 0xFF
        elif ftype == 4:
            for x in range(len(cur)):
                a = cur[x - bpp] if x >= bpp else 0
                b = prev_row[x]
                c = prev_row[x - bpp] if x >= bpp else 0
                p = a + b - c
                pa = abs(p - a)
                pb = abs(p - b)
                pc = abs(p - c)
                if pa <= pb and pa <= pc:
                    pr = a
                elif pb <= pc:
                    pr = b
                else:
                    pr = c
                cur[x] = (cur[x] + pr) & 0xFF
        else:
            raise RuntimeError(f"unknown PNG filter {ftype}")
        out[y * width * bpp : (y + 1) * width * bpp] = cur
        prev_row = cur
    alphas = []
    for x, y in points:
        offset = (y * width + x) * bpp + 3
        alphas.append(out[offset])
    return alphas


def background_sample_points(size: int):
    m = size - 1
    h = size // 2
    return [(0, 0), (m, 0), (0, m), (m, m), (h, 0), (h, m), (0, h), (m, h)]


def alpha_clip_background(raw_png: bytes, tol: int = 12) -> bytes:
    """Detect dominant corner color and zero its alpha across the image.

    Workaround for PixelLab `no_background:true` quirk where the model returns
    a solid background despite the flag (memory: pixellab-no-background-quirk).
    """
    img = Image.open(BytesIO(raw_png)).convert("RGBA")
    w, h = img.size
    corner_rgbs = [
        img.getpixel((0, 0))[:3],
        img.getpixel((w - 1, 0))[:3],
        img.getpixel((0, h - 1))[:3],
        img.getpixel((w - 1, h - 1))[:3],
    ]
    br, bg_, bb = Counter(corner_rgbs).most_common(1)[0][0]
    new_pixels = []
    for r, g, b, a in img.getdata():
        if a == 0 or abs(r - br) + abs(g - bg_) + abs(b - bb) <= tol:
            new_pixels.append((r, g, b, 0))
        else:
            new_pixels.append((r, g, b, a))
    img.putdata(new_pixels)
    buf = BytesIO()
    img.save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def write_empty_frame(idx: int) -> dict:
    """f00 is design-intent 'darkness' — a fully alpha-clear canvas.

    PixelLab cannot render 'empty', so we skip the API call and write a blank
    256x256 RGBA PNG directly. Matches design doc §3 frame f00 spec.
    """
    out_path = OUT_DIR / f"splash_anim_v2_bigbang_256_f{idx:02d}.png"
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    buf = BytesIO()
    img.save(buf, format="PNG", optimize=True)
    data = buf.getvalue()
    out_path.write_bytes(data)
    print(f"[f{idx:02d}] empty canvas written ({len(data)}B)", flush=True)
    return {
        "index": idx,
        "file": str(out_path.relative_to(ROOT)),
        "prompt": "(synthetic empty alpha-clear canvas — design f00)",
        "attempts": [{"attempt": 0, "ok": True, "synthetic": True}],
        "success": True,
        "final_size_bytes": len(data),
        "final_usd": 0.0,
        "final_bg_alphas": [0] * 8,
        "alpha_clipped": False,
        "synthetic": True,
    }


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
    points = background_sample_points(SIZE)
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
            alphas = sample_alpha(img, points)
            max_alpha = max(alphas)
            clipped = False
            if max_alpha > CORNER_ALPHA_MAX:
                # PixelLab quirk: solid bg returned despite no_background:true.
                # Apply corner-color alpha clipping post-process.
                img = alpha_clip_background(img, tol=12)
                if not img.startswith(PNG_SIG):
                    raise RuntimeError("clip output not PNG")
                if len(img) < MIN_BYTES:
                    raise RuntimeError(
                        f"clipped png too small: {len(img)}B < {MIN_BYTES}B"
                    )
                alphas = sample_alpha(img, points)
                max_alpha = max(alphas)
                if max_alpha > CORNER_ALPHA_MAX:
                    raise RuntimeError(
                        f"alpha clip did not clear bg: max={max_alpha} "
                        f"(samples={alphas})"
                    )
                clipped = True
            out_path.write_bytes(img)
            usage = body.get("usage", {})
            cost = float(usage.get("usd", 0.0)) if usage.get("type") == "usd" else 0.0
            info["attempts"].append({
                "attempt": attempt,
                "ok": True,
                "elapsed_s": round(elapsed, 2),
                "usage": usage,
                "size_bytes": len(img),
                "bg_sample_alphas": alphas,
                "alpha_clipped": clipped,
            })
            info["success"] = True
            info["final_size_bytes"] = len(img)
            info["final_usd"] = cost
            info["final_bg_alphas"] = alphas
            info["alpha_clipped"] = clipped
            clip_tag = " [CLIPPED]" if clipped else ""
            print(
                f"  ok ({elapsed:.1f}s, ${cost}, {len(img)}B, "
                f"bg_alpha_max={max_alpha}){clip_tag}",
                flush=True,
            )
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
    only = None
    if len(sys.argv) > 1:
        only = {int(a) for a in sys.argv[1:]}

    results = []
    total_usd = 0.0
    total_calls = 0

    for idx, base_prompt in enumerate(FRAME_PROMPTS):
        if only is not None and idx not in only:
            continue
        if idx == 0:
            # f00 = darkness; synthesize an empty alpha-clear canvas (no API call)
            info = write_empty_frame(idx)
        else:
            info = generate_frame(api_key, idx, base_prompt)
            total_calls += len(info["attempts"])
        results.append(info)
        if info["success"]:
            total_usd += info.get("final_usd", 0.0)
        time.sleep(0.5 if idx == 0 else 1.5)

    summary = {
        "endpoint": ENDPOINT,
        "size": SIZE,
        "frame_count": len(results),
        "total_calls": total_calls,
        "total_usd": round(total_usd, 6),
        "results": results,
        "version": "v3-alpha",
    }
    log_path = ROOT / "scripts" / "gen_splash_v3_alpha_result.json"
    log_path.write_text(json.dumps(summary, indent=2, ensure_ascii=False))
    print("\n=== splash v3 alpha summary ===")
    print(f"total_calls={total_calls}  total_usd=${summary['total_usd']}")
    ok = sum(1 for r in results if r["success"])
    print(f"frames ok: {ok}/{len(results)}")
    for r in results:
        status = "OK " if r["success"] else "FAIL"
        sz = r.get("final_size_bytes", "-")
        bg = r.get("final_bg_alphas", "-")
        print(f"  {status} f{r['index']:02d}  {sz}B  bg={bg}  {r['file']}")
    return 0 if ok == len(results) else 1


if __name__ == "__main__":
    sys.exit(main())
