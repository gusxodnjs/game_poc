# Splash v2 — 빅뱅 시퀀스 스토리보드

> 상태: 기획 확정안 (문서). 자산/코드 작업은 후속 PR에서 진행.
> 적용 대상: `SplashScene.unity` 의 진화 8프레임(v1) → 빅뱅 12프레임(v2) 교체.
> 의존 메모리: `[[game-tone-primitive-planets]]`, `[[game-mechanic-gps-seed-planets]]`, `[[feedback-tone-brand]]`.
> 정합성 참조: [`docs/scenario_v2.md`](./scenario_v2.md) §1 (원시 행성 톤), §3 (PlanetIntroScene 흐름).

---

## 1. 시퀀스 개요

- **의미**: *무 → 빅뱅 → 파편 응집 → 황폐한 원시 행성*. 빈 우주에서 한 점이 터지고, 흩어진 파편이 다시 모여 식어 굳은 **원시 상태의 행성**으로 안착하는 단발 시퀀스.
- **총 길이**: **6,500ms + hold**. 마지막 프레임(`f11`)은 애니가 끝난 뒤에도 계속 화면에 머문다 (loop 금지). 이 hold 상태 위에 타이틀/서브카피/시작 버튼이 차례로 페이드인된다.
- **프레임 수**: 12장.
  - 파일명: `Assets/Splash/splash_anim_v2_bigbang_256_f00.png` ~ `f11.png`
  - 캔버스: **256 × 256** (PixelLab 단일 캔버스, 중앙 정렬)
  - 출력 후 Unity 측에서 화면 비율에 맞게 fit-contain 스케일.
- **재생 정책**:
  - 단발 (loop 금지). 끝 프레임 정지.
  - **스킵 가능**: 시퀀스 진행 중 화면 어디든 1탭 → 즉시 `f11` hold 상태로 점프하고, 타이틀/서브카피/버튼 페이드인 시퀀스를 곧바로 시작.
- **톤**: 원시 행성. **푸른 지구로 가지 않는다.** (참조: `[[game-tone-primitive-planets]]` — *"행성은 모두 원시 상태. 도시/해변/성숙된 숲 금지. 산책으로 회복하는 게 게임 핵심"*).
- **카피 위치 (애니 종료 후)**:
  - 타이틀: `작은정복자들`
  - 서브카피: `걸음마다, 자라나는 세계` ("걸음" 앵커 단어 — `[[feedback-tone-brand]]`)
  - 시작 버튼: `첫 걸음 떼기`

---

## 2. 프레임별 표 (12행)

각 프레임은 PixelLab 단일 이미지로 생성하며, 아래 **공통 스타일 키워드**를 모든 프롬프트에 prefix 로 붙인다.

> **공통 스타일 키워드 (모든 프레임 공통)**
> `pixel art, 256x256, dark cosmic background #080d1f, top-down centered composition, no text, no UI, no logo, limited palette, crisp pixel edges, no anti-aliasing`

