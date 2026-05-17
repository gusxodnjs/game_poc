# World (Planet)

## 행성 카드 (PlanetIntroScene)

시나리오 v1/v2 확정 — 3종 행성 카드 (256×256). `PlanetIntroScene`에서 SpriteRenderer로 한 카드 자리에 1개씩 표시.

세 카드 모두 동일한 시각 포맷 (구형 행성 + 투명 배경 + 원시 황량함) 으로 통일됨. 도시/식생/생명 환경 0건 — "아직 아무것도 자라지 않은" 원시 상태.

| 파일 | 사이즈 | 컨셉 | 색조 base | 미리보기 |
|---|---|---|---|---|
| `planet_card_volcano_256.png` | 256×256 | 잠든 화산 — 식은 검은 용암, 갈라진 틈의 잔열, 흰 김 | `#5C3B33` | ![volcano](planet_card_volcano_256.png) |
| `planet_card_ice_256.png` | 256×256 | 얼어붙은 평원 — 회청색 얼음, 정적, 긴 바람 | `#6E8AA0` | ![ice](planet_card_ice_256.png) |
| `planet_card_desert_256.png` | 256×256 | 메마른 들녘 — 황토 갈라진 땅, 마른 바람 | `#B89968` | ![desert](planet_card_desert_256.png) |

**프롬프트:**
- volcano: `pixel art round planet sphere, primitive volcanic world, surface texture of cooled black lava with cracks glowing faintly red, thin wisps of white steam, dark gray and reddish-brown tones #5C3B33, isolated round planet centered on transparent background, no buildings no vegetation no people no creatures, primordial wasteland, soft pixel shading, simple sphere shading`
- ice: `pixel art round planet sphere, primitive frozen world, surface texture of pale blue-gray ice plains with faint cracks and wind streaks, deep silence, cold tones #6E8AA0, isolated round planet centered on transparent background, no buildings no vegetation no people no creatures, primordial frozen wasteland, soft pixel shading, simple sphere shading`
- desert: `pixel art round planet sphere, primitive desert world, surface texture of ochre cracked dry earth and endless sand plains, warm sun, yellow-brown tones #B89968, isolated round planet centered on transparent background, no buildings no vegetation no palm trees no oasis no people no creatures, primordial barren wasteland, soft pixel shading, simple sphere shading`

**생성 메타:**
- 생성일: 2026-05-17
- 생성기: PixelLab `/v1/generate-image-pixflux` (스크립트 `scripts/gen_planet_cards.py`)
- 호출 수: 3 (첫 시도 모두 성공)
- 비용: $0.00 (보고된 usage.usd 기준)
- 후처리: corner-color 알파 클리핑 (PixelLab `no_background:true`가 솔리드 배경을 반환해서 강제 투명화)

**Unity Import 권장 (`PlanetIntroScene` SpriteRenderer):**
- Texture Type: Sprite (2D and UI)
- Filter Mode: Point (no filter)
- Compression: None
- Pixels Per Unit: 64
- Alpha is Transparency: ON
- Sprite Mode: Single
- 권장 scale: 1.0 (행성이 카드 영역 ~75% 차지하도록 256→Sprite Pivot=Center)

---

## TERRA 행성 (게임 진행 상태)

TERRA v7: "초기 회색 → 6종 모두 안치 시 옅은 녹색으로 변화" — 두 상태의 행성 에셋.

| 파일 | 사이즈 | 프롬프트 | 미리보기 |
|---|---|---|---|
| `planet_grey_128x128.png` | 128×128 | small grey planet sphere, isolated, simple shading, no atmosphere, pixel art, transparent background | ![grey](planet_grey_128x128.png) |
| `planet_green_128x128.png` | 128×128 | small green planet sphere with light vegetation patches, isolated, simple shading, pixel art, transparent background | ![green](planet_green_128x128.png) |

- 생성일: 2026-05-14
- 생성기: PixelLab `/v1/generate-image-pixflux`
- 호출 수: 2
- 비용: $0.00 (보고된 usage.usd 기준)

### 사용 의도

- `planet_grey`: 게임 시작 상태 (안치 0종)
- `planet_green`: 6종 안치 완료 상태 (전환 연출 대상)
- 본 제작 시 중간 단계 (1~5종) 보간 프레임 또는 추가 variant 필요 — 권고안 참조.
