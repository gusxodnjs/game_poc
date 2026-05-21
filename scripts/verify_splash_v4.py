#!/usr/bin/env python3
"""Verify all 30 splash v4 PNGs match spec §6.3 acceptance criteria."""
from __future__ import annotations

import glob
import subprocess
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ASSET_DIR = ROOT / "Assets" / "AppIcon" / "splash_v4"

EXPECTED_PHASES = [
    ("phase1", 12),
    ("phase2", 6),
    ("phase3", 8),
    ("phase4", 4),
]
EXPECTED_TOTAL = 30
EXPECTED_SIZE = (256, 256)
CORNER_ALPHA_MAX = 32


def main() -> int:
    failures: list[str] = []
    total = 0

    for phase_name, expected_count in EXPECTED_PHASES:
        pattern = str(ASSET_DIR / f"{phase_name}_f*.png")
        files = sorted(glob.glob(pattern))
        if len(files) != expected_count:
            failures.append(
                f"{phase_name}: found {len(files)} files, expected {expected_count}"
            )
            continue

        for f in files:
            total += 1
            try:
                result = subprocess.check_output(["file", f], text=True)
                if "PNG image data" not in result:
                    failures.append(f"{f}: file says {result.strip()}")
                    continue
            except subprocess.CalledProcessError as e:
                failures.append(f"{f}: file failed: {e}")
                continue

            try:
                img = Image.open(f)
            except Exception as e:
                failures.append(f"{f}: PIL open failed: {e}")
                continue

            if img.mode != "RGBA":
                failures.append(f"{f}: mode={img.mode}, expected RGBA")
                continue
            if img.size != EXPECTED_SIZE:
                failures.append(f"{f}: size={img.size}, expected {EXPECTED_SIZE}")
                continue

            w, h = img.size
            corners = [
                img.getpixel((0, 0))[3],
                img.getpixel((w - 1, 0))[3],
                img.getpixel((0, h - 1))[3],
                img.getpixel((w - 1, h - 1))[3],
            ]
            if max(corners) > CORNER_ALPHA_MAX:
                failures.append(f"{f}: corners={corners}, max > {CORNER_ALPHA_MAX}")
                continue

            print(f"OK {Path(f).name}  corners={corners}")

    if total != EXPECTED_TOTAL:
        failures.append(f"total count {total} != {EXPECTED_TOTAL}")

    if failures:
        print(f"\nFAIL: {len(failures)} issue(s):", file=sys.stderr)
        for f in failures:
            print(f"  - {f}", file=sys.stderr)
        return 1
    print(f"\n{total}/{EXPECTED_TOTAL} PNGs verified OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
