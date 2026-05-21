#!/usr/bin/env python3
"""Build splash_v4_preview.gif from 30 phase PNGs with per-frame durations
per spec §3.5.

Output: Assets/AppIcon/splash_v4/splash_v4_preview.gif (looping, 10s total)
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ASSET_DIR = ROOT / "Assets" / "AppIcon" / "splash_v4"
OUTPUT = ASSET_DIR / "splash_v4_preview.gif"

# (phase_name, frame_count, per_frame_duration_ms)
# Total: 12*333=3996, 6*250=1500, 8*437=3496, 4*250=1000 ≈ 10000ms
PHASES = [
    ("phase1", 12, 333),
    ("phase2", 6, 250),
    ("phase3", 8, 437),
    ("phase4", 4, 250),
]


def main() -> int:
    frames: list[Image.Image] = []
    durations: list[int] = []

    for phase_name, count, dur in PHASES:
        for i in range(count):
            path = ASSET_DIR / f"{phase_name}_f{i:02d}.png"
            if not path.exists():
                print(f"ERROR: missing {path}", file=sys.stderr)
                return 1
            img = Image.open(path).convert("RGBA")
            # GIF can't do full alpha; composite onto deep cosmic background
            # (spec §2.3 BgOuter #040616) to preview what runtime will show.
            bg = Image.new("RGBA", img.size, (0x04, 0x06, 0x16, 255))
            bg.paste(img, (0, 0), img)
            frames.append(bg.convert("P", palette=Image.ADAPTIVE, colors=128))
            durations.append(dur)

    total_ms = sum(durations)
    print(f"frames={len(frames)} total_duration={total_ms}ms")
    assert len(frames) == 30, f"expected 30 frames, got {len(frames)}"

    frames[0].save(
        OUTPUT,
        save_all=True,
        append_images=frames[1:],
        duration=durations,
        loop=0,
        disposal=2,
        optimize=False,
    )

    size_bytes = OUTPUT.stat().st_size
    print(f"OK wrote {OUTPUT} ({size_bytes}B, {total_ms/1000:.2f}s loop)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
