# 스플래시 통합 디자인 — v2 → v3 (배경·애니메이션 통합)

> 상태: 디자인 확정안. 후속 작업은 별도 PR 로 분리 (자산 → 코드).
> SSOT 참조: 본 문서. 기존 [`splash_v2_bigbang.md`](../../splash_v2_bigbang.md) 는 시퀀스 기획(타이밍/프레임 의미) SSOT 로 유지하되, **§2 프롬프트 prefix** 및 **§5 효과 명세** 는 본 문서가 덮어쓴다.
> 의존 메모리: `[[game-tone-primitive-planets]]`, `[[feedback-tone-brand]]`, `[[workflow-pr-per-task]]`, `[[pixellab-polling-quirk]]`.

---

## 1. 문제 정의

현재 `SplashScene.unity` 에서 **배경과 애니메이션 영역이 시각적으로 분리되어 보임**. 사용자 피드백 (2026-05-21):

> *"배경화면과 안에 애니메이션 영역이 너무 따로 놀고있어 … 배경 이미지와 애니메이션 요소를 하나로 합쳐줘. 배경색이 부드럽게 연결되도록."*

### 1.1 분리감의 기술적 원인 (코드/자산 분석 결과)

| # | 원인 | 위치 | 영향 |
|---|---|---|---|
| 1 | **PNG 12프레임 안에 우주 배경 baked-in** | `docs/splash_v2_bigbang.md:34` 공통 prefix: `dark cosmic background #080d1f, scattered stars` | 256×256 캔버스 자체가 자기 우주를 들고 있음. 60% 박스로 그려지면 "별을 가진 사각 패치" 가 떠 있는 모양. |
| 2 | **별 두 벌 공존** | (a) PNG 안 baked 18개, (b) `SplashScreen.cs:178` 절차적 18개 | 위치/밀도 다른 두 별이 겹쳐 경계가 도드라짐. |
| 3 | **솔리드 단색 배경** | `SplashScreen.cs:143` `BgBase = #080d1f` | 깊이감 없음. 평평한 색 위에서 텍스처 영역만 미세한 색차로 도드라짐. |
| 4 | **행성 박스가 작음** | `SplashScreen.cs:514` `planetSide = min(safeW, safeH) * 0.6f` | 60%. 박스 가장자리와 배경 사이의 빈 영역이 넓어 "박스 vs 바깥" 구분이 강조됨. |

### 1.2 결정 — A안 (풀 통합)

브레인스토밍 결과 두 안 중 **A안 채택** (사용자 결정, 2026-05-21):

> *"A — 풀 통합 (추천). 12프레임 재생성(alpha 배경) + 라디얼 그라데이션 배경 + 별 한 벌 + 행성 75% + 히트 헤이즈. PR 2개(자산/코드)."*

대안 B (자산 유지, 코드로 박스 cover) 는 폭발 임팩트 링이 잘리는 부작용 때문에 기각.

---

## 2. 통합 디자인 명세

### 2.1 화면 레이어 구조 (z-order 아래 → 위)

```
[L0] 카메라 SolidColor #040616 (외곽 fallback 색)
[L1] OnGUI: 라디얼 그라데이션 풀스크린 텍스처 1장
       - 중앙: #1a1228 (살짝 따뜻한 보라/적흑)
       - 외곽: #040616 (deep cosmic)
       - 폭발 구간(1500~2200ms): 중앙색이 #5a2418 적등색으로 펄스 (sin 0→1→0)
[L2] OnGUI: 절차적 별 24개 (한 벌만)
       - 사이즈 2~5px 다양화
       - 폭발 구간 alpha 0.2 다운 유지
[L3] OnGUI: 행성 프레임 (alpha PNG, 75% 박스)
       - 현재 프레임 + 다음 프레임 cross-fade (기존 enableCrossFade)
       - 응집 단계 스케일/부유 (기존 enableCoalesceScale / enableCoalesceFloat)
       - 폭발 구간 ±3px 셰이크 (기존 enableShake)
[L4] OnGUI: 히트 헤이즈 (행성 중심 라디얼 그라데이션, alpha 0~0.6)
       - 행성과 같은 위치/스케일에 1.4× 크기로 그림 (행성 가장자리 너머로 번짐)
       - 폭발~파편 구간(1500~2800ms): alpha 강 (0.6)
       - 응집 단계(2800~5800ms): 점진 감소 (0.4 → 0.15)
       - f11 hold (5800ms+): alpha 0.15 정적
[L5] OnGUI: 타이틀 / 서브카피 / 시작 버튼 / 버전 (기존 그대로)
```

