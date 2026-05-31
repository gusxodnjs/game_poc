---
name: walker-walk-frame-recipe
description: 정면 4프레임 walk cycle을 64x64 player 프레임으로 만드는 PixelLab+후처리 레시피
metadata:
  type: project
---

플레이어 정면 walk 프레임(`Assets/characters/walker_front_idle_frame{0..3}_64x64.png`)은
PixelLab `create_character` + walk 템플릿 애니메이션으로 생성한다.

**Why:** 기존 4프레임은 미묘한 breathing idle이라 "걷는다"는 느낌이 약했음. 다리가
실제로 번갈아 step하는 자연스러운 walk + 화면에서 더 크게 보이길 원함(#52).

**How to apply:**
- `create_character(view="low top-down", proportions=chibi, n_directions=4)` —
  chibi red-cap kid identity(빨간 모자/큰 머리/빨간 셔츠/파란 반바지/빨흰 운동화).
  size=56 -> 캔버스 80x80.
- `animate_character(template_animation_id="walk", directions=["south"])` —
  walk 템플릿이 south 6프레임 반환(N+1 quirk 아님, 그냥 6). east/west/north 불필요.
- backblaze frame URL은 urllib에 **403**(브라우저 UA 필요) -> curl `-A Mozilla/5.0`로 받기.
- 후처리 `scripts/postproc_walker_walk.py`: 6프레임 중 phase [0,1,3,4] 선택
  (contact / right-step / contact / left-step = 다리 번갈아 step + 부드러운 loop).
- **공유 scale**(가장 큰 프레임 bbox 기준)으로 전 프레임 동일 px/unit -> 크기 펄싱 없음.
- **bbox 바닥을 고정 baseline(Y=63)에 앵커** -> 디딘 발이 항상 같은 줄, 수직 jitter 없음.
  걷기 중엔 디딘 발이 늘 최하단 픽셀이라 이 방식이 성립.
- TARGET_H=62로 캔버스를 거의 꽉 채움(폭은 ~28px라 64에서 클리핑 없음). 기존 ~51px보다 큼.
- 파일명/.meta GUID 유지, PNG 픽셀만 덮어씀. 검증 strip: `/tmp/walk_strip.png`.

관련: [[git-case-mismatch-assets-staging]] (PNG는 소문자 `assets/`로 stage),
[[flat-autotile-mask-composite-recipe]] (같은 #52 작업의 path 쪽).
