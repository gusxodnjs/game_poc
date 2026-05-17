using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스플래시 화면 — 황폐 행성에서 푸른 지구로의 진화 시퀀스를 보여주고,
/// 사용자가 시작 버튼을 탭하면 다음 씬으로 이동한다.
///
/// 변경 이력:
/// - 자동 씬 전환 코루틴 제거 (사용자 명시적 입력으로만 전환)
/// - 8프레임 행성 진화 애니메이션 (6fps, Loop)
/// - Screen.safeArea 적용 (iPhone 노치 회피)
/// - Application.version 우하단 표시
/// - IMGUI 기반 (Unity GUI Canvas 비사용)
/// </summary>
public class SplashScreen : MonoBehaviour
{
    [Header("애니메이션 프레임 (8장, 순서대로)")]
    public Texture2D[] frames;

    [Header("표시 문구")]
    public string title = "작은정복자들";
    [Tooltip("타이틀 아래 표시되는 서브 카피. GD 검토: '걸음마다, 자라나는 세계'")]
    public string subCopy = "걸음마다, 자라나는 세계";
    public string startButtonLabel = "첫 걸음 떼기";

    [Header("씬 전환")]
    // 시나리오 v2: SplashScene → PlanetIntroScene (GPS 시드 행성 결정) → HelloScene
    public string nextScene = "PlanetIntroScene";

    [Header("애니메이션 파라미터")]
    [Tooltip("초당 프레임 수")]
    public float frameRate = 6f;

    [Header("페이드 인/아웃")]
    [Tooltip("최초 페이드인 시간(초). 이후에는 자동 전환 없음.")]
    public float fadeInDuration = 0.6f;
    [Tooltip("시작 버튼 탭 후 페이드아웃 시간(초). 0이면 즉시 전환.")]
    public float fadeOutDuration = 0.3f;

    [Header("배경 별 효과")]
    public int sparkleCount = 18;

    // 스타일 캐시
    private GUIStyle _titleStyle;
    private GUIStyle _subCopyStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _versionStyle;

    // 페이드 상태
    private float _alpha;
    private float _startTime;

    // 페이드아웃 상태
    private bool _isFadingOut;
    private float _fadeOutStartTime;

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

    // 별 텍스처는 Texture2D.whiteTexture를 사용한다.
    // (과거 commit 19c08ee: 동적 텍스처 생성이 검은 화면 유발 — 동적 생성 금지)