| # | 단계 | 누적(ms) | duration(ms) | 시각요소 (한 줄) | PixelLab 프롬프트 (영문, 단계별 추가 키워드) |
|---|---|---:|---:|---|---|
| f00 | 무 (어둠) | 500 | 500 | 검은 우주 배경, 옅은 별 18개가 사방에 흩뿌려진 정적 상태 | `empty deep cosmic void, scattered 18 tiny faint stars in muted white #c8d2e0 at alpha 30%, absolute stillness, no central object, balanced negative space` |
| f01 | 작은 점 | 1,200 | 700 | 정중앙 1~2px 흰 광점, 4px 옅은 후광이 막 깨어남 | `single 2px pure white #ffffff pinpoint of light at exact center, faint 4px soft halo glow, background stars unchanged from f00, sense of awakening singularity` |
| f02 | 임계 광원 | 1,500 | 300 | 8px로 부푼 광점, 주변 별이 중심으로 빨려드는 가는 선 효과 | `swollen 8px bright white core at center with pale yellow #fff4c2 inner ring, surrounding stars stretched into thin radial streaks pulled toward center, accretion-pull tension, pre-explosion build-up` |
| f03 | 폭발 1 (코어) | 1,700 | 200 | 흰 코어 60px + 노란 링 96px (초신성 1차 섬광) | `supernova flash, intense 60px solid white #ffffff core, surrounding 96px concentric ring in saturated yellow #ffd755, hard pixel edges, first burst of light, stars washed out by overexposure` |
| f04 | 폭발 2 (확산) | 1,900 | 200 | 링 200px로 확산, 주황/적색 그라데이션 | `expanding shockwave ring at 200px diameter, gradient from inner orange #ff8a3a to outer deep red #c0341a, white core dimmed to 80px, ring edge crisp pixel-by-pixel, debris particles starting to detach from ring` |
| f05 | 폭발 3 (잔광) | 2,200 | 300 | 화면 가득 옅은 적등색 잔광, 작은 파편 점들이 사방으로 비산 | `full-screen faint red-orange afterglow at #5a2418 low alpha, central core faded to 40px dull amber #a86030, dozens of small debris pixel dots scattering outward in all directions, post-explosion stillness creeping in` |
| f06 | 파편 비산 | 2,800 | 600 | 12~16개의 작은 갈색/회색 픽셀 덩어리가 외곽으로 흩어진 상태 | `12 to 16 small irregular debris chunks in muted brown #5C4A3A and grey #4a4a52, scattered across the canvas at outer 60-80% radius, central area nearly empty with only faint embers, afterglow fading to background black, sense of cooling aftermath` |
| f07 | 응집 1 (먼 끌림) | 3,400 | 600 | 파편들이 중심을 향해 안쪽으로 이동, 흐릿한 중력 후광 | `same debris chunks as f06 but now repositioned closer to center at 40-60% radius, faint circular gravitational halo glow at center in dim grey-brown #3a3028, subtle motion-trail hint behind each chunk pointing inward, accretion beginning` |
| f08 | 응집 2 (중간) | 4,200 | 800 | 중심에 불규칙한 작은 덩어리(50px) 가 형성됨 | `irregular 50px proto-mass at center forming from merged debris, rough lumpy silhouette in dark brown #4a3a2c with hot orange #d05030 magma cracks, a few remaining debris chunks still drifting inward at 30% radius, hot accretion glow rim` |
| f09 | 응집 3 (성형) | 5,000 | 800 | 덩어리가 거의 원형(100px), 표면에 균열·마그마 라인 | `roughly spherical 100px primitive planetoid at center, surface still hot with bright magma cracks in orange-red #e05a28 across darker grey-brown crust #5C4A3A, faint outer heat haze, debris fully absorbed, planet not yet cooled` |
| f10 | 식어가는 행성 | 5,800 | 800 | 행성 140px, 적황색 마그마가 점점 어두워짐 | `140px primitive planet at center, magma cracks dimmed from orange to deep ember #7a2818, crust dominant in grey-brown #5C4A3A with dark crack veins #2E2520, cooling surface, first crater shadows appearing, heat haze gone, background stars returning at low alpha` |
| f11 | 황폐 행성 (최종) | 6,500+ | hold | 회갈색/암회색 행성 160px, 분화구·균열·검은 그림자, 완전 정적 | `final 160px primitive desolate planet at center, base grey-brown crust #5C4A3A with dark crack veins #2E2520, deep crater shadows #1A1410, no magma glow, no atmosphere, subtle stone highlight #A89888 on one side hinting at distant starlight, dead silent surface, 18 background stars fully restored at original alpha, completely static and held` |

> **프레임 누적 시간 = 해당 프레임 종료 시점 (ms).** Unity 측에서는 `duration` 만큼 현재 프레임을 표시한 뒤 다음으로 넘어간다. `f11` 은 hold 이므로 별도 다음 프레임이 없다.

---

## 3. 타이틀 / 서브카피 / 시작 버튼 / 버전 타이밍

애니 시퀀스 종료(6,500ms) 후 다음 순서로 페이드인. 모든 페이드는 alpha 0 → 1 선형 보간.

