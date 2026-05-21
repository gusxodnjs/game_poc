# 스플래시 v4 — 고요한 우주 → 빅뱅 → 행성 형성 (10초)

> 상태: 디자인 확정안. SSOT.
> 의존 메모리: `[[game-tone-primitive-planets]]`, `[[feedback-prefer-pixellab-mcp]]`, `[[pixellab-no-background-quirk]]`, `[[pixellab-polling-quirk]]`, `[[workflow-pr-per-task]]`.
> v3 (`[[2026-05-21-splash-merge-design]]`) 의 코드/자산 모두 교체. v3 디자인 문서는 history 보존용.

---

## 1. 문제 정의 / 변경 동기

v3 (6.5초, 12 프레임) 폰 배포 후 사용자 피드백 (2026-05-21):

> *"애니메이션을 더 느리게. 아무것도 없는 고요한 우주에서 빅뱅이 일어나고 서서히 행성이 생기는 모습. 효과음이 아닌 배경음악."*

v3 한계:
- 총 6.5초로 짧아 빅뱅 임팩트 직전의 "고요" 가 없음 (1.5초 안에 점광 → 폭발)
- 12 프레임이라 motion 이 끊김 (특히 응집 phase 의 부드러움 부족)
- BGM 8초 — v3 시퀀스 (6.5초) 와 동기되지만 v4 (10초) 와 mismatch

## 2. 시퀀스 명세 (10초 / 30 프레임)

### 2.1 Phase 구성

| Phase | 시간 (ms) | frames | 의도 |
|---|---|---|---|
| **1. 고요 → 점광** | 0 ~ 4000 | 12 | 텅 빈 우주의 정적, 중앙에 미세한 빛이 서서히 강해짐. 사용자가 "고요" 를 체감하는 핵심 구간. |
| **2. 빅뱅** | 4000 ~ 5500 | 6 | 폭발 코어 + 외곽 충격파 ring 이 빠르게 확장. 시퀀스의 시각/오디오 peak. |
| **3. 응집** | 5500 ~ 9000 | 8 | 잔해/먼지가 중력으로 중앙에 끌려와 행성 본체 형성. 점진적 다크닝. |
| **4. hold** | 9000 ~ 10000 | 4 | 완성된 원시 행성이 천천히 호흡 (subtle scale 1.0↔1.02). 게임 진입 직전 정적. |

총 10000ms / 12+6+8+4 = 30 frames. 평균 333ms/frame.

### 2.2 Phase 사이 transition

각 phase 의 마지막 frame 과 다음 phase 의 첫 frame 사이 200ms cross-fade (SplashScreen.cs 코드 책임). 자산 자체는 phase 별 독립 시퀀스 — PixelLab MCP `animate_object` 의 frame loop 가 phase 내부 모션만 담당하기 때문.

### 2.3 단일 우주 원칙 (v3 §2.2 계승)

- 별은 절차적 30개 한 벌만 (SplashScreen.cs). PNG 안 별 baked-in 금지.
- PNG 는 transparent background RGBA — phase 별 base object 의 alpha PNG.
- PixelLab 의 `no_background:true` quirk 는 corner-color alpha clipping 후처리로 우회 (`[[pixellab-no-background-quirk]]`).

---

## 3. PixelLab MCP 자산 생성 (총 8 호출)

### 3.1 도구 선택 근거

`[[feedback-prefer-pixellab-mcp]]` 룰 적용 — `urllib` 직접 호출 대신 MCP 도구 우선. 가용 도구 schema 검토 결과:

- `animate_object` 는 호출당 최대 16 frames. 30 frames 한 호출 불가.
- `animate_object` 는 "이미 정의된 object 의 motion" 용도. 빅뱅 시퀀스 (없음 → 폭발 → 행성) 처럼 entity 자체가 변하는 흐름은 한 base 로 표현 불가.

→ **Phase-based hybrid**: phase 별로 `create_1_direction_object` (base) + `animate_object` (motion) 쌍. 4 phase × 2 = 8 MCP 호출.

### 3.2 Phase 별 MCP 호출 명세

각 호출은 `get_object` 폴링으로 완료 대기 (PixelLab MCP 의 async job pattern).