### 2.2 단일 우주 원칙

> **별은 한 벌만 존재한다.** PNG 프레임 안에는 별이 없고, 절차적 별 24개가 화면 전체에 산재한다. 행성/폭발/파편 콘텐츠만 PNG로 합성된다.

이로써 (a) PNG 박스 가장자리에 별 밀도 변화가 없고, (b) 배경 그라데이션이 화면 전체에 끊김 없이 적용된다.

### 2.3 행성과 배경의 광학적 연결

- **행성 박스 75%** (기존 60% → 75%). 박스 가장자리와 화면 가장자리 사이 여백 축소.
- **히트 헤이즈 (L4)** — 행성 중심에서 외곽으로 alpha 그라데이션. 행성 가장자리와 배경 사이에 부드러운 광학적 다리 역할. 폭발 구간엔 강하게, hold 구간엔 잔잔하게.
- **폭발 펄스가 배경 전체에 영향** — 기존 `BgPulse` (`SplashScreen.cs:144`) 를 카메라 단일색이 아닌 **L1 라디얼 그라데이션의 중심색** 에 적용. 폭발 빛이 우주 전체로 번지는 효과.

---

## 3. 자산 변경 (PixelLab 12프레임 재생성)

### 3.1 파일명 정책

**기존 파일을 덮어쓴다** (overwrite). 이유:
- `SplashScreen.cs` 의 frame array 참조 변경 불필요
- `PocBuildPipeline.SetupSplashScene` 의 frame 주입 로직 그대로 유지
- 자산 PR 머지만으로 코드는 자동으로 새 프레임 사용

파일 경로 (`Assets/AppIcon/` — 기존 위치 유지):
```
Assets/AppIcon/splash_anim_v2_bigbang_256_f00.png
Assets/AppIcon/splash_anim_v2_bigbang_256_f01.png
... 
Assets/AppIcon/splash_anim_v2_bigbang_256_f11.png
```

`.meta` 파일은 기존 GUID 유지 (PNG만 교체).

### 3.2 프롬프트 prefix 변경

`docs/splash_v2_bigbang.md:34` 의 공통 스타일 키워드:

**기존 (v2)**:
```
pixel art, 256x256, dark cosmic background #080d1f, top-down centered composition,
no text, no UI, no logo, limited palette, crisp pixel edges, no anti-aliasing
```

**신규 (v3 통합)**:
```
pixel art, 256x256, transparent background, no background fill, no stars, no cosmos,
top-down centered composition, no text, no UI, no logo, limited palette,
crisp pixel edges, no anti-aliasing, alpha channel only for object content
```

### 3.3 프레임별 프롬프트 패치

각 프레임 프롬프트(`docs/splash_v2_bigbang.md:38~49`) 에서:
- `background stars unchanged from f00` 등 별 언급 → 삭제
- `scattered 18 tiny faint stars in muted white` → 삭제 (f00)
- `surrounding stars stretched into thin radial streaks pulled toward center` → `surrounding empty space showing only debris streaks pulled toward center` (f02)
- `stars washed out by overexposure` → `bright flash dominates the frame` (f03)
- `background stars returning at low alpha` → 삭제 (f10)
- `18 background stars fully restored at original alpha` → 삭제 (f11)

전체 패치 다이프는 자산 PR 의 description 에 포함.

### 3.4 검증 (PixelLab polling quirk 대응)

메모리: `[[pixellab-polling-quirk]]` — PixelLab MCP가 in-progress JSON error body를 PNG로 저장할 수 있음. 배치 후:

```bash
for f in Assets/AppIcon/splash_anim_v2_bigbang_256_f*.png; do
  file "$f"   # 모두 "PNG image data" 여야 함
done
```

또한 알파 채널 존재 확인:
```bash
for f in Assets/AppIcon/splash_anim_v2_bigbang_256_f*.png; do
  python3 -c "from PIL import Image; print('$f', Image.open('$f').mode)"
  # 모두 'RGBA' 여야 함
done
```

---

## 4. 코드 변경 (SplashScreen.cs)

### 4.1 새로 추가할 헬퍼

