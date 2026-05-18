using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스플래시 화면 v2 — "빅뱅 시퀀스".
/// 무 → 폭발 → 파편 응집 → 황폐한 원시 행성 의 단발 12프레임 시퀀스를 보여주고,
/// 시퀀스 종료 후 타이틀/서브카피/버튼이 순차 페이드인된다.
///
/// 사양 출처(SSOT): docs/splash_v2_bigbang.md
/// 의존: PR #34 (docs), PR #35 (12프레임 자산), PR #36 (BGM)
///
/// 핵심 변경 (v1 → v2):
/// - 12프레임 가변 타이밍 (총 6,500ms, 폭발 구간만 빠르고 응집은 묵직)
/// - 단발 재생 (loop 금지), 끝 프레임 f11 무한 hold
/// - 시퀀스 종료 후 타이틀/서브카피/버튼이 순차 페이드인 (8.3초 시점에 모두 등장)
/// - BGM 재생 (AudioSource, loop=false, vol=0.6)
/// - 스킵 입력: 시퀀스 진행 중 화면 1탭 → 즉시 끝 상태로 점프
/// - (선택) 카메라 셰이크 / 배경색 펄스 / 별 알파 펄스 — 폭발 구간 임팩트
/// - IMGUI 전용 (Canvas / UI Toolkit 사용 금지)
/// </summary>
public class SplashScreen : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // 시퀀스 타이밍 (docs/splash_v2_bigbang.md §2 표 그대로)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 프레임별 표시 시간 (ms). 인덱스 = 프레임 번호.
    /// 총 합 = 6,500ms. f11 은 nominal 700ms 지만 이후 무한 hold (loop 금지).
    /// </summary>
    private static readonly int[] FrameDurationsMs = new int[]
    {
        500,  // f00: 무 (어둠)            0    ~  500
        700,  // f01: 작은 점               500  ~ 1200
        300,  // f02: 임계 광원             1200 ~ 1500
        200,  // f03: 폭발 1 (코어)        1500 ~ 1700
        200,  // f04: 폭발 2 (확산)        1700 ~ 1900
        300,  // f05: 폭발 3 (잔광)        1900 ~ 2200
        600,  // f06: 파편 비산             2200 ~ 2800
        600,  // f07: 응집 1                2800 ~ 3400
        800,  // f08: 응집 2                3400 ~ 4200
        800,  // f09: 응집 3                4200 ~ 5000
        800,  // f10: 식어가는 행성        5000 ~ 5800
        700,  // f11: 황폐 행성 (최종)     5800 ~ 6500 (이후 hold)
    };

    /// <summary>시퀀스 총 길이 (ms). FrameDurationsMs 합과 일치해야 한다.</summary>
    private const int SequenceTotalMs = 6500;

    // 폭발 임팩트 구간 (셰이크 / 펄스 / 별 알파 다운)
    private const float ExplosionStartMs = 1500f; // f03 시작
    private const float ExplosionEndMs   = 2200f; // f05 끝

    // 카피 페이드인 시작 오프셋 (시퀀스 종료 = 0초 기준)
    private const float TitleFadeStartSec    = 0.0f;
    private const float TitleFadeDurSec      = 0.8f;
    private const float SubtitleFadeStartSec = 0.8f;
    private const float SubtitleFadeDurSec   = 0.5f;
    private const float ButtonFadeStartSec   = 1.3f;
    private const float ButtonFadeDurSec     = 0.5f;

    // ─────────────────────────────────────────────────────────────
    // 직렬화 필드 (PocBuildPipeline.SetupSplashScene 에서 주입)
    // ─────────────────────────────────────────────────────────────

    [Header("애니메이션 프레임 (12장, f00 ~ f11)")]
    public Texture2D[] frames;

    [Header("BGM (단발 재생, loop=false)")]
    public AudioClip bgm;

    [Header("표시 문구")]
    public string title = "작은정복자들";
    [Tooltip("타이틀 아래 서브카피 — '걸음' 앵커 단어 1회 등장")]
    public string subCopy = "걸음마다, 자라나는 세계";
    public string startButtonLabel = "첫 걸음 떼기";

    [Header("씬 전환")]
    // 시나리오 v2: SplashScene → PlanetIntroScene (GPS 시드 행성 결정) → HelloScene
    public string nextScene = "PlanetIntroScene";

    [Header("페이드아웃 (시작 버튼 탭 후)")]
    [Tooltip("시작 버튼 탭 후 페이드아웃 시간(초). 0이면 즉시 전환.")]
    public float fadeOutDuration = 0.5f;

    [Header("배경 별 효과")]
    public int sparkleCount = 18;

    [Header("BGM 볼륨")]
    [Range(0f, 1f)]
    public float bgmVolume = 0.6f;

    [Header("효과 옵션 (docs §5.2)")]
    [Tooltip("폭발 구간 ±3px 카메라 셰이크 (행성 Rect 에만 적용).")]
    public bool enableShake = true;
    [Tooltip("폭발 구간 배경색 펄스 (#080d1f → #1f0d08 → #080d1f).")]
    public bool enableBgPulse = true;
    [Tooltip("폭발 구간 별 알파를 0.2 로 낮춤.")]
    public bool enableStarDim = true;

    // ─────────────────────────────────────────────────────────────
    // 내부 상태
    // ─────────────────────────────────────────────────────────────

    // 스타일 캐시
    private GUIStyle _titleStyle;
    private GUIStyle _subCopyStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _versionStyle;

    // 시퀀스 타이밍
    private float _startTime;
    private float _elapsedMs; // Update 에서 캐시 (OnGUI / LateUpdate 공유)
    private float _sequenceDoneTime = -1f; // 시퀀스 종료 시점 Time.time (한 번만 기록)

    // 페이드아웃 상태 (버튼 탭 후)
    private bool _isFadingOut;
    private float _fadeOutStartTime;
    private float _bgmStartVolume; // 페이드아웃 시 lerp 기준

    // BGM
    private AudioSource _audio;

    // 별 효과
    private struct Sparkle
    {
        public float x;
        public float y;
        public float phase;
        public float speed;
        public float size;
    }
    private Sparkle[] _sparkles;

    // 배경색 베이스 (펄스 / 복원 기준)
    private static readonly Color BgBase  = new Color32(0x08, 0x0d, 0x1f, 0xff);
    private static readonly Color BgPulse = new Color32(0x1f, 0x0d, 0x08, 0xff);

    // ─────────────────────────────────────────────────────────────
    // Native interop (iOS)
    // ─────────────────────────────────────────────────────────────

    // Assets/Plugins/iOS/IOSAudioSession.mm 의 entry point.
    // AVAudioSession 카테고리를 Playback 으로 변경 — 디바이스 무음 스위치 ON
    // 상태에서도 BGM 이 재생되도록 보장.
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _ConfigurePlaybackAudioSession();
#endif

    // ─────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        Debug.Log("[POC] SplashScreen v2.Start (frames=" + (frames != null ? frames.Length : 0)
                  + ", bgm=" + (bgm != null ? bgm.name : "null") + ")");

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BgBase;
        }

        _startTime = Time.time;

        // 별 효과 — 동적 텍스처 생성 금지 (커밋 19c08ee 검은 화면 회귀 방지).
        // Texture2D.whiteTexture 를 GUI.color 알파로 변조하여 별처럼 보이게 한다.
        _sparkles = new Sparkle[sparkleCount];
        for (int i = 0; i < _sparkles.Length; i++)
        {
            _sparkles[i] = new Sparkle
            {
                x = Random.value,
                y = Random.value,
                phase = Random.value,
                speed = Random.Range(0.25f, 0.6f),
                size = Random.Range(2f, 4f),
            };
        }

        // iOS AVAudioSession 을 Playback 으로 강제 — 디바이스 무음 스위치 우회.
        // BGM 생성 직전에 1회 호출. Editor / 비 iOS 환경에서는 컴파일 제외.
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            _ConfigurePlaybackAudioSession();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[POC] _ConfigurePlaybackAudioSession failed: " + e.Message);
        }