#### Phase 1 — 고요 → 점광 (12 frames)
```
create_1_direction_object(
  description="empty deep cosmic void, completely dark, single tiny faint point of pure white light at exact center, no stars, no objects, transparent background, pixel art style",
  size=256,
  view="top-down"
)
→ object_id_p1
animate_object(
  object_id=object_id_p1,
  animation_description="tiny point of light very slowly brightening and softly pulsating, gradual intensity increase from barely visible to small clear pinpoint",
  frame_count=12,
)
→ animation_id_p1, 12 frame PNGs
```

#### Phase 2 — 빅뱅 (6 frames)
```
create_1_direction_object(
  description="supernova explosion flash, intense bright white core 80px with saturated yellow #ffd755 shockwave ring 120px expanding outward, hard pixel edges, transparent background, pixel art style",
  size=256,
  view="top-down"
)
→ object_id_p2
animate_object(
  object_id=object_id_p2,
  animation_description="explosion shockwave ring rapidly expanding outward, core dimming as ring grows, intense burst of energy",
  frame_count=6,
)
→ animation_id_p2, 6 frame PNGs
```

#### Phase 3 — 응집 (8 frames)
```
create_1_direction_object(
  description="scattered debris and dust cloud at outer 60-80% radius in muted brown #5C4A3A and grey #4a4a52, faint circular gravitational halo glow at center in dim grey-brown, transparent background, pixel art style",
  size=256,
  view="top-down"
)
→ object_id_p3
animate_object(
  object_id=object_id_p3,
  animation_description="debris and dust slowly pulling inward toward center accreting into a forming mass, gravitational coalescence, gradual darkening",
  frame_count=8,
)
→ animation_id_p3, 8 frame PNGs
```

#### Phase 4 — hold (4 frames)
```
create_1_direction_object(
  description="primitive devastated grey-brown planet at center 160px diameter, base grey-brown crust #5C4A3A with dark crack veins #2E2520, deep crater shadows #1A1410, no atmosphere, transparent background, pixel art style",
  size=256,
  view="top-down"
)
→ object_id_p4
animate_object(
  object_id=object_id_p4,
  animation_description="planet very slowly breathing, subtle scale pulse from 1.0 to 1.02 and back, almost imperceptible motion",
  frame_count=4,
)
→ animation_id_p4, 4 frame PNGs
```

### 3.3 폴백 / 재시도 정책

- 각 `create_*` / `animate_*` 호출은 `get_object` 로 status=COMPLETED 까지 폴링 (max 5분, 5초 간격).
- PixelLab MCP 가 review status (multiple candidates) 를 반환하면 `select_object_frames(indices=[0])` 로 첫 후보 채택.
- 다운로드한 PNG 는 `[[pixellab-no-background-quirk]]` 후처리 (corner-color alpha clipping, tol=12) 일괄 적용.
- 각 frame 검증: file `PNG image data`, PIL mode='RGBA', size=256×256, corner alpha ≤ 32.

### 3.4 자산 경로

```
Assets/AppIcon/splash_v4/
├── phase1_f00.png .. phase1_f11.png   (12)
├── phase2_f00.png .. phase2_f05.png   (6)
├── phase3_f00.png .. phase3_f07.png   (8)
└── phase4_f00.png .. phase4_f03.png   (4)
```

총 30 PNG. .meta 파일은 자산 PR 머지 시 Unity 가 자동 생성. v3 의 `splash_anim_v2_bigbang_256_f*.png` 는 PR #X+2 (코드 PR) 에서 제거.

### 3.5 검사용 GIF (1차 산출물)

코드 작업 전 사용자 시각 검토용:

```
scripts/build_splash_v4_preview.py
→ Assets/AppIcon/splash_v4/splash_v4_preview.gif
```

- 30 frames 순서 (phase1 → 2 → 3 → 4) 합성
- **phase 별 frame duration 적용** (단일 fps 아님 — 실시뮬과 매칭):
  - phase1: 333ms/frame × 12 = 4000ms
  - phase2: 250ms/frame × 6 = 1500ms
  - phase3: 437ms/frame × 8 = 3500ms (마지막 frame 은 449ms 로 보정)
  - phase4: 250ms/frame × 4 = 1000ms
- loop = infinite (검토 편의)
- PIL 사용 (stdlib 외 PIL 만 의존, 기존 `gen_splash_v3_alpha.py` 와 동일). PIL `Image.save(..., save_all=True, append_images=..., duration=[...])` 의 per-frame duration 리스트 지원.

