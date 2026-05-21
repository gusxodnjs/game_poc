# Splash v4 Assets PR Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate splash v4 assets (30 transparent-bg PNGs via PixelLab MCP, 10s procedural BGM wav, preview GIF) and open a draft PR for user GIF review before code integration.

**Architecture:** Per-phase PixelLab MCP calls (`create_1_direction_object` + `animate_object`, 4 phases) → download frame URLs → corner-color alpha clipping (PIL) → batch verification → assemble preview GIF (PIL per-frame durations) + procedural BGM v2 (stdlib `wave`) → draft PR.

**Tech Stack:**
- PixelLab MCP tools (agent-invoked): `mcp__pixellab__create_1_direction_object`, `mcp__pixellab__animate_object`, `mcp__pixellab__get_object`, `mcp__pixellab__get_balance`
- Python 3 + PIL (Pillow 12.x) for postproc / GIF
- Python stdlib (`wave`, `struct`, `math`, `random`) for BGM
- `gh` CLI for PR
- Git worktree for isolation

**Scope:** Spec §7 PR #X+1 only. Code PR (#X+2 — SplashScreen.cs) is out of scope. Draft PR — do not mark ready until user approves the GIF.

**SSOT:** `docs/superpowers/specs/2026-05-21-splash-v4-design.md`

---

## File Structure

**Created in this PR:**

| File | Purpose |
|---|---|
| `scripts/postproc_splash_v4.py` | Library: download a PixelLab frame URL, apply corner-color alpha clipping, write RGBA PNG. CLI for single-frame use. |
| `scripts/verify_splash_v4.py` | Verify all 30 PNGs (file signature, PIL mode=RGBA, size=256×256, corner alpha ≤ 32). |
| `scripts/build_splash_v4_preview.py` | Read 30 PNGs in order, write `splash_v4_preview.gif` with per-frame durations from spec §3.5. |
| `scripts/gen_splash_bgm.py` (**modify**) | Add `--version v2` flag with new 10s timings from spec §4.1. |
| `scripts/splash_v4_state.json` | Track MCP object/animation IDs across tasks for restart safety. |
| `Assets/AppIcon/splash_v4/phase1_f00.png` .. `phase1_f11.png` (12) | Phase 1 frames |
| `Assets/AppIcon/splash_v4/phase2_f00.png` .. `phase2_f05.png` (6) | Phase 2 frames |
| `Assets/AppIcon/splash_v4/phase3_f00.png` .. `phase3_f07.png` (8) | Phase 3 frames |
| `Assets/AppIcon/splash_v4/phase4_f00.png` .. `phase4_f03.png` (4) | Phase 4 frames |
| `Assets/AppIcon/splash_v4/splash_v4_preview.gif` | Preview GIF for user review |
| `Assets/Audio/splash_bgm_v2.wav` | 10s procedural BGM |

**NOT in scope (separate code PR):**
- `Assets/Scripts/SplashScreen.cs` changes
- v3 asset removal (`splash_anim_v2_bigbang_256_f*.png`, `splash_bgm_v1.wav`)
- Unity scene wiring

---

## Pre-Task Setup

Before Task 1, the executor must load these deferred MCP tool schemas via `ToolSearch` (they are not in the default tool surface):

```
ToolSearch(query="select:mcp__pixellab__create_1_direction_object,mcp__pixellab__animate_object,mcp__pixellab__get_object,mcp__pixellab__get_balance")
```

---

## Task 1: Worktree + Branch + Scaffold

**Files:**
- Create: `.claude/worktrees/feat-splash-v4-assets/` (worktree)
- Create: `scripts/splash_v4_state.json` (empty `{}`)
- Create: `Assets/AppIcon/splash_v4/.gitkeep`

- [ ] **Step 1: Create worktree on new branch off origin/main**

```bash
cd /Users/hyun/projects/game_poc
git fetch origin
git worktree add -b feat/splash-v4-assets .claude/worktrees/feat-splash-v4-assets origin/main
cd .claude/worktrees/feat-splash-v4-assets
```

Expected: New worktree at `.claude/worktrees/feat-splash-v4-assets`, branch `feat/splash-v4-assets` based on `origin/main`.

- [ ] **Step 2: Verify worktree state**

```bash
git status
git branch --show-current
```

Expected: clean, on `feat/splash-v4-assets`.