#### `Texture2D _bgGradient` (라디얼 그라데이션)
- `Start()` 에서 1회 생성 (128×128 또는 256×256 정사각 텍스처)
- 중심에서 외곽으로 alpha 그라데이션이 아닌 **색 보간** (alpha=1, RGB가 중심색→외곽색)
- 펄스 색은 매 프레임 변하므로 **두 텍스처** 를 미리 만들지 않고, 그라데이션 texture는 한 장 + `GUI.color` tint 로 펄스 적용
  - 또는 더 단순하게: `_bgGradientBase` (중앙#1a1228, 외곽 #040616) + `_bgGradientPulse` (중앙 #5a2418, 외곽 #040616) 두 장, L1에서 cross-fade

#### `Texture2D _heatHaze`
- `Start()` 에서 1회 생성 (128×128)
- 중심 alpha 1.0 → 외곽 alpha 0.0 라디얼 그라데이션 (RGB는 흰색)
- L4 에서 `GUI.color` 로 alpha 변조 후 행성 위치에 그림

#### `Color BgCenterBase` / `BgCenterPulse` / `BgOuter`
```csharp
private static readonly Color BgCenterBase  = new Color32(0x1a, 0x12, 0x28, 0xff);
private static readonly Color BgCenterPulse = new Color32(0x5a, 0x24, 0x18, 0xff);
private static readonly Color BgOuter       = new Color32(0x04, 0x06, 0x16, 0xff);
```

기존 `BgBase` / `BgPulse` (`SplashScreen.cs:143-144`) 는 카메라 ClearFlags 용도로 외곽색(`BgOuter`)으로 변경 또는 제거.

### 4.2 `OnGUI()` 변경 (L1 + L4 신규)

```csharp
private void OnGUI()
{
    EnsureStyles();
    EnsureTextures(); // 신규

    // ... safe area 계산 (기존)

    float globalAlpha = ...; // 기존

    // [L1] 라디얼 그라데이션 배경 (풀스크린)
    DrawBackgroundGradient(globalAlpha);

    // [L2] 절차적 별 24개
    float starAlphaScale = ...; // 기존
    DrawSparkles(globalAlpha, starAlphaScale);

    // [L3 준비] 행성 Rect 를 미리 계산 — L4 헤이즈가 같은 Rect 를 참조
    Rect planetRect = ComputePlanetRect(safeX, safeY, safeW, safeH);

    // [L4] 히트 헤이즈 (행성 중심) — 행성 PNG 아래에 깔리도록 L3 앞에 그림
    //      L3 보다 먼저 그려야 행성 본체가 헤이즈 위에 올라옴 (행성이 핵심 콘텐츠)
    DrawHeatHaze(globalAlpha, planetRect);

    // [L3] 행성 프레임 (75%, alpha PNG)
    if (frames != null && frames.Length > 0)
    {
        // ... 기존 cross-fade / shake / scale / float 로직
        // planetSide / planetX / planetY 는 ComputePlanetRect 와 일관되어야 함.
        // (또는 ComputePlanetRect 가 셰이크/부유 오프셋 적용 전 Rect 를 반환하고,
        //  L3 내부에서 셰이크 오프셋을 더해 최종 그림 — 헤이즈는 셰이크 따라가지 않게)
    }

    // [L5] 타이틀 / 서브카피 / 버튼 / 버전 (기존)
}
```

### 4.3 `DrawBackgroundGradient(globalAlpha)`

```csharp
private void DrawBackgroundGradient(float globalAlpha)
{
    // 폭발 펄스 비율 (0~1)
    float pulseT = 0f;
    if (enableBgPulse)
    {
        float ms = _elapsedMs;
        if (ms >= ExplosionStartMs && ms <= ExplosionEndMs)
        {
            float t = (ms - ExplosionStartMs) / (ExplosionEndMs - ExplosionStartMs);
            pulseT = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0
        }
    }

    Rect full = new Rect(0, 0, Screen.width, Screen.height);

    // 베이스 그라데이션 (alpha = 1)
    GUI.color = new Color(1f, 1f, 1f, globalAlpha);
    GUI.DrawTexture(full, _bgGradientBase, ScaleMode.StretchToFill);

    // 펄스 그라데이션 cross-fade (alpha = pulseT)
    if (pulseT > 0f)
    {
        GUI.color = new Color(1f, 1f, 1f, pulseT * globalAlpha);
        GUI.DrawTexture(full, _bgGradientPulse, ScaleMode.StretchToFill);
    }

    GUI.color = Color.white;
}
```

카메라는 fallback 외곽색 `BgOuter` 로 clear. 그라데이션 텍스처가 풀스크린을 덮어 클리어 색은 거의 안 보임.

### 4.4 `DrawHeatHaze(globalAlpha, planetRect)`

```csharp
private void DrawHeatHaze(float globalAlpha, Rect planetRect)
{
    float ms = _elapsedMs;
    float hazeAlpha;
    if (ms < ExplosionStartMs)
    {
        hazeAlpha = 0f; // 폭발 전엔 헤이즈 없음
    }
    else if (ms <= 2800f) // 폭발 ~ 파편
    {
        hazeAlpha = 0.6f;
    }
    else if (ms <= 5800f) // 응집
    {
        float t = (ms - 2800f) / 3000f;
        hazeAlpha = Mathf.Lerp(0.4f, 0.15f, t);
    }
    else // hold
    {
        hazeAlpha = 0.15f;
    }

    if (hazeAlpha <= 0f) return;

    // 1.4× 크기로 행성 중심에 그림 (가장자리 너머로 번짐)
    float expand = 1.4f;
    float hw = planetRect.width * expand;
    float hh = planetRect.height * expand;
    float hx = planetRect.x - (hw - planetRect.width) * 0.5f;
    float hy = planetRect.y - (hh - planetRect.height) * 0.5f;
    Rect hazeRect = new Rect(hx, hy, hw, hh);

    GUI.color = new Color(1f, 0.85f, 0.65f, hazeAlpha * globalAlpha);
    GUI.DrawTexture(hazeRect, _heatHaze, ScaleMode.StretchToFill);
    GUI.color = Color.white;
}
```

### 4.5 `EnsureTextures()`

```csharp
private bool _texturesReady;
private Texture2D _bgGradientBase;
private Texture2D _bgGradientPulse;
private Texture2D _heatHaze;

private void EnsureTextures()
{
    if (_texturesReady) return;
    _bgGradientBase  = BuildRadialColorTexture(128, BgCenterBase,  BgOuter);
    _bgGradientPulse = BuildRadialColorTexture(128, BgCenterPulse, BgOuter);
    _heatHaze        = BuildRadialAlphaTexture(128, new Color(1f, 0.85f, 0.65f, 1f));
    _texturesReady = true;
}

private static Texture2D BuildRadialColorTexture(int size, Color center, Color outer)
{
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    tex.filterMode = FilterMode.Bilinear; // 그라데이션은 bilinear (별은 별도 그림)
    tex.wrapMode = TextureWrapMode.Clamp;
    var pixels = new Color[size * size];
    float cx = (size - 1) * 0.5f;
    float cy = (size - 1) * 0.5f;
    float maxR = Mathf.Sqrt(cx * cx + cy * cy);
    for (int y = 0; y < size; y++)
    {
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx, dy = y - cy;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float t = Mathf.Clamp01(r / maxR);
            // SmoothStep 으로 부드럽게
            t = Mathf.SmoothStep(0f, 1f, t);
            pixels[y * size + x] = Color.Lerp(center, outer, t);
        }
    }
    tex.SetPixels(pixels);
    tex.Apply();
    return tex;
}

private static Texture2D BuildRadialAlphaTexture(int size, Color rgb)
{
    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    tex.filterMode = FilterMode.Bilinear;
    tex.wrapMode = TextureWrapMode.Clamp;
    var pixels = new Color[size * size];
    float cx = (size - 1) * 0.5f;
    float cy = (size - 1) * 0.5f;
    float maxR = Mathf.Sqrt(cx * cx + cy * cy);
    for (int y = 0; y < size; y++)
    {
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx, dy = y - cy;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float t = Mathf.Clamp01(r / maxR);
            // 가운데 1, 외곽 0, 부드러운 fall-off
            float a = 1f - Mathf.SmoothStep(0f, 1f, t);
            pixels[y * size + x] = new Color(rgb.r, rgb.g, rgb.b, a);
        }
    }
    tex.SetPixels(pixels);
    tex.Apply();
    return tex;
}
```

### 4.6 별 개수 변경

`SplashScreen.cs:88` 기본값 `sparkleCount = 18` → `24`. 사이즈 범위도 `Random.Range(2f, 4f)` → `Random.Range(2f, 5f)` 로 확장.

### 4.7 카메라 ClearFlags

`SplashScreen.cs:170-172`:
```csharp
cam.clearFlags = CameraClearFlags.SolidColor;
cam.backgroundColor = BgBase; // 기존 #080d1f
```
→
```csharp
cam.clearFlags = CameraClearFlags.SolidColor;
cam.backgroundColor = BgOuter; // 신규 #040616 (외곽 fallback)
```

### 4.8 행성 박스 사이즈

`SplashScreen.cs:514`:
```csharp
float planetSide = Mathf.Min(safeW, safeH) * 0.6f;
```
→
```csharp
float planetSide = Mathf.Min(safeW, safeH) * 0.75f;
```

위 오프셋(`- safeH * 0.05f`, `SplashScreen.cs:518`)은 그대로 유지 — 텍스트 영역 확보.

### 4.9 LateUpdate 의 펄스 코드 제거

`SplashScreen.cs:266-283` 의 `LateUpdate` 는 카메라 배경색에 직접 펄스를 적용했지만, 이제 그라데이션 펄스가 OnGUI 단에서 처리. **카메라 배경은 정적 `BgOuter`** 로 유지. `LateUpdate` 자체를 삭제 또는 비워둠.

---

## 5. 시퀀스 / 카피 타이밍 (변경 없음)

§5의 시퀀스 타이밍(6500ms + 카피 페이드인 1800ms), 카피 문구(`작은정복자들` / `걸음마다, 자라나는 세계` / `첫 걸음 떼기`), 스킵 입력, BGM 정책은 `splash_v2_bigbang.md` §3 그대로 유지. 본 문서는 시각 합성만 변경.

---

## 6. 검증 체크리스트 (구현 완료 시)

### 6.1 시각

- [ ] iPhone 시뮬레이터에서 실행 시 배경 + 행성 영역의 경계가 보이지 않는다 (페퍼링 / 사각 패치 없음).
- [ ] 배경에 라디얼 그라데이션이 적용되어 화면 중앙이 외곽보다 살짝 밝다.
- [ ] 별 한 벌만 보인다 (PNG 안 별 없음, 절차적 24개만).
- [ ] 폭발 구간(1.5~2.2초) 동안 배경 전체가 적등색으로 펄스한다.
- [ ] 행성 가장자리에 부드러운 히트 헤이즈가 보이며 배경과 자연스럽게 이어진다.
- [ ] 행성이 화면 짧은 변의 75% 크기로 그려진다.

### 6.2 회귀 방지

- [ ] 시퀀스 타이밍 (6500ms + 1800ms 카피 페이드인) 변경 없음.
- [ ] 스킵 입력 동작 (탭 → f11 즉시 점프) 유지.
- [ ] BGM 재생 / 페이드아웃 동작 유지.
- [ ] iOS 무음 스위치 우회 (`_ConfigurePlaybackAudioSession`) 유지.
- [ ] 검은 화면 회귀 없음 (커밋 19c08ee 패턴: 동적 텍스처 생성으로 인한 SRGB/format 이슈) — `EnsureTextures()` 가 `Start()` 가 아닌 첫 `OnGUI()` 에서 호출되도록 보장.

### 6.3 자산

- [ ] 12프레임 모두 `file` 명령으로 `PNG image data` 출력.
- [ ] 12프레임 모두 PIL/Pillow 로 mode='RGBA'.
- [ ] PNG 안에 별이 보이지 않음 (육안 확인 또는 alpha 분포 분석).

---

## 7. PR 분기 (`[[workflow-pr-per-task]]`)

1. **PR #X — 디자인 문서 (본 PR)** — 본 문서 추가, splash_v2_bigbang.md §2 의 prefix 라인 패치.
2. **PR #X+1 — 자산 재생성** (`pixellab-asset-designer`) — 12프레임 alpha PNG 재생성, 검증, 커밋.
3. **PR #X+2 — 코드 통합** (`unity-ios-client-dev`) — `SplashScreen.cs` 변경 (라디얼 그라데이션 / 히트 헤이즈 / 행성 75% / 별 24개 / LateUpdate 정리).

각 PR 은 본 문서를 SSOT 로 참조한다. 자산 PR 머지 후 코드 PR 시작 — 자산이 없으면 코드 시각 검증 불가.

---

## 8. Open Questions (구현 중 결정)

| # | 항목 | 현재 결정 | 비고 |
|---|---|---|---|
| Q1 | 라디얼 그라데이션 텍스처 사이즈 | 128×128 | 풀스크린 stretch 이므로 더 클 필요 없음. bilinear filter 로 부드럽게. |
| Q2 | 펄스를 cross-fade 로 vs 색 lerp 로 | cross-fade (두 텍스처) | 색 lerp 는 매 프레임 Color.Lerp 후 텍스처 한 장 tint — 단순하지만 셰이더 같은 효과가 약함. cross-fade 가 시각적으로 더 자연스러움. |
| Q3 | 히트 헤이즈 색 | `#ffd9a6` (`Color(1, 0.85, 0.65)`) | 따뜻한 황색. 폭발 잔광 톤과 매치. |
| Q4 | 행성 75% 가 너무 큰가 | 75% (기본) | 텍스트 영역 (상단 8% 타이틀 / 하단 28% 서브카피+버튼) 침범 없음. 시뮬레이터 검증 후 70~80% 사이 조정 가능. |