검사 OK 받기 전엔 코드 PR (#X+2) 시작 금지.

---

## 4. BGM (10초, gen_splash_bgm.py v2)

기존 `scripts/gen_splash_bgm.py` 의 stdlib procedural pattern 을 v4 시퀀스에 맞춰 재배치. `numpy` 없음, `wave`/`struct`/`math` 만.

### 4.1 구간 구조 (총 10000ms)

| 시간 (ms) | 내용 | 톤 |
|---|---|---|
| 0 ~ 4000 | **고요 → 점광** | 110Hz 드론, 페이드인 0.00 → 0.10. 매우 잔잔 |
| 4000 ~ 4500 | **빅뱅 직전 임계** | 220Hz → 880Hz 글리산도 sweep, 텐션 빌드업 |
| 4500 ~ 5500 | **폭발** | 화이트 노이즈 burst + 55Hz boom, peak loudness |
| 5500 ~ 9000 | **응집** | 110/165/220Hz 화음 패드, LFO + detune, 점진 안정화 |
| 9000 ~ 10000 | **hold + fade** | 패드 유지, 페이드아웃 0.10 → 0.00 |

### 4.2 출력

- `Assets/Audio/splash_bgm_v2.wav` (44.1kHz mono 16-bit PCM, 10.00s)
- 기존 `splash_bgm_v1.wav` 는 보존 (v3 코드가 참조 중인 단계 있음, 코드 PR 머지 후 별도 정리)
- 라이선스: CC0 1.0 (self-generated)

### 4.3 효과음 정책

사용자 명시: "효과음이 아닌 배경음악". BGM 외 추가 sfx (탭 사운드 등) 도입 금지. 스킵 입력 (탭 → 즉시 f29 점프) 시에도 sfx 없음.

---

## 5. 코드 변경 (SplashScreen.cs — 검사 후 PR #X+2)

### 5.1 frame array — phase 별 4개 배열

v3 의 단일 `Frames[12]` → v4 의 phase 별 배열 4개:
```csharp
public Texture2D[] phase1Frames; // 12
public Texture2D[] phase2Frames; // 6
public Texture2D[] phase3Frames; // 8
public Texture2D[] phase4Frames; // 4
```

이유: phase 별 frame duration / cross-fade 처리가 단일 array 보다 명확. `SetupSplashScene` 메뉴 의 frame 주입 로직을 phase 별 path 패턴 (`Assets/AppIcon/splash_v4/phase{p}_f{nn}.png`) 으로 갱신.

### 5.2 타이밍 상수 갱신

```csharp
public float displayDuration = 10.0f;          // v3: 6.5f
private const float Phase1EndMs   = 4000f;     // 고요 → 점광 끝
private const float Phase2StartMs = 4000f;     // 빅뱅 시작 (= 기존 ExplosionStartMs)
private const float Phase2EndMs   = 5500f;     // 빅뱅 끝
private const float Phase3EndMs   = 9000f;     // 응집 끝
private const float Phase4EndMs   = 10000f;    // hold 끝
private const float CrossFadeMs   = 200f;      // phase 사이
```

v3 의 `ExplosionStartMs=1500f`, `ExplosionEndMs=2200f` 는 `Phase2StartMs`, `Phase2EndMs` 로 의미 보존.

### 5.3 phase frame 인덱스 매핑

`_elapsedMs` → `(phase, localFrame)` 변환 헬퍼:
```csharp
private (int phase, int frame, float blend) FrameAt(float ms);
// 반환: phase = 1..4, frame = phase 내 인덱스, blend = 다음 frame 으로의 lerp 0..1
```

200ms cross-fade 는 `blend` 가 1.0 에 접근할 때 다음 frame 과 alpha 합성.

### 5.4 BGM 교체

`splashBgm` 필드의 AudioClip reference 를 `splash_bgm_v2` 로 변경. `SetupSplashScene` 메뉴에서 자동 주입.

### 5.5 v3 보존 항목

- 라디얼 그라데이션 배경 (L1) — phase1 의 깊은 어둠도 그라데이션 위에 깔림
- 히트 헤이즈 (L4) — phase2~3 에 적용 (alpha 0.6 → 0.15)
- 별 24개 (v3 그대로 유지 — 시퀀스 길이 비례로 늘릴 근거 약함, 단일 우주 원칙상 보수적 유지)
- 행성 박스 75%
- iOS 무음 스위치 우회 (`_ConfigurePlaybackAudioSession`)
- 검은 화면 회귀 방지 (`EnsureTextures` 는 첫 `OnGUI` 에서)

### 5.6 LateUpdate 정리

v3 에서 이미 제거됨. v4 도 LateUpdate 사용 금지 (펄스/모션은 OnGUI 단에서만).

---

## 6. 검증 체크리스트

### 6.1 시각 (사용자 GIF 검토 단계)

자산 PR 머지 전:
- [ ] phase1 12 frames: 화면이 거의 검고 중앙에 미세한 점이 서서히 밝아진다
- [ ] phase2 6 frames: 폭발 코어 + 외곽 ring 이 화면 절반 이상으로 확장
- [ ] phase3 8 frames: 잔해가 중앙으로 모여 행성 본체가 형성
- [ ] phase4 4 frames: 회색-갈색 원시 행성이 거의 정적으로 보임 (미세 호흡)
- [ ] phase 사이 시각 점프가 어색하지 않다 (코드 cross-fade 가 보정해줄 정도)
- [ ] 모든 frame 에 PNG 안 별 baked-in 없음

코드 PR 머지 후 (iOS 시뮬레이터/실기):
- [ ] 총 길이 10초, 스킵 입력 시 즉시 다음 scene 으로 전환
- [ ] BGM 이 시퀀스와 동기 (4초 잔잔 → 5초 peak → 9초 안정 → fade)
- [ ] 배경 라디얼 그라데이션이 phase 전체에 자연스럽게 깔림
- [ ] 별 30개 절차적, PNG 안 별 없음
- [ ] 행성 hold 단계에서 미세 호흡 (1.0↔1.02 scale)

### 6.2 회귀 방지

- [ ] iOS 무음 스위치 우회 동작
- [ ] 검은 화면 회귀 없음 (`EnsureTextures` 호출 위치 점검)
- [ ] v3 의 별/히트헤이즈/그라데이션 모두 작동 (단지 30 frames 시퀀스만 교체)
- [ ] 효과음 (sfx) 추가되지 않음 — BGM 만

### 6.3 자산

- [ ] 30 PNG 모두 `file` → PNG image data
- [ ] 30 PNG 모두 PIL mode='RGBA', size 256×256, corner alpha ≤ 32
- [ ] `splash_v4_preview.gif` 10초 loop
- [ ] `splash_bgm_v2.wav` 10.00s, 44.1kHz mono 16-bit

---

## 7. PR 분기 (`[[workflow-pr-per-task]]`)

1. **PR #X — 디자인 문서** (본 PR) — 본 spec 만 추가.
2. **PR #X+1 — 자산 (자산 PR)** — 30 PNG + GIF preview + BGM v2 wav + 자산 생성 스크립트 (`scripts/gen_splash_v4_assets.py`, `scripts/build_splash_v4_preview.py`, `scripts/gen_splash_bgm.py` 수정).
   - **머지 전 사용자 GIF 검토 의무**. 검토 OK 받기 전 PR draft 유지.
3. **PR #X+2 — 코드 통합 (SplashScreen.cs)** — frame array, 타이밍 상수, phase 매핑, BGM reference 변경.

자산 PR 머지 → 코드 PR 시작. 자산 없으면 코드 시각 검증 불가.

---

## 8. Open Questions

| # | 항목 | 현재 결정 | 비고 |
|---|---|---|---|
| Q1 | phase1 의 점광 위치 | 정확한 화면 중앙 | GPS 발견 직전 단계라 추상적 중심. 행성 등장 위치와 일치. |
| Q2 | phase 사이 cross-fade 시간 | 200ms | 너무 길면 phase 경계 흐려짐, 너무 짧으면 점프. 시뮬레이터 검증 후 100~300ms 조정 가능. |
| Q3 | GIF preview FPS | 3 fps (333ms/frame) | 실시뮬과 동일. 더 빠른 검토를 원하면 6 fps loop (5초) 도 고려. |
| Q4 | PixelLab 결과 거부 시 정책 | 자산 PR 단계에서 재생성, 코드 PR 와 무관 | 사용자 시각 검토에서 phase X 가 부적합하면 해당 phase 만 재호출. |
| Q5 | v3 자산 (`splash_anim_v2_bigbang_256_f*.png`) 처리 | PR #X+2 (코드 PR) 에 포함해 제거 | git history 에 남음. .meta 도 함께 제거. v3 splash_bgm_v1.wav 도 동일 PR 에서 제거. |