| 구간 (ms) | 길이 (ms) | 동작 | 비고 |
|---:|---:|---|---|
| 0 – 6,500 | 6,500 | 빅뱅 시퀀스 (f00 → f11) | 끝나면 f11 hold |
| 6,500 – 7,300 | 800 | 타이틀 `작은정복자들` fadeIn | 위치: 화면 세로 상단 8% 라인, 가로 중앙. 행성 위쪽 여백에 배치. |
| 7,300 – 7,800 | 500 | 서브카피 `걸음마다, 자라나는 세계` fadeIn | 위치: 화면 세로 72% 라인, 가로 중앙. 행성 아래쪽. |
| 7,800 – 8,300 | 500 | 시작 버튼 `첫 걸음 떼기` + 버전 텍스트 fadeIn | 버튼은 fadeIn **종료 시점(8,300ms)** 부터 입력 활성. 그 전에는 탭 입력 무시 (스킵 탭은 시퀀스 구간에서만). |

- **스킵 시 타임라인**: 시퀀스 구간(0–6,500ms) 동안 탭 → 즉시 6,500ms 시점으로 점프 → 위 페이드인 시퀀스가 그 시점부터 정상 진행.
- **버전 텍스트**: 화면 우하단, 작은 회색(`#A89888` 의 alpha 60%). 예: `v0.2.0`.

---

## 4. 끝 프레임 톤 가이드 (f11)

PlanetIntroScene 의 카드 3종(Volcano / Ice / Desert) 과 시각적으로 **명확히 구분되는 "미정의 원시 행성"** 톤. 어떤 type 도 아닌 *일반화된 황폐 원시 행성*. 이는 의도된 분리이다 — IntroScene 에서 GPS 시드 기반으로 비로소 구체 type 이 결정되기 때문 (`[[game-mechanic-gps-seed-planets]]`).

### 4.1 행성 색 팔레트 (hex)

| 역할 | hex | 비고 |
|---|---|---|
| 메인 회갈색 (크러스트 베이스) | `#5C4A3A` | 행성 표면 dominant 색 |
| 균열 어두운 갈색 | `#2E2520` | 균열 / 크랙 라인 |
| 분화구 그림자 | `#1A1410` | 크레이터 깊은 그림자, 어두운 면 |
| 별빛 액센트 (선택) | `#A89888` | 한쪽 림 라이트 (먼 별빛 반사) |

### 4.2 배경과의 관계

| 화면 | 배경색 | 메모 |
|---|---|---|
| SplashScene (v2) | `#080d1f` | 본 문서. 별 18개 alpha 변동 (§5). |
| PlanetIntroScene | `#0a1022` | 시나리오 v2 §3 기준. |

두 배경 모두 깊은 우주 톤. 스플래시의 `#080d1f` 가 IntroScene 의 `#0a1022` 보다 약간 더 어둡고 차가운 청흑색. f11 의 행성 팔레트는 두 배경 모두에 안착되도록 채도를 낮춰 설계.

### 4.3 카드 3종과의 시각 구분

| 항목 | f11 (스플래시 최종) | Volcano 카드 | Ice 카드 | Desert 카드 |
|---|---|---|---|---|
| dominant 색조 | 회갈색 무톤 | 적등색 마그마 | 청백색 빙하 | 황토색 사막 |
| 표면 활동 | 정적 (마그마 X) | 활화산 / 용암 | 얼음 균열 | 모래 / 바람 결 |
| 메시지 | "정의되지 않은 원시 상태" | "불의 원시" | "얼음의 원시" | "마름의 원시" |

f11 은 셋 중 어느 것도 아닌 *원형(原型)* 처럼 보여야 한다. 빅뱅 직후의 "아직 어떤 행성이 될지 결정되지 않은 상태" — 다음 씬(IntroScene) 에서 GPS 시드로 구체화된다는 서사적 연결.

---

## 5. 효과 옵션 (T4 코드 작업 참고)

후속 클라이언트 작업(`unity-ios-client-dev`) 의 참고용 효과 명세. **MVP 필수 / 선택 옵션** 으로 분리.

### 5.1 MVP 필수

| 효과 | 구간 | 명세 |
|---|---|---|
| 스킵 입력 | 0 – 6,500ms | 화면 어디든 1탭 → `f11` hold 즉시 점프 + 카피 페이드인 시퀀스 시작. |
| 프레임 hold | 6,500ms+ | `f11` 은 다음 프레임 없음. Unity 측 sprite swap 종료. |

