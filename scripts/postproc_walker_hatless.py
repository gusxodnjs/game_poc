"""Post-process the PixelLab "TerraWalkerKid Hatless" walk animation into the
eight canonical 64x64 player frames (hat removal request).

Same chibi kid as the red-cap walker, but bare head with natural brown hair
(no cap). This regenerates BOTH the front (south) idle/walk frames and the
side (east) walk frames so the on-map player matches across directions:
  assets/characters/walker_front_idle_frame{0,1,2,3}_64x64.png   <- south walk
  assets/characters/walker_side_walk_frame{0,1,2,3}_64x64.png    <- east  walk

We KEEP the existing filenames (and therefore the .meta GUIDs Unity tracks) and
only overwrite the PNG pixel contents.

Pipeline (mirrors scripts/postproc_walker_walk.py, generalised to 2 directions
and sourcing frames from the completed character download ZIP):
  1. Read the 6 raw walk frames per direction from /tmp/hl_dl.zip.
  2. Pick phases [0,1,3,4] = contact / step / contact / opposite-step -> a clean
     4-frame alternating-leg loop.
  3. Per direction: SHARED scale (from the tallest chosen frame, target height
     ~62/64) so apparent size is constant across the cycle, then paste onto a
     64x64 transparent canvas with the bbox BOTTOM (planted foot) anchored to a
     fixed baseline so the cycle does not jitter vertically.

Pure PIL. PNG signature validated on read.
"""
import io
import sys
import zipfile
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow required: pip3 install Pillow")

ROOT = Path(__file__).resolve().parent.parent
CHARS = ROOT / "assets" / "characters"  # actual on-disk case is lowercase
ZIP = Path("/tmp/hl_dl.zip")
ANIM_DIR = "TerraWalkerKid_Hatless/animations/walking-9a0935dc"

N_RAW = 6
CHOSEN = [0, 1, 3, 4]  # contact / step / contact / opposite-step
PNG_SIG = b"\x89PNG\r\n\x1a\n"

CANVAS = 64
TARGET_H = 62
BASELINE_Y = 63

# direction -> output filename stem
OUTPUTS = {
    "south": "walker_front_idle_frame{}_64x64.png",
    "east": "walker_side_walk_frame{}_64x64.png",
}


def load_frames(z: zipfile.ZipFile, direction: str) -> list[Image.Image]:
    frames = []
    for i in range(N_RAW):
        raw = z.read(f"{ANIM_DIR}/{direction}/frame_{i:03d}.png")
        if not raw.startswith(PNG_SIG):
            raise RuntimeError(f"{direction} frame {i}: invalid PNG signature")
        frames.append(Image.open(io.BytesIO(raw)).convert("RGBA"))
    return frames


def autocrop(img: Image.Image) -> Image.Image:
    bbox = img.getbbox()
    return img if bbox is None else img.crop(bbox)


def place(img: Image.Image, scale: float) -> Image.Image:
    cropped = autocrop(img.convert("RGBA"))
    w, h = cropped.size
    new_w = max(1, round(w * scale))
    new_h = max(1, round(h * scale))
    scaled = cropped.resize((new_w, new_h), Image.NEAREST)
    canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    x = (CANVAS - new_w) // 2
    y = BASELINE_Y - new_h
    canvas.paste(scaled, (x, y), scaled)
    return canvas


def process_direction(z: zipfile.ZipFile, direction: str, name_tpl: str) -> list[Path]:
    raw = load_frames(z, direction)
    chosen = [raw[i] for i in CHOSEN]
    heights = []
    for f in chosen:
        bbox = f.getbbox()
        heights.append((bbox[3] - bbox[1]) if bbox else f.height)
    ref_h = max(heights)
    scale = TARGET_H / ref_h
    print(f"[{direction}] phases {CHOSEN}, ref bbox h={ref_h}px -> scale {scale:.3f}",
          flush=True)
    out_paths = []
    for i in range(4):
        placed = place(chosen[i], scale)
        out = CHARS / name_tpl.format(i)
        placed.save(out, format="PNG", optimize=True)
        out_paths.append(out)
        print(f"  wrote {out.name}", flush=True)
    return out_paths


def main() -> int:
    if not ZIP.exists():
        sys.exit(f"missing {ZIP}")
    z = zipfile.ZipFile(ZIP)
    all_paths = []
    for direction, name_tpl in OUTPUTS.items():
        all_paths += process_direction(z, direction, name_tpl)

    # verification strip: 8 frames, 4x upscaled, with a baseline guide row.
    strip = Image.new("RGBA", (CANVAS * len(all_paths), CANVAS), (30, 30, 30, 255))
    for i, p in enumerate(all_paths):
        fr = Image.open(p)
        strip.paste(fr, (i * CANVAS, 0), fr)
    for x in range(CANVAS * len(all_paths)):
        strip.putpixel((x, BASELINE_Y), (255, 80, 80, 255))
    strip.resize((CANVAS * len(all_paths) * 4, CANVAS * 4), Image.NEAREST).save(
        "/tmp/hatless_strip.png"
    )
    print("wrote /tmp/hatless_strip.png", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
