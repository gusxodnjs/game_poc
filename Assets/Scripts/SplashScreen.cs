using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스플래시 화면 v2 — "빅뱅 시퀀스".
/// 무 → 폭발 → 파편 응집 → 황폐한 원시 행성 의 단발 12프레임 시퀀스를 보여주고,
/// 시퀀스 종료 후 타이틀/서브카피/버튼이 순차 페이드인된다.
///
/// 사양 출처(SSOT):
///   - 시퀀스 / 카피 / 타이밍: docs/splash_v2_bigbang.md
///   - 시각 합성 v3 통합 (라디얼 그라데이션 / 히트 헤이즈 / 행성 75%): docs/superpowers/specs/2026-05-21-splash-merge-design.md
/// 의존: PR #34 (docs), PR #35 (12프레임 자산), PR #36 (BGM), PR #46 (v3 alpha 12프레임)
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
    // v3 통합 디자인 §4.6: 18 → 24, PNG 안 baked 별이 사라졌으므로 화면 별 밀도 보강.
    public int sparkleCount = 24;

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
    [Tooltip("프레임 간 크로스페이드 (마지막 180ms).")]
    public bool enableCrossFade = true;
    [Tooltip("응집 단계 행성 스케일 ease (파편 → 행성).")]
    public bool enableCoalesceScale = true;
    [Tooltip("응집 단계 미세 부유 (1.5px).")]
    public bool enableCoalesceFloat = true;

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

    // v3 통합 디자인: 라디얼 그라데이션 배경 색상 (docs/superpowers/specs/2026-05-21-splash-merge-design.md §2.1, §4.1)
    // - 카메라 ClearFlags 는 외곽 fallback 색 BgOuter 로만 clear.
    // - L1 OnGUI 에서 두 장의 라디얼 그라데이션 텍스처(베이스/펄스) 를 cross-fade.
    private static readonly Color BgCenterBase  = new Color32(0x1a, 0x12, 0x28, 0xff); // 살짝 따뜻한 보라/적흑
    private static readonly Color BgCenterPulse = new Color32(0x5a, 0x24, 0x18, 0xff); // 폭발 펄스 적등색
    private static readonly Color BgOuter       = new Color32(0x04, 0x06, 0x16, 0xff); // deep cosmic 외곽 fallback

    // v3: OnGUI 에서 1회 생성하는 동적 텍스처 핸들.
    // ★ 검은 화면 회귀 방지(커밋 19c08ee): 텍스처 생성은 Start() 가 아닌 첫 OnGUI() 에서 (EnsureTextures).
    private bool _texturesReady;
    private Texture2D _bgGradientBase;
    private Texture2D _bgGradientPulse;
    private Texture2D _heatHaze;

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
            // v3 §4.7: 카메라 ClearFlags 는 외곽 fallback 색만 클리어.
            // 펄스 / 그라데이션은 OnGUI L1 단에서 라디얼 텍스처가 풀스크린 덮음.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BgOuter;
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
                // v3 §4.6: 사이즈 범위 2~4 → 2~5 로 확장 (다양화).
                size = Random.Range(2f, 5f),
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

    // v3 §4.9: 기존 LateUpdate 의 카메라 배경 펄스는 제거.
    // 펄스는 이제 OnGUI L1 단의 라디얼 그라데이션 cross-fade (DrawBackgroundGradient) 로만 처리.
    // 카메라 배경은 정적 BgOuter 유지.

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

    /// <summary>프레임 간 크로스페이드 윈도우 (ms). 각 프레임의 마지막 구간만 활성.</summary>
    private const float CrossFadeMs = 180f;

    /// <summary>
    /// 현재 시점의 (curIdx, nextIdx, fadeT) 반환.
    /// fadeT 는 다음 프레임으로의 cross-fade 비율 (0=현재 100%, 1=다음 100%).
    /// 각 프레임의 마지막 CrossFadeMs 동안만 cross-fade 활성, 그 외는 fadeT=0.
    /// 마지막 프레임(f11)은 hold — nextIdx=curIdx, fadeT=0.
    /// enableCrossFade=false 이면 항상 fadeT=0 으로 폴백.
    /// </summary>
    private (int curIdx, int nextIdx, float fadeT) ComputeFrameBlend(float elapsedMs)
    {
        if (elapsedMs < 0f) return (0, 0, 0f);
        int acc = 0;
        for (int i = 0; i < FrameDurationsMs.Length; i++)
        {
            int dur = FrameDurationsMs[i];
            int end = acc + dur;
            if (elapsedMs < end)
            {
                // 현재 프레임 i 내부 위치 ms (0 ~ dur)
                float inFrame = elapsedMs - acc;
                bool isLast = i == FrameDurationsMs.Length - 1;
                if (isLast) return (i, i, 0f);
                if (!enableCrossFade) return (i, i + 1, 0f);
                float fadeStart = dur - CrossFadeMs;
                if (inFrame < fadeStart) return (i, i + 1, 0f);
                float t = Mathf.Clamp01((inFrame - fadeStart) / CrossFadeMs);
                return (i, i + 1, t);
            }
            acc = end;
        }
        int last = FrameDurationsMs.Length - 1;
        return (last, last, 0f);
    }

    /// <summary>
    /// 응집 단계 행성 스케일 (1.0 = 100%, 기본 크기).
    /// - 0~1900ms (폭발 전~잔광): 1.0 (변화 없음)
    /// - 1900~2200ms (잔광): 1.0 → 0.4 빠르게 축소 (빅뱅 후 파편이 작아 보이게)
    /// - 2200~5800ms (응집): 0.4 → 1.0 SmoothStep ease (점진 성장)
    /// - 5800ms+: 1.0 (최종 행성)
    /// enableCoalesceScale=false 이면 항상 1.0 반환.
    /// </summary>
    private float ComputePlanetScale(float ms)
    {
        if (!enableCoalesceScale) return 1.0f;
        if (ms < 1900f) return 1.0f;
        if (ms < 2200f)
        {
            float t = (ms - 1900f) / 300f;
            return Mathf.Lerp(1.0f, 0.4f, t);
        }
        if (ms < 5800f)
        {
            float t = Mathf.SmoothStep(0f, 1f, (ms - 2200f) / 3600f);
            return Mathf.Lerp(0.4f, 1.0f, t);
        }
        return 1.0f;
    }

    /// <summary>
    /// 응집 단계 (2200~5000ms) 행성 위치 미세 부유 (±1.5px).
    /// sin/cos 위상차로 부드러운 8자 곡선 — 셰이크와 달리 산만하지 않음.
    /// enableCoalesceFloat=false 이면 Vector2.zero.
    /// </summary>
    private Vector2 CoalesceFloat(float ms)
    {
        if (!enableCoalesceFloat) return Vector2.zero;
        if (ms < 2200f || ms > 5000f) return Vector2.zero;
        float t = (ms - 2200f) / 1000f;
        return new Vector2(
            Mathf.Sin(t * Mathf.PI * 0.7f) * 1.5f,
            Mathf.Cos(t * Mathf.PI * 0.5f) * 1.5f
        );
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
        // v3 §6.2: 동적 텍스처는 첫 OnGUI() 에서 1회 생성 (Start() 금지 — SRGB/format 회귀 방지).
        EnsureTextures();

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

        // ──────────────────────────────────────────────────────────
        // v3 통합 디자인 레이어 순서 (§2.1, §4.2):
        //   [L1] 라디얼 그라데이션 배경 (풀스크린, 폭발 펄스 cross-fade)
        //   [L2] 절차적 별 24개 (한 벌만 — PNG 안 baked 별 제거됨)
        //   [L4] 히트 헤이즈 (행성 중심) — L3 보다 먼저 그려야 행성 본체가 위에 올라옴
        //   [L3] 행성 프레임 (75%, alpha PNG)
        //   [L5] 타이틀 / 서브카피 / 버튼 / 버전
        // ──────────────────────────────────────────────────────────

        // [L1] 라디얼 그라데이션 배경
        DrawBackgroundGradient(globalAlpha);

        // [L2] 별 — 폭발 구간에는 알파 다운 (0.2 배), 그 외 1.0 배
        float starAlphaScale = 1.0f;
        if (enableStarDim && _elapsedMs >= ExplosionStartMs && _elapsedMs <= ExplosionEndMs)
        {
            starAlphaScale = 0.2f;
        }
        DrawSparkles(globalAlpha, starAlphaScale);

        // 행성 Rect 사전 계산 — L4 히트 헤이즈와 L3 행성이 같은 기준 Rect 공유.
        // 헤이즈는 셰이크/부유 오프셋을 따라가지 않음 (배경과 행성 사이 정적 광학 다리).
        // v3 §4.8: 행성 박스 60% → 75%. y 오프셋(-safeH*0.05f) 은 그대로 유지.
        float planetSide = Mathf.Min(safeW, safeH) * 0.75f;
        planetSide *= ComputePlanetScale(_elapsedMs);
        float planetBaseX = safeX + (safeW - planetSide) * 0.5f;
        float planetBaseY = safeY + (safeH - planetSide) * 0.5f - safeH * 0.05f;
        Rect planetBaseRect = new Rect(planetBaseX, planetBaseY, planetSide, planetSide);

        // [L4] 히트 헤이즈 (행성 PNG 아래) — L3 보다 먼저 그림.
        DrawHeatHaze(globalAlpha, planetBaseRect);

        // [L3] 행성 애니메이션 (safe area 중앙) — 단발, loop 금지
        // 프레임 간 cross-fade + 응집 단계 스케일 ease + 미세 부유 + 폭발 셰이크
        if (frames != null && frames.Length > 0)
        {
            var (curIdx, nextIdx, fadeT) = ComputeFrameBlend(_elapsedMs);
            curIdx = Mathf.Clamp(curIdx, 0, frames.Length - 1);
            nextIdx = Mathf.Clamp(nextIdx, 0, frames.Length - 1);

            Texture2D curTex = frames[curIdx];
            Texture2D nextTex = frames[nextIdx];

            if (curTex != null)
            {
                // 폭발 구간 셰이크 + 응집 구간 미세 부유 (중첩 의도) — 헤이즈는 따라가지 않음
                Vector2 shake = ShakeOffset();
                Vector2 floatOff = CoalesceFloat(_elapsedMs);
                Rect planetRect = new Rect(
                    planetBaseRect.x + shake.x + floatOff.x,
                    planetBaseRect.y + shake.y + floatOff.y,
                    planetBaseRect.width,
                    planetBaseRect.height
                );

                // 현재 프레임 (1 - fadeT)
                GUI.color = new Color(1f, 1f, 1f, (1f - fadeT) * globalAlpha);
                GUI.DrawTexture(planetRect, curTex, ScaleMode.ScaleToFit);

                // 다음 프레임 (fadeT) — 같은 Rect 에 cross-fade
                if (fadeT > 0f && nextTex != null && nextIdx != curIdx)
                {
                    GUI.color = new Color(1f, 1f, 1f, fadeT * globalAlpha);
                    GUI.DrawTexture(planetRect, nextTex, ScaleMode.ScaleToFit);
                }
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

    // ─────────────────────────────────────────────────────────────
    // v3 통합 디자인 — L1 라디얼 그라데이션 배경 (§4.3)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 풀스크린 라디얼 그라데이션 배경. 베이스(중앙 #1a1228 → 외곽 #040616) 위에
    /// 폭발 구간(1500~2200ms) 동안 펄스(중앙 #5a2418 → 외곽 #040616) 텍스처를
    /// sin(t*π) cross-fade 로 덮는다.
    /// </summary>
    private void DrawBackgroundGradient(float globalAlpha)
    {
        if (_bgGradientBase == null) return;

        // 폭발 펄스 비율 (0 → 1 → 0)
        float pulseT = 0f;
        if (enableBgPulse)
        {
            float ms = _elapsedMs;
            if (ms >= ExplosionStartMs && ms <= ExplosionEndMs)
            {
                float t = (ms - ExplosionStartMs) / (ExplosionEndMs - ExplosionStartMs);
                pulseT = Mathf.Sin(t * Mathf.PI);
            }
        }

        Rect full = new Rect(0, 0, Screen.width, Screen.height);

        // 베이스 그라데이션 (불투명)
        GUI.color = new Color(1f, 1f, 1f, globalAlpha);
        GUI.DrawTexture(full, _bgGradientBase, ScaleMode.StretchToFill);

        // 펄스 그라데이션 cross-fade
        if (pulseT > 0f && _bgGradientPulse != null)
        {
            GUI.color = new Color(1f, 1f, 1f, pulseT * globalAlpha);
            GUI.DrawTexture(full, _bgGradientPulse, ScaleMode.StretchToFill);
        }

        GUI.color = Color.white;
    }

    // ─────────────────────────────────────────────────────────────
    // v3 통합 디자인 — L4 히트 헤이즈 (§4.4)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 행성 중심 라디얼 alpha 그라데이션을 1.4× 크기로 그려 행성과 배경 사이를 광학적으로 연결.
    /// 시간대별 alpha:
    ///   - ~1500ms (폭발 전): 0 (헤이즈 없음)
    ///   - 1500~2800ms (폭발~파편): 0.6
    ///   - 2800~5800ms (응집): 0.4 → 0.15 lerp
    ///   - 5800ms+ (hold): 0.15 정적
    /// 헤이즈는 셰이크/부유를 따라가지 않음 — planetBaseRect 기준.
    /// </summary>
    private void DrawHeatHaze(float globalAlpha, Rect planetBaseRect)
    {
        if (_heatHaze == null) return;

        float ms = _elapsedMs;
        float hazeAlpha;
        if (ms < ExplosionStartMs)
        {
            hazeAlpha = 0f;
        }
        else if (ms <= 2800f)
        {
            hazeAlpha = 0.6f;
        }
        else if (ms <= 5800f)
        {
            float t = (ms - 2800f) / 3000f;
            hazeAlpha = Mathf.Lerp(0.4f, 0.15f, t);
        }
        else
        {
            hazeAlpha = 0.15f;
        }

        if (hazeAlpha <= 0f) return;

        // 1.4× 크기로 행성 중심에 그림 (가장자리 너머로 번짐)
        const float expand = 1.4f;
        float hw = planetBaseRect.width * expand;
        float hh = planetBaseRect.height * expand;
        float hx = planetBaseRect.x - (hw - planetBaseRect.width) * 0.5f;
        float hy = planetBaseRect.y - (hh - planetBaseRect.height) * 0.5f;
        Rect hazeRect = new Rect(hx, hy, hw, hh);

        // 따뜻한 황색 헤이즈 (#ffd9a6 = (1, 0.85, 0.65))
        GUI.color = new Color(1f, 0.85f, 0.65f, hazeAlpha * globalAlpha);
        GUI.DrawTexture(hazeRect, _heatHaze, ScaleMode.StretchToFill);
        GUI.color = Color.white;
    }

    // ─────────────────────────────────────────────────────────────
    // v3 통합 디자인 — 동적 텍스처 생성 (§4.5)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 라디얼 그라데이션 + 히트 헤이즈 텍스처를 첫 OnGUI() 에서 1회 생성.
    /// ★ Start() 가 아닌 OnGUI() 에서 호출해야 함 — Start() 에서 동적 텍스처 생성 시
    ///   커밋 19c08ee 의 SRGB/format 검은 화면 회귀가 발생함 (§6.2).
    /// </summary>
    private void EnsureTextures()
    {
        if (_texturesReady) return;
        _bgGradientBase  = BuildRadialColorTexture(128, BgCenterBase,  BgOuter);
        _bgGradientPulse = BuildRadialColorTexture(128, BgCenterPulse, BgOuter);
        _heatHaze        = BuildRadialAlphaTexture(128, new Color(1f, 0.85f, 0.65f, 1f));
        _texturesReady = true;
    }

    /// <summary>
    /// 중앙 → 외곽으로 RGB 색이 lerp 되는 라디얼 그라데이션 텍스처 (alpha=1).
    /// SmoothStep 으로 부드러운 fall-off. bilinear filter 로 풀스크린 stretch 시 자연스러움.
    /// </summary>
    private static Texture2D BuildRadialColorTexture(int size, Color center, Color outer)
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
                t = Mathf.SmoothStep(0f, 1f, t);
                pixels[y * size + x] = Color.Lerp(center, outer, t);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// 중앙 alpha=1 → 외곽 alpha=0 라디얼 그라데이션 텍스처. RGB 는 고정 (헤이즈 색).
    /// 행성 위에 GUI.color 알파 변조로 덧대어 광학 다리 역할.
    /// </summary>
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
                float a = 1f - Mathf.SmoothStep(0f, 1f, t);
                pixels[y * size + x] = new Color(rgb.r, rgb.g, rgb.b, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