### 5.2 선택 옵션 (가능하면 추가)

| 효과 | 구간 | 명세 |
|---|---|---|
| 카메라 셰이크 | f03 – f05 사이 (1,700 – 2,200ms) | ±3px 노이즈 셰이크, 총 300ms. 폭발 임팩트 강조. |
| 배경색 펄스 | f03 – f05 (1,700 – 2,200ms) | `#080d1f` → `#1f0d08` (적색 펄스) → `#080d1f` 선형 보간. 폭발 빛이 우주를 잠시 물들인다. |
| 별 알파 펄스 | f03 – f05 | 배경 별 alpha 0.3 → 0.2 로 낮춤 (폭발에 압도). f06 부터 원복. |
| 행성 회전 | f11 hold 중 | 매우 느린 회전 (한 바퀴 60s 이상) 또는 정지 중 택1. **기본은 정지** 권장 — "정적인 원시" 톤 강화. |

> 위 옵션은 PixelLab 자산을 건드리지 않고 Unity 측 코드/머티리얼로만 구현 가능한 후처리다. PixelLab 프레임 자체는 §2 표 그대로 정적 12장.

---

## 6. `docs/scenario_v2.md` 정합성 체크

- [x] **Splash → PlanetIntroScene 흐름 유지** — 빌드 씬 순서는 그대로 `[Splash, PlanetIntro, Hello]`. 시퀀스 길이만 변경. (scenario v2 §3)
- [x] **PlanetIntroScene 의 GPS 시드 자동 결정 컨셉 침해 없음** — 스플래시는 어떤 행성 type 도 미리 보여주지 않는다. f11 의 "정의되지 않은 원시 상태" 가 다음 씬에서 type 결정의 여지를 남긴다. (`[[game-mechanic-gps-seed-planets]]`)
- [x] **원시 행성 톤 일관성** — f11 은 마그마/얼음/모래 어느 것도 아닌 회갈색 무톤. 푸른 지구 X, 도시 X, 성숙된 숲 X. (scenario v2 §1 원시 행성 톤, `[[game-tone-primitive-planets]]`)
- [x] **카피 톤 일관성** — 타이틀/서브카피/버튼 카피 모두 자연·시간·신체 어휘. "걸음" 앵커 단어 1회 등장 (서브카피). SF 어휘 0. (`[[feedback-tone-brand]]`)
- [x] **단발 재생** — loop 금지. 한 번의 우주 탄생을 보여주고 끝.

---

## 7. 후속 PR 분기 (참고)

본 문서는 **기획안 확정**까지만. 실제 작업은 아래 후속 PR 로 분리한다 (`[[workflow-pr-per-task]]`):

1. **자산 PR** (`pixellab-asset-designer`) — `splash_anim_v2_bigbang_256_f00~f11.png` 12장 생성, `Assets/Splash/` 배치, `.meta` 커밋.
2. **클라이언트 PR** (`unity-ios-client-dev`) — `SplashScreen.cs` 의 frame array / duration array 를 v2 12프레임 + 누적 타이밍 표로 교체. 스킵 입력 / 카피 페이드인 / (선택) 셰이크·펄스 구현.
3. **삭제 PR** (선택) — v1 진화 8프레임 자산이 더 이상 참조되지 않는 게 확인되면 별도 PR 로 삭제.

각 PR 은 본 문서를 명세 출처(SSOT) 로 참조한다.

---

## 8. 한계 및 결정 필요 사항 (Open Questions)

| # | 항목 | 현재 결정 | 비고 |
|---|---|---|---|
| Q1 | f11 행성 회전 여부 | 정지 (기본) | 톤 강화 우선. 추후 사용자 피드백 시 재검토. |
| Q2 | 셰이크/펄스 적용 여부 | 선택 (§5.2) | 클라이언트 작업 공수에 따라 결정. MVP 는 미적용 가능. |
| Q3 | 사운드 | 본 PoC 범위 외 | `[[project-poc-scope]]` 사운드 제외 확인. 후속 빌드에서 검토. |
| Q4 | 버전 텍스트 노출 정책 | 우하단 상시 | 디버그/릴리즈 동일. |