#endif

        // BGM (null 안전 — 자산 누락 시 무음 진행)
        if (bgm != null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = bgm;
            _audio.loop = false;
            _audio.volume = bgmVolume;
            _audio.playOnAwake = false;
            _audio.Play();
            _bgmStartVolume = bgmVolume;
        }
    }

    private void Update()
    {
        // 스킵 입력 — 시퀀스 진행 중 화면 1탭 → 즉시 끝 상태로 점프.
        // 페이드아웃 중에는 무시 (의도치 않은 더블 입력 방지).
        if (!_isFadingOut && !SequenceDone())
        {
            bool tapped = Input.GetMouseButtonDown(0) || Input.touchCount > 0;
            if (tapped)
            {
                // _startTime 을 과거로 끌어서 elapsed = SequenceTotalMs 가 되도록 한다.
                _startTime = Time.time - (SequenceTotalMs / 1000f);
                Debug.Log("[POC] SplashScreen v2: sequence skipped → jump to f11 hold");
            }
        }

        // elapsed 캐시 갱신
        _elapsedMs = (Time.time - _startTime) * 1000f;

        // 시퀀스 종료 시점 1회 기록 (카피 페이드인 기준 시각)
        if (_sequenceDoneTime < 0f && SequenceDone())
        {
            _sequenceDoneTime = Time.time;
            Debug.Log("[POC] SplashScreen v2: sequence done at t=" + _sequenceDoneTime
                      + ", title/subtitle/button fade-in starting");
        }

        // 페이드아웃 (버튼 탭 후) — BGM 볼륨도 함께 페이드
        if (_isFadingOut)
        {
            if (fadeOutDuration <= 0f)
            {
                if (_audio != null) _audio.Stop();
                SceneManager.LoadScene(nextScene);
                return;
            }
            float ft = Time.time - _fadeOutStartTime;
            if (_audio != null)
            {
                _audio.volume = Mathf.Lerp(_bgmStartVolume, 0f, Mathf.Clamp01(ft / fadeOutDuration));
            }
            if (ft >= fadeOutDuration)
            {
                if (_audio != null) _audio.Stop();
                SceneManager.LoadScene(nextScene);
            }
        }
    }

    /// <summary>배경색 펄스. LateUpdate 에서 카메라에 직접 반영 (OnGUI 와 분리).</summary>
    private void LateUpdate()
    {
        if (!enableBgPulse) return;
        var cam = Camera.main;
        if (cam == null) return;

        float ms = _elapsedMs;
        if (ms < ExplosionStartMs || ms > ExplosionEndMs)
        {
            cam.backgroundColor = BgBase;
        }
        else
        {
            float t = (ms - ExplosionStartMs) / (ExplosionEndMs - ExplosionStartMs); // 0~1
            float pulse = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0
            cam.backgroundColor = Color.Lerp(BgBase, BgPulse, pulse);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 시퀀스 / 페이드 계산
    // ─────────────────────────────────────────────────────────────

    /// <summary>시퀀스가 끝났는지 (=f11 hold 상태). 카피 페이드인 시작 조건.</summary>
    private bool SequenceDone()
    {
        return _elapsedMs >= SequenceTotalMs;
    }

    /// <summary>
    /// 경과 ms 로부터 현재 프레임 인덱스를 계산. 누적 duration 표 사용.
    /// loop 금지 — 시퀀스 종료 후에는 항상 마지막 프레임 인덱스(11) 반환.
    /// </summary>
    private int ComputeFrameIndex(float elapsedMs)
    {
        if (elapsedMs < 0f) return 0;
        int acc = 0;
        for (int i = 0; i < FrameDurationsMs.Length; i++)
        {
            acc += FrameDurationsMs[i];
            if (elapsedMs < acc) return i;
        }
        return FrameDurationsMs.Length - 1; // hold 마지막 프레임
    }

    private float TitleAlpha()
    {
        if (_sequenceDoneTime < 0f) return 0f;
        float t = Time.time - _sequenceDoneTime - TitleFadeStartSec;
        return Mathf.Clamp01(t / TitleFadeDurSec);
    }

    private float SubtitleAlpha()
    {
        if (_sequenceDoneTime < 0f) return 0f;
        float t = Time.time - _sequenceDoneTime - SubtitleFadeStartSec;
        return Mathf.Clamp01(t / SubtitleFadeDurSec);
    }

    private float ButtonAlpha()
    {
        if (_sequenceDoneTime < 0f) return 0f;
        float t = Time.time - _sequenceDoneTime - ButtonFadeStartSec;
        return Mathf.Clamp01(t / ButtonFadeDurSec);
    }

    /// <summary>폭발 구간 카메라 셰이크 오프셋 (행성 Rect 에만 적용). ±3px 노이즈.</summary>
    private Vector2 ShakeOffset()
    {
        if (!enableShake) return Vector2.zero;
        float ms = _elapsedMs;
        if (ms < ExplosionStartMs || ms > ExplosionEndMs) return Vector2.zero;
        return new Vector2(
            (Random.value - 0.5f) * 6f,
            (Random.value - 0.5f) * 6f
        );
    }

    // ─────────────────────────────────────────────────────────────
    // IMGUI
    // ─────────────────────────────────────────────────────────────

    private void EnsureStyles()
    {
        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow,
            };
            _titleStyle.normal.textColor = Color.white;
        }

        if (_subCopyStyle == null)
        {
            _subCopyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                clipping = TextClipping.Overflow,
            };
            _subCopyStyle.normal.textColor = new Color(0.85f, 0.92f, 1f, 1f);
        }

        if (_buttonStyle == null)
        {
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            _buttonStyle.normal.textColor = Color.white;
        }

        if (_versionStyle == null)
        {
            _versionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Normal,
            };
            _versionStyle.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
        }
    }

    private void OnGUI()
    {
        EnsureStyles();

        // Safe area 계산 (Screen.safeArea 는 픽셀 좌표, y 는 아래에서 위로)
        // OnGUI 의 y 는 위에서 아래로이므로 변환 필요.
        Rect safe = Screen.safeArea;
        float safeX = safe.x;
        float safeY = Screen.height - (safe.y + safe.height); // 상단 여백
        float safeW = safe.width;
        float safeH = safe.height;

        // 페이드아웃 중에는 전체 알파를 점진 감소
        float globalAlpha = 1f;
        if (_isFadingOut && fadeOutDuration > 0f)
        {
            float ft = Time.time - _fadeOutStartTime;
            globalAlpha = Mathf.Clamp01(1f - ft / fadeOutDuration);
        }

        // 배경 별 — 폭발 구간에는 알파 다운 (0.2 배), 그 외 1.0 배
        float starAlphaScale = 1.0f;
        if (enableStarDim && _elapsedMs >= ExplosionStartMs && _elapsedMs <= ExplosionEndMs)
        {
            starAlphaScale = 0.2f;
        }
        DrawSparkles(globalAlpha, starAlphaScale);

        // 행성 애니메이션 (safe area 중앙) — 단발, loop 금지
        if (frames != null && frames.Length > 0)
        {
            int frameIdx = ComputeFrameIndex(_elapsedMs);
            if (frameIdx < 0) frameIdx = 0;
            if (frameIdx >= frames.Length) frameIdx = frames.Length - 1;

            Texture2D tex = frames[frameIdx];
            if (tex != null)
            {
                // 행성 크기: safe area 짧은 변의 60%
                float planetSide = Mathf.Min(safeW, safeH) * 0.6f;
                float planetX = safeX + (safeW - planetSide) * 0.5f;
                // 화면 세로 중앙보다 약간 위 (텍스트 영역 확보)
                float planetY = safeY + (safeH - planetSide) * 0.5f - safeH * 0.05f;

                // 폭발 구간 셰이크 오프셋
                Vector2 shake = ShakeOffset();
                planetX += shake.x;
                planetY += shake.y;

                GUI.color = new Color(1f, 1f, 1f, globalAlpha);
                GUI.DrawTexture(new Rect(planetX, planetY, planetSide, planetSide), tex, ScaleMode.ScaleToFit);
            }
        }

        // 폰트 크기 (해상도 적응)
        int titleFontSize = Mathf.Clamp((int)(safeW / 12f), 28, 96);
        int subCopyFontSize = Mathf.Clamp((int)(safeW / 22f), 14, 42);
        int buttonFontSize = Mathf.Clamp((int)(safeW / 18f), 18, 56);
        int versionFontSize = Mathf.Clamp((int)(safeW / 38f), 10, 24);

        _titleStyle.fontSize = titleFontSize;
        _subCopyStyle.fontSize = subCopyFontSize;
        _buttonStyle.fontSize = buttonFontSize;
        _versionStyle.fontSize = versionFontSize;

        // ── 타이틀 (상단 8%, 페이드인) ──────────────────────────
        float titleA = TitleAlpha() * globalAlpha;
        float titleH = titleFontSize * 1.6f;
        Rect titleRect = new Rect(safeX, safeY + safeH * 0.08f, safeW, titleH);

        if (titleA > 0f)
        {
            // 그림자 (2px,2px,알파 0.5)
            Color origTitleColor = _titleStyle.normal.textColor;
            _titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.5f);
            GUI.color = new Color(1f, 1f, 1f, titleA);
            Rect titleShadowRect = new Rect(titleRect.x + 2f, titleRect.y + 2f, titleRect.width, titleRect.height);
            GUI.Label(titleShadowRect, title, _titleStyle);

            // 본문
            _titleStyle.normal.textColor = origTitleColor;
            GUI.color = new Color(1f, 1f, 1f, titleA);
            GUI.Label(titleRect, title, _titleStyle);
        }

        // ── 서브카피 (행성 아래 72%, 페이드인) ────────────────
        float subA = SubtitleAlpha() * globalAlpha;
        if (subA > 0f)
        {
            float subH = subCopyFontSize * 2.2f;
            Rect subRect = new Rect(safeX, safeY + safeH * 0.72f, safeW, subH);
            GUI.color = new Color(1f, 1f, 1f, subA);
            GUI.Label(subRect, subCopy, _subCopyStyle);
        }

        // ── 시작 버튼 (하단 15% 위, 페이드인 + 입력 활성) ──
        float btnA = ButtonAlpha() * globalAlpha;
        float btnW = Mathf.Min(safeW * 0.7f, 460f);
        float btnH = Mathf.Clamp(buttonFontSize * 2.4f, 56f, 120f);
        float btnX = safeX + (safeW - btnW) * 0.5f;
        float btnY = safeY + safeH * 0.85f - btnH * 0.5f;
        Rect btnRect = new Rect(btnX, btnY, btnW, btnH);

        // 버튼은 fadeIn 거의 완료(α≥0.95) + 시퀀스 종료 + 페이드아웃 X 일 때만 입력 활성
        bool buttonEnabled = btnA >= 0.95f && SequenceDone() && !_isFadingOut;
        if (btnA > 0f)
        {
            GUI.color = new Color(1f, 1f, 1f, btnA);
            GUI.enabled = buttonEnabled;
            if (GUI.Button(btnRect, startButtonLabel, _buttonStyle))
            {
                Debug.Log("[POC] SplashScreen v2: start button tapped, fading out → " + nextScene);
                _isFadingOut = true;
                _fadeOutStartTime = Time.time;
            }
            GUI.enabled = true;
        }

        // ── 버전 (우하단, 버튼과 동기 페이드인) ──────────────
        if (btnA > 0f)
        {
            string versionText = "v " + Application.version;
            float verW = safeW * 0.4f;
            float verH = versionFontSize * 1.8f;
            float verX = safeX + safeW - verW - safeW * 0.04f;
            float verY = safeY + safeH - verH - safeH * 0.02f;
            GUI.color = new Color(1f, 1f, 1f, btnA * 0.6f);
            GUI.Label(new Rect(verX, verY, verW, verH), versionText, _versionStyle);
        }

        GUI.color = Color.white;
    }

    private void DrawSparkles(float globalAlpha, float alphaScale)
    {
        if (_sparkles == null) return;

        for (int i = 0; i < _sparkles.Length; i++)
        {
            var s = _sparkles[i];
            float p = (s.phase + Time.time * s.speed) % 1f;
            float a = Mathf.Sin(p * Mathf.PI) * 0.8f * alphaScale * globalAlpha;
            if (a <= 0f) continue;
            GUI.color = new Color(1f, 1f, 0.85f, a);
            float px = s.x * Screen.width - s.size / 2f;
            float py = s.y * Screen.height - s.size / 2f;
            GUI.DrawTexture(new Rect(px, py, s.size, s.size), Texture2D.whiteTexture);
        }
    }
}