- [ ] **Step 3: Verify PixelLab subscription**

Agent call: `mcp__pixellab__get_balance`

Expected: `subscription: active`, generations remaining > 400 (4 phases × ~100 generations budget).

If insufficient: STOP, report to user, do not proceed.

- [ ] **Step 4: Create asset directory and state file**

```bash
mkdir -p Assets/AppIcon/splash_v4
touch Assets/AppIcon/splash_v4/.gitkeep
echo '{}' > scripts/splash_v4_state.json
```

- [ ] **Step 5: Initial commit**

```bash
git add Assets/AppIcon/splash_v4/.gitkeep scripts/splash_v4_state.json
git commit -m "chore(splash-v4): scaffold asset dir + state file

For PR #X+1 (asset PR). State file tracks PixelLab MCP object_ids
across phase tasks for restart safety.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: BGM v2 Generator (10s, gen_splash_bgm.py --version v2)

**Files:**
- Modify: `scripts/gen_splash_bgm.py` (add `--version v2` branch with new timings from spec §4.1)
- Create: `Assets/Audio/splash_bgm_v2.wav` (output)

- [ ] **Step 1: Read existing gen_splash_bgm.py**

```bash
cat scripts/gen_splash_bgm.py
```

Note current structure: stdlib-only (wave/struct/math/random), single fixed 8s output. Identify the per-section synthesis functions.

- [ ] **Step 2: Add `--version` CLI flag and v2 timing table**

Modify `scripts/gen_splash_bgm.py`:
1. Add `argparse` import + arg `--version` with choices `["v1", "v2"]`, default `"v1"`.
2. Extract timing constants into a dict keyed by version:
   ```python
   TIMINGS = {
       "v1": {
           "total_ms": 8000,
           "sections": [
               ("drone_in", 0, 1500),
               ("sweep", 1500, 1700),
               ("explosion", 1700, 2200),
               ("pad", 2200, 6500),
               ("fade", 6500, 8000),
           ],
           "output": "Assets/Audio/splash_bgm_v1.wav",
       },
       "v2": {
           "total_ms": 10000,
           "sections": [
               ("drone_in", 0, 4000),       # 잔잔 페이드인 0 → 0.10
               ("sweep", 4000, 4500),       # 220→880Hz 글리산도
               ("explosion", 4500, 5500),   # 화이트 노이즈 + 55Hz boom
               ("pad", 5500, 9000),         # 110/165/220 화음
               ("fade", 9000, 10000),       # 0.10 → 0.00
           ],
           "output": "Assets/Audio/splash_bgm_v2.wav",
       },
   }
   ```
3. Wire `main()` to pick timings + output path by `args.version`.
4. Verify each section function (drone_in / sweep / explosion / pad / fade) accepts `start_ms`, `end_ms` and computes duration internally — refactor if currently hardcoded to v1 timings.

- [ ] **Step 3: Run v2 generation**

```bash
cd /Users/hyun/projects/game_poc/.claude/worktrees/feat-splash-v4-assets
python3 scripts/gen_splash_bgm.py --version v2
```

Expected: writes `Assets/Audio/splash_bgm_v2.wav`, prints duration ~10.0s.

- [ ] **Step 4: Verify wav properties**

```bash
python3 -c "
import wave
with wave.open('Assets/Audio/splash_bgm_v2.wav') as w:
    dur = w.getnframes() / w.getframerate()
    print(f'duration={dur:.2f}s channels={w.getnchannels()} sr={w.getframerate()} bits={w.getsampwidth()*8}')
    assert abs(dur - 10.0) < 0.05, f'expected 10.0s, got {dur:.2f}'
    assert w.getnchannels() == 1
    assert w.getframerate() == 44100
    assert w.getsampwidth() == 2  # 16-bit
print('OK')
"
```

Expected: `duration=10.00s channels=1 sr=44100 bits=16\nOK`

- [ ] **Step 5: Verify v1 still works (regression)**

```bash
python3 scripts/gen_splash_bgm.py --version v1
python3 -c "
import wave
with wave.open('Assets/Audio/splash_bgm_v1.wav') as w:
    dur = w.getnframes() / w.getframerate()
    assert abs(dur - 8.0) < 0.05, f'v1 regression: expected 8.0s got {dur:.2f}'
