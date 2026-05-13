# Characters

플레이어 캐릭터 픽셀 스프라이트. Genesis 지시서의 본 시안 후보 (C) "산책자 시선"과 일관된 톤. 1인 캐릭터, 성별 중립적.

사용자 피드백("더 귀엽게")을 반영해 **chibi/kawaii 마스코트 스타일**로 리메이크 (`feat/pixellab-walker-cute-and-anim` 브랜치).

## 정적 스프라이트

| 파일 | 사이즈 | 미리보기 |
|---|---|---|
| `walker_front_64x64.png` | 64×64 | ![walker_front](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-walker-cute-and-anim/assets/characters/walker_front_64x64.png) |
| `walker_side_64x64.png` | 64×64 | ![walker_side](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-walker-cute-and-anim/assets/characters/walker_side_64x64.png) |

**프롬프트 (cute remake)**

- front: `super cute adorable chibi walker character mascot, front view, big round head, large sparkling expressive eyes, tiny rounded body, warm friendly smile, soft pastel casual outfit, rounded shapes, kawaii style, bright cheerful colors, clean pixel art, transparent background`
- side: `super cute adorable chibi walker character mascot, side view, big round head, large sparkling eyes, tiny rounded body, mid-step walking pose, soft pastel casual outfit, rounded shapes, kawaii style, bright cheerful colors, clean pixel art, transparent background`

## 애니메이션

PixelLab `/v1/animate-with-text` 사용, 64×64, 4프레임. 시트는 가로 4프레임 (256×64).

| 동작 | 프레임 시트 | 개별 프레임 |
|---|---|---|
| `walker_side_walk` (side, walking) | ![sheet](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-walker-cute-and-anim/assets/characters/walker_side_walk_sheet_64x64.png) | frame0~3 (`walker_side_walk_frame{0..3}_64x64.png`) |
| `walker_front_idle` (gentle bob) | ![sheet](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-walker-cute-and-anim/assets/characters/walker_front_idle_sheet_64x64.png) | frame0~3 (`walker_front_idle_frame{0..3}_64x64.png`) |

### Unity Animator 적용 가이드

1. **Import 설정** — sheet PNG 선택 → Inspector
   - Texture Type: `Sprite (2D and UI)`
   - Sprite Mode: `Multiple`
   - Pixels Per Unit: `64` (혹은 게임 표준값)
   - Filter Mode: `Point (no filter)`
   - Compression: `None`
2. **Sprite Slicer** — Sprite Editor → Slice → Type `Grid By Cell Size`, Pixel Size `64 × 64` → Slice → Apply
3. **Animation Clip** — 슬라이스된 sprite 4개를 Project view에서 선택 → Hierarchy의 GameObject에 드래그 → Animation Clip 자동 생성 (e.g. `Walker_Side_Walk.anim`)
   - Sample Rate: `8` fps (4 프레임 × 2 = 0.5초 사이클) 권장. 빠른 걷기는 `12 fps`.
4. **Animator State** — Animator window → 새 State 추가 → Motion에 Clip 할당 → 파라미터 Bool/Trigger로 전이 설정
   - 예: `IsWalking` Bool로 `Idle ↔ Walk` 전이

## 메타데이터

- 생성일: 2026-05-14 (cute remake)
- API: `POST https://api.pixellab.ai/v1/generate-image-pixflux` (정적), `POST https://api.pixellab.ai/v1/animate-with-text` (애니메이션)
- 호출 수: 2 (정적) + 2 (애니메이션) = 4
- 응답 시간: 정적 평균 ~30.7s, 애니메이션 평균 ~42.4s
- 비용: $0.00 (응답 `usage.usd` 합계 — 베타/무료 티어)
- 생성 스크립트: `scripts/gen_cute_walker.py`, `scripts/gen_animations.py`
- 상세 로그: `scripts/gen_cute_walker_result.json`, `scripts/gen_animations_result.json`
