using System.Collections;
using UnityEngine;

public class GpsCheck : MonoBehaviour
{
    private string _status = "GPS: 초기화 대기";
    private string _coords = "";

    private GUIStyle _labelStyle;
    private GUIStyle _shadowStyle;

    private IEnumerator Start()
    {
#if UNITY_IOS || UNITY_ANDROID
        if (!Input.location.isEnabledByUser)
        {
            _status = "GPS: 위치 서비스 꺼져있음";
            yield break;
        }
#endif

        Input.location.Start(5f, 5f);
        _status = "GPS: 권한 요청 / 초기화 중";

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1f);
            maxWait--;
        }

        if (maxWait <= 0)
        {
            _status = "GPS: 초기화 타임아웃";
            yield break;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            _status = "GPS: 권한 거부 또는 실패";
            yield break;
        }

        _status = "GPS: 측정 중";
        while (true)
        {
            var d = Input.location.lastData;
            _coords = string.Format(
                "위도 {0:F6}\n경도 {1:F6}\n정확도 {2:F1}m",
                d.latitude, d.longitude, d.horizontalAccuracy);
            yield return new WaitForSeconds(1f);
        }
    }

    private void OnDestroy()
    {
        if (Input.location.status == LocationServiceStatus.Running)
        {
            Input.location.Stop();
        }
    }

    private void EnsureStyles()
    {
        if (_labelStyle != null) return;
        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Normal,
            wordWrap = true,
        };
        _labelStyle.normal.textColor = Color.white;
        _shadowStyle = new GUIStyle(_labelStyle);
        _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
    }

    private void OnGUI()
    {
        EnsureStyles();
        int size = Mathf.Max(22, Screen.height / 32);
        _labelStyle.fontSize = size;
        _shadowStyle.fontSize = size;

        float h = Screen.height * 0.30f;
        float y = Screen.height - h - 20f;
        Rect rect = new Rect(0f, y, Screen.width, h);
        Rect shadow = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);

        string text = _status + (string.IsNullOrEmpty(_coords) ? "" : "\n\n" + _coords);
        GUI.Label(shadow, text, _shadowStyle);
        GUI.Label(rect, text, _labelStyle);
    }
}