    private void Start()
    {
        Debug.Log("[POC] SplashScreen.Start (frames=" + (frames != null ? frames.Length : 0) + ")");

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x08, 0x0d, 0x1f, 0xff);
        }

        _startTime = Time.time;

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
    }

    private void Update()
    {
        // 페이드아웃 우선 처리 (시작 버튼 탭 후)
        if (_isFadingOut)
        {
            if (fadeOutDuration <= 0f)
            {
                _alpha = 0f;
                SceneManager.LoadScene(nextScene);
                return;
            }
            float ft = Time.time - _fadeOutStartTime;
            _alpha = Mathf.Clamp01(1f - ft / fadeOutDuration);
            if (ft >= fadeOutDuration)
            {
                SceneManager.LoadScene(nextScene);
            }
            return;
        }

        // 페이드인 (자동 씬 전환은 제거됨 — 명시적 입력만)
        float t = Time.time - _startTime;
        if (t < fadeInDuration)
        {
            _alpha = Mathf.Clamp01(t / fadeInDuration);
        }
        else
        {
            _alpha = 1f;
        }
    }

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

        // Safe area 계산 (Unity의 Screen.safeArea는 픽셀 좌표, y는 아래에서 위로)
        // OnGUI의 y는 위에서 아래로이므로 변환 필요.
        Rect safe = Screen.safeArea;
        float safeX = safe.x;
        float safeY = Screen.height - (safe.y + safe.height); // 상단 여백
        float safeW = safe.width;
        float safeH = safe.height;

        // 배경 별 (전체 화면 기준)
        DrawSparkles();

        // 행성 애니메이션 (safe area 중앙)
        if (frames != null && frames.Length > 0)
        {
            int frameIdx = Mathf.FloorToInt((Time.time - _startTime) * frameRate) % frames.Length;
            // 음수 보호 (Time.time이 매우 작을 때)
            if (frameIdx < 0) frameIdx = 0;

            Texture2D tex = frames[frameIdx];
            if (tex != null)
            {
                // 행성 크기: safe area 짧은 변의 60%
                float planetSide = Mathf.Min(safeW, safeH) * 0.6f;
                float planetX = safeX + (safeW - planetSide) * 0.5f;
                // 화면 세로 중앙보다 약간 위 (텍스트 영역 확보)
                float planetY = safeY + (safeH - planetSide) * 0.5f - safeH * 0.05f;

                GUI.color = new Color(1f, 1f, 1f, _alpha);
                GUI.DrawTexture(new Rect(planetX, planetY, planetSide, planetSide), tex, ScaleMode.ScaleToFit);
            }
        }

        // 폰트 크기 계산 (해상도 적응)
        int titleFontSize = Mathf.Clamp((int)(safeW / 12f), 28, 96);
        int subCopyFontSize = Mathf.Clamp((int)(safeW / 22f), 14, 42);
        int buttonFontSize = Mathf.Clamp((int)(safeW / 18f), 18, 56);
        int versionFontSize = Mathf.Clamp((int)(safeW / 38f), 10, 24);

        _titleStyle.fontSize = titleFontSize;
        _subCopyStyle.fontSize = subCopyFontSize;
        _buttonStyle.fontSize = buttonFontSize;
        _versionStyle.fontSize = versionFontSize;

        GUI.color = new Color(1f, 1f, 1f, _alpha);

        // 타이틀 (safe area 상단 12% 위치) — 검은 그림자 (2px,2px,알파 0.5) 후 본문
        float titleH = titleFontSize * 1.6f;
        Rect titleRect = new Rect(safeX, safeY + safeH * 0.08f, safeW, titleH);

        // 그림자 — 별도 스타일 객체 없이 textColor 토글로 처리
        Color origTitleColor = _titleStyle.normal.textColor;
        _titleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.5f);
        GUI.color = new Color(1f, 1f, 1f, _alpha);
        Rect titleShadowRect = new Rect(titleRect.x + 2f, titleRect.y + 2f, titleRect.width, titleRect.height);
        GUI.Label(titleShadowRect, title, _titleStyle);

        // 본문
        _titleStyle.normal.textColor = origTitleColor;
        GUI.Label(titleRect, title, _titleStyle);

        // 서브카피 (행성 아래)
        float subH = subCopyFontSize * 2.2f;
        Rect subRect = new Rect(safeX, safeY + safeH * 0.72f, safeW, subH);
        GUI.Label(subRect, subCopy, _subCopyStyle);

        // 시작 버튼 (safe area 하단 약 15% 위)
        float btnW = Mathf.Min(safeW * 0.7f, 460f);
        float btnH = Mathf.Clamp(buttonFontSize * 2.4f, 56f, 120f);
        float btnX = safeX + (safeW - btnW) * 0.5f;
        float btnY = safeY + safeH * 0.85f - btnH * 0.5f;
        Rect btnRect = new Rect(btnX, btnY, btnW, btnH);

        // 버튼은 _alpha가 충분히 차오른 뒤, 그리고 페이드아웃 중이 아닐 때만 입력 받는다
        bool buttonEnabled = _alpha >= 0.95f && !_isFadingOut;
        GUI.enabled = buttonEnabled;
        if (GUI.Button(btnRect, startButtonLabel, _buttonStyle))
        {
            Debug.Log("[POC] SplashScreen: start button tapped, fading out → " + nextScene);
            _isFadingOut = true;
            _fadeOutStartTime = Time.time;
        }
        GUI.enabled = true;

        // 버전 표시 (safe area 우하단)
        string versionText = "v " + Application.version;
        float verW = safeW * 0.4f;
        float verH = versionFontSize * 1.8f;
        float verX = safeX + safeW - verW - safeW * 0.04f;
        float verY = safeY + safeH - verH - safeH * 0.02f;
        GUI.color = new Color(1f, 1f, 1f, _alpha * 0.6f);
        GUI.Label(new Rect(verX, verY, verW, verH), versionText, _versionStyle);

        GUI.color = Color.white;
    }

    private void DrawSparkles()
    {
        if (_sparkles == null) return;

        for (int i = 0; i < _sparkles.Length; i++)
        {
            var s = _sparkles[i];
            float p = (s.phase + Time.time * s.speed) % 1f;
            float a = Mathf.Sin(p * Mathf.PI) * _alpha * 0.8f;
            if (a <= 0f) continue;
            GUI.color = new Color(1f, 1f, 0.85f, a);
            float px = s.x * Screen.width - s.size / 2f;
            float py = s.y * Screen.height - s.size / 2f;
            GUI.DrawTexture(new Rect(px, py, s.size, s.size), Texture2D.whiteTexture);
        }
    }
}
