"""Post-process the PixelLab TerraWalkerKid south WALK animation into the four
canonical 64x64 player frames (issue #52).

The user wants the on-map player to:
  - read BIGGER (chibi fills more of the 64x64 canvas, feet near bottom), and
  - have a NATURAL 4-frame WALK cycle (legs alternate: left step / passing /
    right step / passing) with arm swing + slight vertical bob,
replacing the old subtle "breathing idle".

We KEEP the four existing filenames (and therefore the .meta GUIDs Unity tracks):
  Assets/characters/walker_front_idle_frame{0,1,2,3}_64x64.png
and only overwrite their PNG pixel contents.

Pipeline:
  1. Download the completed character zip from the MCP download endpoint.
  2. Locate the south-direction WALK animation frames inside the zip.
  3. Pick 4 frames that form a clean step cycle (contact L / passing /
     contact R / passing). PixelLab walk templates commonly emit N+1 frames
     (loop repeats first frame); we drop the duplicate tail and resample to 4.
  4. For each chosen frame: autocrop to the opaque bounding box, scale up so the
     character is LARGE in-frame (target height ~ 56/64 of canvas), then paste
     onto a 64x64 transparent canvas with the FEET anchored to a fixed baseline
     so the cycle doesn't jitter vertically (the only vertical motion is the
     intended 1px bob on passing frames).

Pure PIL. PNG signature validated on download.
"""
import io
import sys
import urllib.request
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow required: pip3 install Pillow")

ROOT = Path(__file__).resolve().parent.parent
CHARS = ROOT / "Assets" / "characters"
TMP = Path("/tmp")
CHAR_ID = "9dde2d2a-f827-4122-8cf2-bf083baa7815"
ANIM_ID = "eda949ca-54c8-4d69-834a-e96379a76eb6"
FRAME_BASE = (
    "https://backblaze.pixellab.ai/file/pixellab-characters/"
    "baa3ebb5-0eb4-4a99-9215-5384d147f7ce/"
    f"{CHAR_ID}/animations/{ANIM_ID}/south"
)
N_RAW = 6  # PixelLab returned 6 south walk frames
# Which raw phases form a clean 4-frame loop with both legs clearly stepping:
#   0 = contact (feet together), 1 = RIGHT foot step,
#   3 = contact (feet together), 4 = LEFT foot step.
# -> left step / passing / right step / passing rhythm.
CHOSEN = [0, 1, 3, 4]
PNG_SIG = b"\x89PNG\r\n\x1a\n"

CANVAS = 64
# How tall the character should be within the 64px canvas (bigger presence).
# The old idle filled ~51-53px tall; this fills ~62px (nearly the whole canvas)
# -> clearly larger on map. Silhouette is only ~27px wide so it never clips.
TARGET_H = 62
# Baseline: feet sit this many px from the top (feet right near the bottom edge).
BASELINE_Y = 63


def fetch_frame(i: int) -> Image.Image:
    """Load a south walk frame.

    The backblaze CDN rejects urllib (403 without a browser UA) but accepts
    curl, so we shell out to curl into /tmp and read from there. The frames are
    cached in /tmp/walkraw_{i}.png on first run.
    """
    import subprocess

    local = TMP / f"walkraw_{i}.png"
    if not local.exists() or local.stat().st_size == 0:
        url = f"{FRAME_BASE}/{i}.png"
        subprocess.run(
            ["curl", "-sS", "-A", "Mozilla/5.0", "-o", str(local), url],
            check=True,
        )
    raw = local.read_bytes()
    if not raw.startswith(PNG_SIG):
        raise RuntimeError(f"frame {i}: invalid PNG signature ({local})")
    return Image.open(io.BytesIO(raw)).convert("RGBA")


def autocrop(img: Image.Image) -> Image.Image:
    bbox = img.getbbox()
    if bbox is None:
        return img
    return img.crop(bbox)


def place(img: Image.Image, scale: float) -> Image.Image:
    """Scale the autocropped frame by a SHARED scale factor (so all frames use the
    same pixels-per-unit -> the character's apparent size is constant), center
    horizontally, and anchor the bbox BOTTOM (the planted foot) to BASELINE_Y on
    a 64x64 transparent canvas.

    Anchoring bbox-bottom to a fixed baseline means the lowest foot pixel sits on
    the same row every frame. In a walk cycle the planted foot is always the
    lowest pixel, so the feet do NOT jitter vertically; the only vertical motion
    is the natural body bob already drawn into the frames (the raised foot and
    head rise/fall within the silhouette)."""
    cropped = autocrop(img.convert("RGBA"))
    w, h = cropped.size
    new_w = max(1, round(w * scale))
    new_h = max(1, round(h * scale))
    scaled = cropped.resize((new_w, new_h), Image.NEAREST)

    canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    x = (CANVAS - new_w) // 2
    y = BASELINE_Y - new_h  # bottom of sprite at BASELINE_Y
    canvas.paste(scaled, (x, y), scaled)
    return canvas


def main() -> int:
    print(f"fetching {N_RAW} south walk frames...", flush=True)
    raw = [fetch_frame(i) for i in range(N_RAW)]
    chosen = [raw[i] for i in CHOSEN]
    print(f"chosen phases {CHOSEN} (contact/right-step/contact/left-step)", flush=True)

    # SHARED scale: derive from the tallest chosen frame so TARGET_H maps to the
    # full standing height; every frame then uses the SAME px/unit -> the
    # character's apparent size is constant across the cycle (no size pulsing).
    heights = []
    for f in chosen:
        bbox = f.getbbox()
        heights.append((bbox[3] - bbox[1]) if bbox else f.height)
    ref_h = max(heights)
    scale = TARGET_H / ref_h
    print(f"ref bbox height {ref_h}px -> shared scale {scale:.3f}", flush=True)

    out_paths = []
    for i in range(4):
        placed = place(chosen[i], scale)
        out = CHARS / f"walker_front_idle_frame{i}_64x64.png"
        placed.save(out, format="PNG", optimize=True)
        out_paths.append(out)
        print(f"  wrote {out.name}", flush=True)

    # Horizontal verification strip, 4x upscaled, with a baseline guide row so we
    # can eyeball that feet stay anchored.
    strip = Image.new("RGBA", (CANVAS * 4, CANVAS), (30, 30, 30, 255))
    for i, p in enumerate(out_paths):
        fr = Image.open(p)
        strip.paste(fr, (i * CANVAS, 0), fr)
    # draw baseline guide
    for x in range(CANVAS * 4):
        strip.putpixel((x, BASELINE_Y), (255, 80, 80, 255))
    strip_big = strip.resize((CANVAS * 4 * 4, CANVAS * 4), Image.NEAREST)
    strip_big.save(TMP / "walk_strip.png")
    print("wrote /tmp/walk_strip.png", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