print('v1 OK')
"
```

Expected: `v1 OK`. (v1 wav already committed; this overwrites with same content — verify byte-identical or accept rebuild.)

- [ ] **Step 6: Commit**

```bash
git add scripts/gen_splash_bgm.py Assets/Audio/splash_bgm_v2.wav
git commit -m "feat(audio): splash BGM v2 generator (10s procedural)

gen_splash_bgm.py 에 --version v2 추가. spec §4.1 구간 구조:
- 0–4000ms 드론 페이드인
- 4000–4500ms sweep
- 4500–5500ms 폭발 (peak)
- 5500–9000ms 화음 패드
- 9000–10000ms fade

stdlib only (wave/struct/math). splash_bgm_v1.wav 는 보존.
v3 코드가 v1 참조 중 — v2 wav 는 코드 PR (#X+2) 에서 wire.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Postproc Library (download + alpha clip)

**Files:**
- Create: `scripts/postproc_splash_v4.py`

- [ ] **Step 1: Write the library + CLI**

Create `scripts/postproc_splash_v4.py`:

```python
#!/usr/bin/env python3
"""Postprocess splash v4 frames: download from PixelLab URL, apply
corner-color alpha clipping (memory: pixellab-no-background-quirk),
write RGBA PNG to disk.

Library functions are importable; CLI processes a single frame.

Usage (CLI):
    postproc_splash_v4.py <url> <output_path>
    postproc_splash_v4.py --file <input_path> <output_path>  # already downloaded

The corner-color alpha clipping handles PixelLab's no_background:true
quirk where solid background is sometimes returned despite the flag.
"""
from __future__ import annotations

import argparse
import sys
import urllib.request
from collections import Counter
from io import BytesIO
from pathlib import Path

from PIL import Image

EXPECTED_SIZE = (256, 256)
ALPHA_CLIP_TOL = 12
CORNER_ALPHA_MAX = 32


def download_png(url: str, timeout: int = 60) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": "splash-v4-postproc/1.0"})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        data = resp.read()
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        raise RuntimeError(f"not a PNG (head={data[:16]!r})")
    return data


def alpha_clip_background(raw_png: bytes, tol: int = ALPHA_CLIP_TOL) -> bytes:
    img = Image.open(BytesIO(raw_png)).convert("RGBA")
    w, h = img.size
    corners = [
        img.getpixel((0, 0))[:3],
        img.getpixel((w - 1, 0))[:3],
        img.getpixel((0, h - 1))[:3],
        img.getpixel((w - 1, h - 1))[:3],
    ]
    br, bg_, bb = Counter(corners).most_common(1)[0][0]
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


def process_to_file(raw_png: bytes, out_path: Path) -> dict:
    """Apply alpha clip, validate, write. Returns info dict."""
    img_check = Image.open(BytesIO(raw_png))
    if img_check.size != EXPECTED_SIZE:
        raise RuntimeError(f"unexpected size {img_check.size}, expected {EXPECTED_SIZE}")

    clipped = alpha_clip_background(raw_png)
    if not clipped.startswith(b"\x89PNG\r\n\x1a\n"):
        raise RuntimeError("clip output not PNG")

    img = Image.open(BytesIO(clipped))
    w, h = img.size
    corners = [
        img.getpixel((0, 0))[3],
        img.getpixel((w - 1, 0))[3],
        img.getpixel((0, h - 1))[3],
        img.getpixel((w - 1, h - 1))[3],
    ]
    max_corner = max(corners)
    if max_corner > CORNER_ALPHA_MAX:
        raise RuntimeError(
            f"alpha clip insufficient: max corner alpha={max_corner} > {CORNER_ALPHA_MAX}"
        )

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_bytes(clipped)
    return {
        "path": str(out_path),
        "size_bytes": len(clipped),
        "corner_alphas": corners,
    }


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", help="URL or path (with --file)")
    parser.add_argument("output", help="Output PNG path")
    parser.add_argument("--file", action="store_true", help="Read source as file path")
    args = parser.parse_args(argv)

    if args.file:
        raw = Path(args.source).read_bytes()
    else:
        raw = download_png(args.source)

    info = process_to_file(raw, Path(args.output))
    print(f"OK {info['path']} ({info['size_bytes']}B, corners={info['corner_alphas']})")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
```

- [ ] **Step 2: Test alpha clip on a known input**

```bash
cd /Users/hyun/projects/game_poc/.claude/worktrees/feat-splash-v4-assets
python3 -c "
from PIL import Image
img = Image.new('RGBA', (256, 256), (128, 64, 32, 255))  # solid brown background
# Add a few non-background pixels at center
for x in range(120, 136):
    for y in range(120, 136):
        img.putpixel((x, y), (255, 0, 0, 255))
img.save('/tmp/test_input.png')
"
python3 scripts/postproc_splash_v4.py --file /tmp/test_input.png /tmp/test_output.png
python3 -c "
from PIL import Image
img = Image.open('/tmp/test_output.png')
print(f'mode={img.mode} size={img.size}')
corners = [img.getpixel((0,0))[3], img.getpixel((255,0))[3], img.getpixel((0,255))[3], img.getpixel((255,255))[3]]
center = img.getpixel((128,128))
print(f'corners={corners} center={center}')
assert max(corners) <= 32
assert center[3] == 255  # center red square preserved
print('OK')
"
```

Expected: `OK splash_v4 ... corners=[0, 0, 0, 0]\nmode=RGBA size=(256, 256)\ncorners=[0, 0, 0, 0] center=(255, 0, 0, 255)\nOK`

- [ ] **Step 3: Commit**

```bash
git add scripts/postproc_splash_v4.py
git commit -m "feat(scripts): postproc utility (download + alpha clip)

scripts/postproc_splash_v4.py — PixelLab no_background quirk 우회용
corner-color alpha clipping (memory: pixellab-no-background-quirk).
다음 task 의 phase 자산 생성에서 사용.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Phase 1 Assets (12 frames — 고요 → 점광)

**Files:**
- Create: `Assets/AppIcon/splash_v4/phase1_f00.png` .. `phase1_f11.png` (12)
- Modify: `scripts/splash_v4_state.json` (record `phase1.object_id`, `phase1.animation_id`)

**Note on agent execution:** This task involves agent MCP tool calls and Bash. The executor should keep `splash_v4_state.json` updated after each MCP success so a restart can resume.

- [ ] **Step 1: Create base object via PixelLab MCP**

Agent call:
```
mcp__pixellab__create_1_direction_object(
  description="empty deep cosmic void, completely dark, single tiny faint point of pure white light at exact center, no stars, no objects, transparent background, pixel art style",
  size=256,
  view="top-down"
)
```

Capture returned `object_id` → record in `scripts/splash_v4_state.json` as `phase1.base_object_id`.

- [ ] **Step 2: Poll base object until COMPLETED**

Loop (max 5 min, 5s interval):
```
mcp__pixellab__get_object(object_id=<phase1.base_object_id>)
```

Expected terminal: `status == "completed"`. If `status == "review"` (shouldn't happen at size=256, but defensive): call `mcp__pixellab__select_object_frames(object_id=<id>, indices=[0])` and use returned new object_id.

If `status == "failed"` after 5 min: abort task, surface error to user.

- [ ] **Step 3: Queue animation**

Agent call:
```
mcp__pixellab__animate_object(
  object_id=<phase1.base_object_id>,
  animation_description="tiny point of light very slowly brightening and softly pulsating, gradual intensity increase from barely visible to small clear pinpoint",
  frame_count=12
)
```

Capture returned animation/job identifiers → record as `phase1.animation_id` (whatever ID the response provides — could be a new object_id grouping the animation frames).

- [ ] **Step 4: Poll animation until COMPLETED**

Loop (max 10 min, 10s interval):
```
mcp__pixellab__get_object(object_id=<phase1.animation_id>)
```

Expected: `status == "completed"`, response contains 12 frame URLs (field names may vary — likely `frames`, `urls`, or `images` — inspect response shape).

- [ ] **Step 5: Download and postproc all 12 frames**

For each frame index `i` in `0..11`, get the URL from the get_object response and run:

```bash
python3 scripts/postproc_splash_v4.py "<frame_i_url>" Assets/AppIcon/splash_v4/phase1_f$(printf '%02d' $i).png
```

Each invocation downloads, applies corner alpha clipping, writes 256×256 RGBA PNG, prints `OK ... corners=[≤32, ≤32, ≤32, ≤32]`.

If any frame fails the alpha clip check (corner alpha > 32 even after clipping): record the URL in state file under `phase1.failures`, continue with remaining frames, surface failures at end of task.

- [ ] **Step 6: Verify all 12 phase1 PNGs**

```bash
python3 -c "
import glob, os
from PIL import Image
files = sorted(glob.glob('Assets/AppIcon/splash_v4/phase1_f*.png'))
assert len(files) == 12, f'expected 12 phase1 PNGs, got {len(files)}'
for f in files:
    img = Image.open(f)
    assert img.mode == 'RGBA', f'{f}: mode={img.mode}'
    assert img.size == (256, 256), f'{f}: size={img.size}'
    w, h = img.size
    corners = [img.getpixel((0,0))[3], img.getpixel((w-1,0))[3], img.getpixel((0,h-1))[3], img.getpixel((w-1,h-1))[3]]
    assert max(corners) <= 32, f'{f}: corners={corners}'
    print(f'OK {os.path.basename(f)} corners={corners}')
"
```

Expected: 12 `OK` lines.

- [ ] **Step 7: Commit phase 1**

```bash
git add Assets/AppIcon/splash_v4/phase1_f*.png scripts/splash_v4_state.json
git commit -m "feat(splash-v4): phase 1 자산 (12 frames, 고요 → 점광)

PixelLab MCP create_1_direction_object + animate_object 호출.
4000ms 동안 점광이 서서히 밝아지는 시퀀스. spec §3.2 phase 1.

corner alpha 검증 통과 (모든 frame ≤ 32). PixelLab no_background
quirk 는 postproc_splash_v4.py 의 corner-color alpha clipping 으로 우회.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Phase 2 Assets (6 frames — 빅뱅)

**Files:**
- Create: `Assets/AppIcon/splash_v4/phase2_f00.png` .. `phase2_f05.png` (6)

Same flow as Task 4, with phase 2 descriptions from spec §3.2.

- [ ] **Step 1: Create base object**

Agent call:
```
mcp__pixellab__create_1_direction_object(
  description="supernova explosion flash, intense bright white core 80px with saturated yellow #ffd755 shockwave ring 120px expanding outward, hard pixel edges, transparent background, pixel art style",
  size=256,
  view="top-down"
)
```

Record `phase2.base_object_id` in state file.

- [ ] **Step 2: Poll base object until COMPLETED**

Same polling logic as Task 4 Step 2.

- [ ] **Step 3: Queue animation (frame_count=6)**

```
mcp__pixellab__animate_object(
  object_id=<phase2.base_object_id>,
  animation_description="explosion shockwave ring rapidly expanding outward, core dimming as ring grows, intense burst of energy",
  frame_count=6
)
```

Record `phase2.animation_id`.

- [ ] **Step 4: Poll animation until COMPLETED**

Same as Task 4 Step 4.

- [ ] **Step 5: Download and postproc 6 frames**

For each frame index `i` in `0..5`:
```bash
python3 scripts/postproc_splash_v4.py "<frame_i_url>" Assets/AppIcon/splash_v4/phase2_f$(printf '%02d' $i).png
```

- [ ] **Step 6: Verify 6 phase2 PNGs**

```bash
python3 -c "
import glob, os
from PIL import Image
files = sorted(glob.glob('Assets/AppIcon/splash_v4/phase2_f*.png'))
assert len(files) == 6
for f in files:
    img = Image.open(f)
    assert img.mode == 'RGBA' and img.size == (256, 256)
    w, h = img.size
    corners = [img.getpixel((0,0))[3], img.getpixel((w-1,0))[3], img.getpixel((0,h-1))[3], img.getpixel((w-1,h-1))[3]]
    assert max(corners) <= 32
    print(f'OK {os.path.basename(f)} corners={corners}')
"
```

Expected: 6 OK lines.

- [ ] **Step 7: Commit phase 2**

```bash
git add Assets/AppIcon/splash_v4/phase2_f*.png scripts/splash_v4_state.json
git commit -m "feat(splash-v4): phase 2 자산 (6 frames, 빅뱅)

PixelLab MCP. 1500ms 폭발 코어 + 외곽 충격파 확장. spec §3.2 phase 2.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Phase 3 Assets (8 frames — 응집)

**Files:**
- Create: `Assets/AppIcon/splash_v4/phase3_f00.png` .. `phase3_f07.png` (8)

Same flow as Tasks 4–5 with phase 3 descriptions.

- [ ] **Step 1: Create base object**

```
mcp__pixellab__create_1_direction_object(
  description="scattered debris and dust cloud at outer 60-80% radius in muted brown #5C4A3A and grey #4a4a52, faint circular gravitational halo glow at center in dim grey-brown, transparent background, pixel art style",
  size=256,
  view="top-down"
)
```

Record `phase3.base_object_id`.

- [ ] **Step 2: Poll until COMPLETED**

Same as prior.

- [ ] **Step 3: Queue animation (frame_count=8)**

```
mcp__pixellab__animate_object(
  object_id=<phase3.base_object_id>,
  animation_description="debris and dust slowly pulling inward toward center accreting into a forming mass, gravitational coalescence, gradual darkening",
  frame_count=8
)
```

Record `phase3.animation_id`.

- [ ] **Step 4: Poll animation until COMPLETED**

- [ ] **Step 5: Download and postproc 8 frames**

For each `i` in `0..7`:
```bash
python3 scripts/postproc_splash_v4.py "<frame_i_url>" Assets/AppIcon/splash_v4/phase3_f$(printf '%02d' $i).png
```

- [ ] **Step 6: Verify 8 phase3 PNGs**

```bash
python3 -c "
import glob, os
from PIL import Image
files = sorted(glob.glob('Assets/AppIcon/splash_v4/phase3_f*.png'))
assert len(files) == 8
for f in files:
    img = Image.open(f)
    assert img.mode == 'RGBA' and img.size == (256, 256)
    w, h = img.size
    corners = [img.getpixel((0,0))[3], img.getpixel((w-1,0))[3], img.getpixel((0,h-1))[3], img.getpixel((w-1,h-1))[3]]
    assert max(corners) <= 32
    print(f'OK {os.path.basename(f)} corners={corners}')
"
```

- [ ] **Step 7: Commit phase 3**

```bash
git add Assets/AppIcon/splash_v4/phase3_f*.png scripts/splash_v4_state.json
git commit -m "feat(splash-v4): phase 3 자산 (8 frames, 응집)

PixelLab MCP. 3500ms 잔해 → 중앙 응집. spec §3.2 phase 3.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: Phase 4 Assets (4 frames — hold)

**Files:**
- Create: `Assets/AppIcon/splash_v4/phase4_f00.png` .. `phase4_f03.png` (4)

- [ ] **Step 1: Create base object**

```
mcp__pixellab__create_1_direction_object(
  description="primitive devastated grey-brown planet at center 160px diameter, base grey-brown crust #5C4A3A with dark crack veins #2E2520, deep crater shadows #1A1410, no atmosphere, transparent background, pixel art style",
  size=256,
  view="top-down"
)
```

Record `phase4.base_object_id`.

- [ ] **Step 2: Poll until COMPLETED**

- [ ] **Step 3: Queue animation (frame_count=4)**

```
mcp__pixellab__animate_object(
  object_id=<phase4.base_object_id>,
  animation_description="planet very slowly breathing, subtle scale pulse from 1.0 to 1.02 and back, almost imperceptible motion",
  frame_count=4
)
```

Record `phase4.animation_id`.

- [ ] **Step 4: Poll animation until COMPLETED**

- [ ] **Step 5: Download and postproc 4 frames**

For each `i` in `0..3`:
```bash
python3 scripts/postproc_splash_v4.py "<frame_i_url>" Assets/AppIcon/splash_v4/phase4_f$(printf '%02d' $i).png
```

- [ ] **Step 6: Verify 4 phase4 PNGs**

```bash
python3 -c "
import glob, os
from PIL import Image
files = sorted(glob.glob('Assets/AppIcon/splash_v4/phase4_f*.png'))
assert len(files) == 4
for f in files:
    img = Image.open(f)
    assert img.mode == 'RGBA' and img.size == (256, 256)
    w, h = img.size
    corners = [img.getpixel((0,0))[3], img.getpixel((w-1,0))[3], img.getpixel((0,h-1))[3], img.getpixel((w-1,h-1))[3]]
    assert max(corners) <= 32
    print(f'OK {os.path.basename(f)} corners={corners}')
"
```

- [ ] **Step 7: Commit phase 4**

```bash
git add Assets/AppIcon/splash_v4/phase4_f*.png scripts/splash_v4_state.json
git commit -m "feat(splash-v4): phase 4 자산 (4 frames, hold)

PixelLab MCP. 1000ms 행성 미세 호흡. spec §3.2 phase 4.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: Full Asset Verification (all 30 PNGs)

**Files:**
- Create: `scripts/verify_splash_v4.py`

- [ ] **Step 1: Write verify script**

Create `scripts/verify_splash_v4.py`:

```python
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
            # 1. `file` command — PNG signature
            try:
                result = subprocess.check_output(["file", f], text=True)
                if "PNG image data" not in result:
                    failures.append(f"{f}: file says {result.strip()}")
                    continue
            except subprocess.CalledProcessError as e:
                failures.append(f"{f}: file failed: {e}")
                continue

            # 2. PIL mode + size
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

            # 3. Corner alpha ≤ 32
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
```

- [ ] **Step 2: Run verification**

```bash
python3 scripts/verify_splash_v4.py
```

Expected: 30 OK lines + `30/30 PNGs verified OK`, exit code 0.

If FAIL: surface failures, do NOT proceed to Task 9. Re-run the relevant phase task to regenerate failing frames.

- [ ] **Step 3: Commit**

```bash
git add scripts/verify_splash_v4.py
git commit -m "feat(scripts): verify splash v4 asset acceptance (spec §6.3)

scripts/verify_splash_v4.py — 30 PNG batch 검증:
file PNG signature / PIL mode=RGBA / size=256x256 / corner alpha ≤ 32.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: Preview GIF Builder

**Files:**
- Create: `scripts/build_splash_v4_preview.py`
- Create: `Assets/AppIcon/splash_v4/splash_v4_preview.gif`

- [ ] **Step 1: Write GIF builder**

Create `scripts/build_splash_v4_preview.py`:

```python
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
# Total per-phase: 12*333=3996, 6*250=1500, 8*437=3496, 4*250=1000 ≈ 10000ms
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
            # GIF doesn't support full alpha; composite onto deep cosmic background
            # so transparent areas appear as the splash bg color (spec §2.3 BgOuter #040616)
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
        loop=0,  # infinite
        disposal=2,
        optimize=False,
    )

    size_bytes = OUTPUT.stat().st_size
    print(f"OK wrote {OUTPUT} ({size_bytes}B, {total_ms/1000:.2f}s loop)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: Run GIF builder**

```bash
python3 scripts/build_splash_v4_preview.py
```

Expected: `frames=30 total_duration=9988ms\nOK wrote ... .gif (NNNNB, 9.99s loop)`.

- [ ] **Step 3: Verify GIF**

```bash
python3 -c "
from PIL import Image
img = Image.open('Assets/AppIcon/splash_v4/splash_v4_preview.gif')
assert img.is_animated, 'not animated'
print(f'frames={img.n_frames} size={img.size} duration_first={img.info.get(\"duration\")}ms loop={img.info.get(\"loop\")}')
assert img.n_frames == 30
assert img.size == (256, 256)
total = 0
for i in range(img.n_frames):
    img.seek(i)
    total += img.info.get('duration', 0)
print(f'total_duration={total}ms')
assert 9900 <= total <= 10100, f'expected ~10000ms, got {total}'
print('OK')
"
```

Expected: `frames=30 size=(256, 256) ...\ntotal_duration=9988ms\nOK`.

- [ ] **Step 4: Commit**

```bash
git add scripts/build_splash_v4_preview.py Assets/AppIcon/splash_v4/splash_v4_preview.gif
git commit -m "feat(splash-v4): preview GIF + builder (spec §3.5)

scripts/build_splash_v4_preview.py — phase 별 per-frame duration 으로
30 PNG → 10s loop GIF 합성. PIL save_all + duration list.

GIF 는 alpha 부분을 deep cosmic #040616 (spec §2.3 BgOuter) 로 composite —
실제 게임에서 SplashScreen.cs 가 그릴 배경과 일치하게 미리보기.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: Push + Draft PR + User GIF Review Request

**Files:**
- (none new — push branch and open draft PR)

- [ ] **Step 1: Push branch**

```bash
cd /Users/hyun/projects/game_poc/.claude/worktrees/feat-splash-v4-assets
git push -u origin feat/splash-v4-assets
```

Expected: branch created remote.

- [ ] **Step 2: Open draft PR**

```bash
gh pr create --draft --title "feat(splash-v4): 자산 (30 PNG + GIF preview + BGM v2)" --body "$(cat <<'EOF'
## Summary

스플래시 v4 **자산 PR** (spec §7 PR #X+1). **DRAFT — 사용자 GIF 검토 OK 받기 전 머지 금지.**

- SSOT: \`docs/superpowers/specs/2026-05-21-splash-v4-design.md\` (PR #48 의존)
- 자산 30 PNG (4 phase, 256×256 RGBA, transparent bg)
- 검사용 \`splash_v4_preview.gif\` (10s loop, 3 fps 평균)
- \`splash_bgm_v2.wav\` (10s procedural, stdlib)
- 코드 PR (#X+2) 은 별도

## PixelLab MCP 활용 (메모리: feedback-prefer-pixellab-mcp)

Phase 별 \`create_1_direction_object\` + \`animate_object\` (총 8 호출).
\`no_background:true\` quirk 는 \`postproc_splash_v4.py\` 의 corner-color
alpha clipping (tol=12) 으로 우회.

## 검증 결과

- \`scripts/verify_splash_v4.py\` PASS — 30/30 PNG (file/RGBA/256×256/corner≤32)
- \`splash_v4_preview.gif\` 30 frames, ~10000ms loop
- \`splash_bgm_v2.wav\` 10.00s mono 44.1kHz 16-bit

## 사용자 검토 액션

1. **GIF 시청**: \`Assets/AppIcon/splash_v4/splash_v4_preview.gif\` 직접 열기 (혹은 GitHub PR 의 \"Files changed\" 탭에서 inline preview)
2. **BGM 시청**: \`Assets/Audio/splash_bgm_v2.wav\` 로컬 재생 (PR 의 GIF 옆 wav download)
3. spec §6.1 시각 체크리스트 6항목 (phase 1~4 의도 + 전환 + PNG 별 baked-in 없음) 확인

**검토 결과:**
- OK → 이 PR \"Ready for review\" 전환 후 머지 → 코드 PR (#X+2) 시작
- 수정 필요 → 어느 phase 가 어떻게 다른지 알려주기. 해당 phase 만 재호출

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Expected: `https://github.com/gusxodnjs/game_poc/pull/<NN>` printed.

- [ ] **Step 3: Surface PR URL to user**

Report to user:
- PR URL
- Path of `splash_v4_preview.gif` (worktree absolute)
- Path of `splash_bgm_v2.wav`
- spec §6.1 6항목 체크리스트 (요약 1줄씩)

Do NOT proceed past this step. Code PR (#X+2) is blocked on user approval of this asset PR.

---

## Self-Review Notes

- **Spec coverage**: §2 시퀀스 (Task 4~7 phase 자산), §3 PixelLab MCP (Task 4~7 + Pre-Task ToolSearch), §3.3 폴백 (각 phase task 의 polling + state file), §3.4 자산 경로 (File Structure 표), §3.5 GIF (Task 9), §4 BGM (Task 2), §6.3 자산 검증 (Task 8), §7 자산 PR 분기 (Task 10) — 모두 커버. §5 코드 변경 / §6.1 시뮬레이터 검증 / §6.2 회귀 방지 / §8 Open Question 은 코드 PR (#X+2) 범위라 본 plan 에서 제외.
- **Placeholder scan**: 모든 step 에 실제 코드/명령. "<frame_i_url>" 같은 placeholder 는 runtime 값 (MCP 응답에서 추출) — instruction 으로 충분.
- **Type consistency**: state file key 이름 (`phase{N}.base_object_id`, `phase{N}.animation_id`) 일관. file path 패턴 (`phase{P}_f{NN}.png`) 일관. CORNER_ALPHA_MAX=32 / ALPHA_CLIP_TOL=12 일관.

---

## Execution Handoff

**Plan saved to** `docs/superpowers/plans/2026-05-21-splash-v4-assets.md`.

Two execution options:

**1. Subagent-Driven (recommended)** — Fresh subagent per task with two-stage review. Each phase task (4–7) runs MCP calls + polling + download + commit independently. State file enables restart if a single phase fails.

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch with checkpoints. Faster but exhausts main context faster (MCP polling outputs are bulky).

Which approach?
