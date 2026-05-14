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

    private GUIStyle _titleStyle;
    private float _alpha;
    private float _startTime;

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
        GUI.color = new Color(1f, 1f, 1f, _alpha);

        if (backgroundIcon != null)
        {
            int side = Mathf.Min(Screen.width, Screen.height) * 6 / 10;
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
}
