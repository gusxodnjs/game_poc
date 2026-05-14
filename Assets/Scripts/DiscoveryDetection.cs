using UnityEngine;

public class DiscoveryDetection : MonoBehaviour
{
    public string[] speciesNames = new[]
    {
        "민들레", "강아지풀", "흰토끼풀", "벚꽃", "무당벌레", "꿀벌",
    };

    public float displayDuration = 3.5f;

    private string _prevCellId = "";
    private string _discoveryMessage = "";
    private float _discoveryStartedAt = -10f;

    private System.Random _rng = new System.Random();
    private GUIStyle _msgStyle;
    private GUIStyle _msgShadowStyle;

    private void Update()
    {
        if (Input.location.status != LocationServiceStatus.Running) return;

        var d = Input.location.lastData;
        string currentCell = CellMapping.ToCellId(d.latitude, d.longitude);

        if (!string.IsNullOrEmpty(_prevCellId) && currentCell != _prevCellId)
        {
            string species = speciesNames[_rng.Next(speciesNames.Length)];
            _discoveryMessage = "발견! " + species;
            _discoveryStartedAt = Time.time;
        }
        _prevCellId = currentCell;
    }

    private void EnsureStyles()
    {
        if (_msgStyle != null) return;
        _msgStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            wordWrap = false,
            clipping = TextClipping.Overflow,
        };
        _msgStyle.normal.textColor = new Color(1f, 0.95f, 0.6f, 1f);
        _msgShadowStyle = new GUIStyle(_msgStyle);
        _msgShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
    }

    private void OnGUI()
    {
        float elapsed = Time.time - _discoveryStartedAt;
        if (elapsed > displayDuration || string.IsNullOrEmpty(_discoveryMessage)) return;

        EnsureStyles();
        int fontSize = Mathf.Min(Screen.width / 9, Screen.height / 20);
        _msgStyle.fontSize = fontSize;
        _msgShadowStyle.fontSize = fontSize;

        float fade = 0.5f;
        float alpha = 1f;
        if (elapsed < fade) alpha = elapsed / fade;
        else if (elapsed > displayDuration - fade) alpha = (displayDuration - elapsed) / fade;
        alpha = Mathf.Clamp01(alpha);

        Rect rect = new Rect(0f, Screen.height * 0.12f, Screen.width, fontSize * 2f);
        Rect shadow = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);

        Color prev = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(shadow, _discoveryMessage, _msgShadowStyle);
        GUI.Label(rect, _discoveryMessage, _msgStyle);
        GUI.color = prev;
    }
}
