using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SplashScreen : MonoBehaviour
{
    public Texture2D backgroundIcon;
    public string title = "작은정복자들";
    public string nextScene = "HelloScene";
    public float displayDuration = 2.0f;
    public float fadeDuration = 0.6f;
    public int sparkleCount = 10;

    private struct Sparkle
    {
        public float x;
        public float y;
        public float phase;
        public float speed;
        public float size;
    }

    private GUIStyle _titleStyle;
    private float _alpha;
    private float _startTime;
    private Sparkle[] _sparkles;
    private Texture2D _whiteTex;

    private void Awake()
    {
        if (backgroundIcon != null)
        {
            backgroundIcon.filterMode = FilterMode.Point;
        }

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
                size = Random.Range(3f, 6f),
            };
        }

        _whiteTex = new Texture2D(1, 1);
        _whiteTex.SetPixel(0, 0, Color.white);
        _whiteTex.Apply();
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(displayDuration + fadeDuration * 2f);
        SceneManager.LoadScene(nextScene);
    }

    private void Update()
    {
        float t = Time.time - _startTime;
        if (t < fadeDuration)
        {
            _alpha = Mathf.Clamp01(t / fadeDuration);
        }
        else if (t < fadeDuration + displayDuration)
        {
            _alpha = 1f;
        }
        else
        {
            float fadeOut = (t - fadeDuration - displayDuration) / fadeDuration;
            _alpha = Mathf.Clamp01(1f - fadeOut);
        }
    }

    private void EnsureStyles()
    {
        if (_titleStyle != null) return;
        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Overflow,
        };
        _titleStyle.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        EnsureStyles();

        DrawSparkles();

        GUI.color = new Color(1f, 1f, 1f, _alpha);

        if (backgroundIcon != null)
        {
            float elapsed = Time.time - _startTime;
            float zoom = 1f + Mathf.Clamp01(elapsed / 6f) * 0.06f;
            int sideBase = Mathf.Min(Screen.width, Screen.height) * 6 / 10;
            int side = (int)(sideBase * zoom);
            float x = (Screen.width - side) / 2f;
            float y = (Screen.height - side) / 2f - Screen.height * 0.05f;
            GUI.DrawTexture(new Rect(x, y, side, side), backgroundIcon, ScaleMode.ScaleToFit);
        }

        int fontSize = Mathf.Min(Screen.width / 10, Screen.height / 22);
        _titleStyle.fontSize = fontSize;
        float titleH = fontSize * 2f;
        Rect titleRect = new Rect(0f, Screen.height * 0.78f, Screen.width, titleH);
        GUI.Label(titleRect, title, _titleStyle);

        GUI.color = Color.white;
    }

    private void DrawSparkles()
    {
        if (_sparkles == null || _whiteTex == null) return;

        for (int i = 0; i < _sparkles.Length; i++)
        {
            var s = _sparkles[i];
            float p = (s.phase + Time.time * s.speed) % 1f;
            float a = Mathf.Sin(p * Mathf.PI) * _alpha * 0.8f;
            if (a <= 0f) continue;
            GUI.color = new Color(1f, 1f, 0.85f, a);
            float px = s.x * Screen.width - s.size / 2f;
            float py = s.y * Screen.height - s.size / 2f;
            GUI.DrawTexture(new Rect(px, py, s.size, s.size), _whiteTex);
        }
    }
}
