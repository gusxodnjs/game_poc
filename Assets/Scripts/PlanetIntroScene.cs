// PlanetIntroScene.cs
// TERRA × BIOSPHERE PoC — GPS 1회 fix → 1km 셀 hash → 행성 자동 결정 → 카드 표시.
//
// 시나리오 v2 §1/§4/§8 — IMGUI 한 화면 구성.
// 위→아래:
//   1) §1 인트로 카피 (3줄, 화면 폭 80% 중앙)
//   2) 카드 Sprite (256×256, hue shift 적용)
//   3) 자동 이름 (1줄 큰 폰트)
//   4) base lore (2줄)
//   5) "여기서 첫 걸음" 버튼 — 1초 페이드 → §3 진입 시나리오 한 컷 → HelloScene
//
// GPS 처리:
//   1) Start 에서 _debugForceLatLon (Editor) 또는 PlayerPrefs("debug_force_seed_cell") 우선
//   2) 둘 다 없으면 Input.location.Start → 최대 15초 대기
//   3) 실패/타임아웃 시 PlayerPrefs("last_seed_cell") 폴백
//   4) 그것도 없으면 fallback seed (서울시청 좌표) — 사용자 경고 없음 (PoC §7)
//
// Safe area 적용 (SplashScreen 패턴 참조).

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlanetIntroScene : MonoBehaviour
{
    [Header("씬 전환")]
    public string nextScene = "HelloScene";

    [Header("표시 문구 (시나리오 v2 §1 / §6)")]
    [TextArea(3, 5)]
    public string introCopy = "지금 당신이 서 있는 자리에서,\n하나의 작은 세계가 깨어납니다.\n\n같은 자리로 다시 오면, 같은 세계가 당신을 기다립니다.";

    public string startButtonLabel = "여기서 첫 걸음";

    [Header("페이드")]
    public float fadeInDuration = 0.6f;
    [Tooltip("시작 버튼 → 진입 시나리오 표시 사이 페이드.")]
    public float fadeToScenarioDuration = 1.0f;
    [Tooltip("진입 시나리오 → HelloScene 사이 페이드 (탭 후).")]
    public float fadeToHelloDuration = 0.5f;
    [Tooltip("진입 시나리오 최소 노출 시간 (이전엔 탭 무시).")]
    public float scenarioMinDwellSeconds = 1.2f;

    [Header("GPS")]
    [Tooltip("권한 요청 후 fix 를 기다리는 최대 시간(초).")]
    public float gpsFixTimeoutSeconds = 15f;

    [Header("카드 Sprite (PocBuildPipeline 가 주입, 순서: Volcano, Ice, Desert)")]
    [Tooltip("런타임에서는 AssetDatabase 가 동작하지 않으므로 scene serialization 으로 직접 주입한다.")]
    public Sprite[] cardSpritesByType = new Sprite[3];

    [Header("디버그")]
#if UNITY_EDITOR
    [Tooltip("Editor 전용: 이 값이 (0,0) 이 아니면 GPS 대신 사용. x=lat, y=lon")]
    public Vector2 debugForceLatLon = new Vector2(37.5663f, 126.9779f); // 기본 = 서울시청
#endif
    [Tooltip("런타임 디버그 — PlayerPrefs 키 'debug_force_seed_cell' 가 있으면 GPS 무시.")]
    public bool useDebugSeedCellFromPrefs = true;

    // ─── 상태 ───
    private enum Phase
    {
        AwaitingFix,    // GPS / 디버그 좌표 확정 대기
        ShowingPlanet,  // 카드 + 이름 + lore + 버튼
        FadingToScenario,
        ShowingScenario,// 진입 한 컷
        FadingToHello,
    }
    private Phase _phase = Phase.AwaitingFix;
    private float _phaseStartTime;
    private float _alpha;
    private string _gpsStatus = "위치 확인 중…";
    private PlanetInstance _planet;

    // ─── 스타일 캐시 ───
    private GUIStyle _introStyle;
    private GUIStyle _nameStyle;
    private GUIStyle _nameShadowStyle;
    private GUIStyle _loreStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _statusStyle;
    private GUIStyle _scenarioStyle;
    private GUIStyle _scenarioHintStyle;

    private void Start()
    {
        Debug.Log("[POC] PlanetIntroScene.Start");

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0a, 0x10, 0x22, 0xff);
        }

        _phaseStartTime = Time.time;
        StartCoroutine(DetermineCurrentPlanet());
    }

    private IEnumerator DetermineCurrentPlanet()
    {
        // 1순위: 런타임 디버그 키 (TestFlight 시연용).
        if (useDebugSeedCellFromPrefs)
        {
            string forced = PlayerPrefs.GetString("debug_force_seed_cell", "");
            if (!string.IsNullOrEmpty(forced))
            {
                ulong seed = PlanetSeed.SeedFromCellId(forced);
                if (seed != 0UL)
                {
                    AcceptPlanet(seed, forced, "debug_prefs");
                    yield break;
                }
            }
        }

#if UNITY_EDITOR
        // 2순위: Editor 디버그 좌표.
        if (debugForceLatLon != Vector2.zero)
        {
            double lat = debugForceLatLon.x;
            double lon = debugForceLatLon.y;
            ulong seed = PlanetSeed.Compute(lat, lon);
            string cellId = PlanetSeed.ToCellId(lat, lon);
            AcceptPlanet(seed, cellId, "editor_debug_latlon");
            yield break;
        }
#endif

        // 3순위: 실제 GPS.
        _gpsStatus = "위치 권한 확인 중…";

#if UNITY_IOS || UNITY_ANDROID
        if (!Input.location.isEnabledByUser)
        {
            Debug.Log("[POC] PlanetIntroScene: GPS disabled by user");
            yield return RestoreOrFallback("GPS 권한 없음");
            yield break;
        }
#endif

        if (Input.location.status != LocationServiceStatus.Running)
        {
            Input.location.Start(5f, 5f);
        }
        _gpsStatus = "위치 측정 중…";

        float waited = 0f;
        while (Input.location.status == LocationServiceStatus.Initializing && waited < gpsFixTimeoutSeconds)
        {
            yield return new WaitForSeconds(0.5f);
            waited += 0.5f;
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            Debug.Log("[POC] PlanetIntroScene: GPS fix failed (status=" + Input.location.status + ")");
            yield return RestoreOrFallback("위치 확인 실패");
            yield break;
        }

        var data = Input.location.lastData;
        ulong realSeed = PlanetSeed.Compute(data.latitude, data.longitude);
        string realCell = PlanetSeed.ToCellId(data.latitude, data.longitude);
        AcceptPlanet(realSeed, realCell, "gps_fix");
    }

    private IEnumerator RestoreOrFallback(string reason)
    {
        _gpsStatus = reason + " — 마지막 자리 복원 시도";
        if (GameSession.Instance.TryRestoreLastPlanet())
        {
            _planet = GameSession.Instance.CurrentPlanet;
            AssignSpriteFromInspector(_planet);
            EnterShowingPlanet();
            yield break;
        }

        // fallback — 서울시청. PoC §7: 경고 없이 base volcano 표시.
        const double fallbackLat = 37.5663;
        const double fallbackLon = 126.9779;
        ulong seed = PlanetSeed.Compute(fallbackLat, fallbackLon);
        string cellId = PlanetSeed.ToCellId(fallbackLat, fallbackLon);
        AcceptPlanet(seed, cellId, "fallback_seoul");
    }

    private void AcceptPlanet(ulong seed, string cellId, string source)
    {
        _planet = PlanetGenerator.Generate(seed);
        _planet.cellId = cellId;
        AssignSpriteFromInspector(_planet);
        GameSession.Instance.SetPlanet(_planet);
        Debug.Log("[POC] PlanetIntroScene.AcceptPlanet (" + source + "): " +
                  _planet.displayName + " seed=" + seed + " cell=" + cellId);
        EnterShowingPlanet();
    }

    /// <summary>
    /// Inspector serialized cardSpritesByType 에서 행성 타입에 맞는 sprite 를 할당.
    /// 비어 있으면 (Editor) PlanetGenerator.LoadAndAssignSprite (AssetDatabase) 로 폴백.
    /// </summary>
    private void AssignSpriteFromInspector(PlanetInstance planet)
    {
        if (planet == null) return;
        int idx = (int)planet.type;
        if (cardSpritesByType != null && idx >= 0 && idx < cardSpritesByType.Length && cardSpritesByType[idx] != null)
        {
            planet.cardSprite = cardSpritesByType[idx];
            return;
        }
        // 폴백 — Editor 의 AssetDatabase 경유 (빌드에서는 null 로 남음).
        PlanetGenerator.LoadAndAssignSprite(planet);
    }

    private void EnterShowingPlanet()
    {
        _phase = Phase.ShowingPlanet;
        _phaseStartTime = Time.time;
        _alpha = 0f;
    }

    private void Update()
    {
        switch (_phase)
        {
            case Phase.AwaitingFix:
                // 페이드 인만 진행 (상태 라벨 가독성 확보)
                _alpha = Mathf.Clamp01((Time.time - _phaseStartTime) / fadeInDuration);
                break;

            case Phase.ShowingPlanet:
                _alpha = Mathf.Clamp01((Time.time - _phaseStartTime) / fadeInDuration);
                break;

            case Phase.FadingToScenario:
                {
                    float t = (Time.time - _phaseStartTime) / Mathf.Max(0.01f, fadeToScenarioDuration);
                    _alpha = Mathf.Clamp01(1f - t);
                    if (t >= 1f)
                    {
                        _phase = Phase.ShowingScenario;
                        _phaseStartTime = Time.time;
                        _alpha = 0f;
                    }
                }
                break;

            case Phase.ShowingScenario:
                _alpha = Mathf.Clamp01((Time.time - _phaseStartTime) / 0.5f);
                break;

            case Phase.FadingToHello:
                {
                    float t = (Time.time - _phaseStartTime) / Mathf.Max(0.01f, fadeToHelloDuration);
                    _alpha = Mathf.Clamp01(1f - t);
                    if (t >= 1f)
                    {
                        SceneManager.LoadScene(nextScene);
                    }
                }
                break;
        }
    }

    private void EnsureStyles()
    {
        if (_introStyle == null)
        {
            _introStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                fontStyle = FontStyle.Normal,
            };
            _introStyle.normal.textColor = new Color(0.92f, 0.96f, 1f, 1f);

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            };
            _nameStyle.normal.textColor = Color.white;
            _nameShadowStyle = new GUIStyle(_nameStyle);
            _nameShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.55f);

            _loreStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
            };
            _loreStyle.normal.textColor = new Color(0.88f, 0.92f, 0.98f, 1f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            _buttonStyle.normal.textColor = Color.white;

            _statusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
            };
            _statusStyle.normal.textColor = new Color(0.8f, 0.85f, 0.92f, 1f);

            _scenarioStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
            };
            _scenarioStyle.normal.textColor = new Color(0.95f, 0.97f, 1f, 1f);

            _scenarioHintStyle = new GUIStyle(_scenarioStyle);
            _scenarioHintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
        }
    }

    private void OnGUI()
    {
        EnsureStyles();

        // Safe area (SplashScreen 패턴).
        Rect safe = Screen.safeArea;
        float safeX = safe.x;
        float safeY = Screen.height - (safe.y + safe.height);
        float safeW = safe.width;
        float safeH = safe.height;

        // 적응형 폰트 크기.
        int introFont = Mathf.Clamp((int)(safeW / 26f), 14, 36);
        int nameFont = Mathf.Clamp((int)(safeW / 14f), 22, 64);
        int loreFont = Mathf.Clamp((int)(safeW / 26f), 14, 32);
        int buttonFont = Mathf.Clamp((int)(safeW / 18f), 18, 52);
        int statusFont = Mathf.Clamp((int)(safeW / 24f), 16, 36);
        int scenarioFont = Mathf.Clamp((int)(safeW / 20f), 18, 44);

        _introStyle.fontSize = introFont;
        _nameStyle.fontSize = nameFont;
        _nameShadowStyle.fontSize = nameFont;
        _loreStyle.fontSize = loreFont;
        _buttonStyle.fontSize = buttonFont;
        _statusStyle.fontSize = statusFont;
        _scenarioStyle.fontSize = scenarioFont;
        _scenarioHintStyle.fontSize = Mathf.Max(12, scenarioFont - 8);

        switch (_phase)
        {
            case Phase.AwaitingFix:
                DrawAwaiting(safeX, safeY, safeW, safeH);
                break;
            case Phase.ShowingPlanet:
            case Phase.FadingToScenario:
                DrawPlanetCard(safeX, safeY, safeW, safeH);
                break;
            case Phase.ShowingScenario:
            case Phase.FadingToHello:
                DrawScenario(safeX, safeY, safeW, safeH);
                break;
        }

        GUI.color = Color.white;
    }

    private void DrawAwaiting(float sx, float sy, float sw, float sh)
    {
        GUI.color = new Color(1f, 1f, 1f, _alpha);
        Rect rect = new Rect(sx + sw * 0.1f, sy + sh * 0.45f, sw * 0.8f, sh * 0.2f);
        GUI.Label(rect, _gpsStatus, _statusStyle);
    }

    private void DrawPlanetCard(float sx, float sy, float sw, float sh)
    {
        if (_planet == null) return;

        // 상단 인트로 카피 — 화면 폭 80% 중앙, 상단 6%~24%.
        Rect introRect = new Rect(sx + sw * 0.1f, sy + sh * 0.06f, sw * 0.8f, sh * 0.20f);
        GUI.color = new Color(1f, 1f, 1f, _alpha);
        GUI.Label(introRect, introCopy, _introStyle);

        // 카드 — 가로 중앙, 세로 중앙(살짝 위). 폭/높이 짧은 변 50%.
        float cardSide = Mathf.Min(sw * 0.62f, sh * 0.42f);
        float cardX = sx + (sw - cardSide) * 0.5f;
        float cardY = sy + sh * 0.30f;
        Rect cardRect = new Rect(cardX, cardY, cardSide, cardSide);

        if (_planet.cardSprite != null)
        {
            // GUI.color 로 hue shift 의 단순 근사 — base tint 와 hueShift 만큼 약하게 토글.
            // PoC 단계: Shader 도입 없이 GUI 색 곱셈으로 분위기 변주.
            Color tint = ApplyHueShift(_planet.baseTint, _planet.hueShift);
            // 카드 원본을 너무 어둡게 만들지 않도록 tint 강도 제한 (35% 혼합).
            Color blended = Color.Lerp(Color.white, tint, 0.35f);
            blended.a = _alpha;
            GUI.color = blended;
            GUI.DrawTexture(cardRect, _planet.cardSprite.texture, ScaleMode.ScaleToFit, true);
            GUI.color = new Color(1f, 1f, 1f, _alpha);
        }
        else
        {
            // 카드 미로드 — 빈 박스 + 행성 타입명.
            GUI.color = new Color(0.4f, 0.4f, 0.5f, 0.6f * _alpha);
            GUI.DrawTexture(cardRect, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, _alpha);
            GUI.Label(cardRect, "[" + _planet.type + "]", _statusStyle);
        }

        // 자동 이름 — 카드 바로 아래.
        float nameY = cardY + cardSide + sh * 0.025f;
        Rect nameRect = new Rect(sx, nameY, sw, nameFontHeight());
        // 그림자
        GUI.color = new Color(1f, 1f, 1f, _alpha);
        Rect nameShadow = new Rect(nameRect.x + 2f, nameRect.y + 2f, nameRect.width, nameRect.height);
        GUI.Label(nameShadow, _planet.displayName, _nameShadowStyle);
        GUI.Label(nameRect, _planet.displayName, _nameStyle);

        // Lore — 이름 아래.
        float loreY = nameY + nameFontHeight() + sh * 0.015f;
        Rect loreRect = new Rect(sx + sw * 0.08f, loreY, sw * 0.84f, sh * 0.13f);
        GUI.Label(loreRect, _planet.lore, _loreStyle);

        // 시작 버튼 — 하단 9% 위치.
        float btnW = Mathf.Min(sw * 0.7f, 460f);
        float btnH = Mathf.Clamp(_buttonStyle.fontSize * 2.4f, 56f, 120f);
        float btnX = sx + (sw - btnW) * 0.5f;
        float btnY = sy + sh - btnH - sh * 0.05f;
        Rect btnRect = new Rect(btnX, btnY, btnW, btnH);

        bool enabled = _phase == Phase.ShowingPlanet && _alpha >= 0.9f;
        GUI.enabled = enabled;
        if (GUI.Button(btnRect, startButtonLabel, _buttonStyle))
        {
            Debug.Log("[POC] PlanetIntroScene: '여기서 첫 걸음' tapped → scenario");
            _phase = Phase.FadingToScenario;
            _phaseStartTime = Time.time;
        }
        GUI.enabled = true;
    }

    private void DrawScenario(float sx, float sy, float sw, float sh)
    {
        if (_planet == null) return;

        GUI.color = new Color(1f, 1f, 1f, _alpha);

        // 진입 시나리오 — 화면 중앙.
        Rect rect = new Rect(sx + sw * 0.08f, sy + sh * 0.30f, sw * 0.84f, sh * 0.4f);
        GUI.Label(rect, _planet.introScenario, _scenarioStyle);

        // 탭 안내 (최소 dwell 경과 후 표시).
        float dwell = Time.time - _phaseStartTime;
        if (_phase == Phase.ShowingScenario && dwell >= scenarioMinDwellSeconds)
        {
            Rect hintRect = new Rect(sx, sy + sh - sh * 0.08f, sw, sh * 0.06f);
            GUI.Label(hintRect, "화면을 탭해 한 걸음 떼기", _scenarioHintStyle);

            // 전체 화면 탭 감지 — Unity IMGUI 는 모바일 터치를 MouseUp 으로 매핑.
            if (Event.current.type == EventType.MouseUp)
            {
                Debug.Log("[POC] PlanetIntroScene: scenario tap → " + nextScene);
                _phase = Phase.FadingToHello;
                _phaseStartTime = Time.time;
            }
        }
    }

    private float nameFontHeight()
    {
        return _nameStyle.fontSize * 1.5f;
    }

    /// <summary>
    /// base tint 에 hue shift 도(°) 적용. HSV 공간에서 hue 만 (shift/360) 회전.
    /// hue shift 가 0 이면 baseTint 그대로 반환.
    /// </summary>
    private static Color ApplyHueShift(Color baseTint, float hueShiftDegrees)
    {
        Color.RGBToHSV(baseTint, out float h, out float s, out float v);
        h = Mathf.Repeat(h + hueShiftDegrees / 360f, 1f);
        Color shifted = Color.HSVToRGB(h, s, v);
        shifted.a = baseTint.a;
        return shifted;
    }
}
