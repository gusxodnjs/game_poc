# Characters

플레이어 캐릭터 픽셀 스프라이트. Genesis 지시서의 본 시안 후보 (C) "산책자 시선"과 일관된 톤. 1인 캐릭터, 성별 중립적.

## 톤 가이드 (PR #11 rebalance)

PR #9의 chibi 마스코트가 "너무 동물 같다"는 사용자 피드백을 반영해
**16-bit JRPG protagonist 톤**으로 재조정 (`feat/pixellab-walker-human-rebalance`).

- 4~5등신, 사람 비례 유지 (chibi 3등신 ❌)
- 청년/청소년 (20대 초반)
- 평범한 외출복 — 티셔츠 + 청바지 + 운동화
- 픽셀아트지만 인간임이 명확히 식별 가능
- 참조 톤: Stardew Valley · Pokemon Black/White · Earthbound 주인공

## 정적 스프라이트

| 파일 | 사이즈 | 미리보기 |
|---|---|---|
| `walker_front_64x64.png` | 64×64 | ![walker_front](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-walker-human-rebalance/assets/characters/walker_front_64x64.png) |
| `walker_side_64x64.png` | 64×64 | ![walker_side](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-walker-human-rebalance/assets/characters/walker_side_64x64.png) |

**프롬프트 (human rebalance)**

- front: `young adult human character in their early twenties, walker, front view standing pose facing camera, plain casual t-shirt and blue jeans, everyday sneakers, natural human body proportions four heads tall, 16-bit JRPG protagonist pixel art style like Pokemon Black White or Earthbound, Stardew Valley style human villager, neutral friendly face, short hair, clearly recognizable as a person, slight stylization, not chibi, not mascot, not animal, no big oversized head, transparent background, clean pixel art`
- side: 동일 베이스, `side view profile standing ready to walk`로 변경

`text_guidance_scale=10.0`, `no_background=true`. 1회 시도로 양호한 결과 확보 (재시도 없음).

## 애니메이션

PixelLab `/v1/animate-with-text` 사용, 64×64, 4프레임. 시트는 가로 4프레임 (256×64).
PR #9에서 만들어진 chibi 애니메이션을 새 human 정적 기반으로 재생성했다.

| 동작 | 프레임 시트 | 개별 프레임 |
|---|---|---|
| `walker_side_walk` (side, walking) | ![sheet](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-walker-human-rebalance/assets/characters/walker_side_walk_sheet_64x64.png) | frame0~3 (`walker_side_walk_frame{0..3}_64x64.png`) |
| `walker_front_idle` (gentle bob) | ![sheet](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-walker-human-rebalance/assets/characters/walker_front_idle_sheet_64x64.png) | frame0~3 (`walker_front_idle_frame{0..3}_64x64.png`) |

`walker_side_walk`는 첫 호출에서 프레임 0이 누운 자세로 나와 액션 문구를
`walking forward, upright posture, arms swinging, legs alternating steps`로 강화한 뒤 재생성했다.

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

- 생성일: 2026-05-14 (human rebalance, PR #11)
- API: `POST https://api.pixellab.ai/v1/generate-image-pixflux` (정적), `POST https://api.pixellab.ai/v1/animate-with-text` (애니메이션)
- 호출 수: 2 (정적) + 2 (애니메이션) + 1 (side_walk 재시도) = 5
- 응답 시간 합계: 약 ~178s
- 비용: **$0.00** (응답 `usage.usd` 합계 — 베타/무료 티어)
- 생성 스크립트: `scripts/gen_human_walker.py`, `scripts/gen_human_walker_anim.py`
- 상세 로그: `scripts/gen_human_walker_result.json`, `scripts/gen_human_walker_anim_result.json`

## 플레이어 마커 (지도 표시용)

피크민 블룸 스타일 지도 위에서 walker 캐릭터 발밑/주변에 겹쳐 그릴 보조 마커.
Unity Tilemap/지도 위에 walker 스프라이트와 함께 배치된다.

| 파일 | 사이즈 | 용도 |
|---|---|---|
| `player_shadow_32x16.png` | 32×16 | 캐릭터 발밑 타원 그림자. walker 발 중앙에 정렬해서 배치 |
| `player_accuracy_ring_64x64.png` | 64×64 | GPS 정확도가 낮을 때 캐릭터 둘레에 표시되는 얇은 파란 외곽선 원 |

**프롬프트 (player markers)**

- shadow: `pixel art top-down soft circular shadow ellipse, semi-transparent dark gray, 32x16 pixels, no background, transparent background, subtle edge fade, isolated, simple flat shape, centered horizontal oval`
- accuracy_ring: `pixel art thin blue circle outline, transparent center, light blue ring, 64x64 pixels, no background, transparent background, soft glow on edge, isolated, 1 to 2 pixel thick stroke, perfect circle, hollow center, no fill`

**기술 메모**

- PixelLab 최소 캔버스가 32×32라 그림자는 32×32로 생성 후 PIL nearest-neighbor 리샘플로 32×16 다운스케일 (`scripts/gen_player_markers.py`의 `postprocess`). 단색에 가까운 평탄한 그림자라 nearest 다운샘플로도 깨짐 없음.
- accuracy_ring은 64×64 직접 생성.
- 둘 다 `no_background=true`, `text_guidance_scale=10.0`. 1회 시도로 성공.
- 검증: `file` 명령 PNG 매직바이트 통과, `sips` pixelWidth/Height 정확 (32×16, 64×64), 파일 크기 436B/3181B (PixelLab 폴링 에러 본문 70B 케이스 아님).
- Unity Import 권장: Filter Mode `Point`, Compression `None`, Pixels Per Unit 64.
  - shadow의 pivot은 `Center` (캐릭터 발 좌표에 직접 배치).
  - accuracy_ring의 pivot도 `Center` (캐릭터 위치 = ring 중심).
  - shadow는 SpriteRenderer alpha를 0.4~0.6 정도로 낮추면 더 자연스러운 반투명감 (생성 결과가 거의 단색 짙은 회색이라 코드에서 alpha 조절 권장).

생성일: 2026-05-17. 스크립트: `scripts/gen_player_markers.py`. 로그: `scripts/gen_player_markers_result.json`. 비용: $0.00 (`usage.usd` 합계, 베타/무료 티어).

## 이전 시안 (참고)

- 원본 (PR #8, 평범 톤): `feat/pixellab-chars-tiles-ui` 브랜치
- chibi 마스코트 (PR #9, 너무 동물스러움): `feat/pixellab-walker-cute-and-anim` 브랜치
- **현재 (PR #11, 인간 비례)**: 이 브랜치 ✅
