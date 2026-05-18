using System.Collections;
using UnityEngine;

public class GpsCheck : MonoBehaviour
{
    [Header("지도 연동")]
    [Tooltip("GPS 좌표가 갱신될 때 SetCenter 를 호출할 MapView. 비어있으면 호출 생략.")]
    [SerializeField] private MapView mapView;

    [Tooltip("이 위경도 차이 미만이면 SetCenter 호출 생략 (호출 폭주 방지). 0 이면 항상 호출.")]
    [SerializeField] private double minMoveDegrees = 0.0001;

    private string _status = "GPS: 초기화 대기";
    private string _coords = "";

    // 마지막으로 SetCenter 에 넘긴 좌표 (debounce 용). NaN = 미설정.
    private double _lastSentLat = double.NaN;
    private double _lastSentLon = double.NaN;

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
            string cellId = CellMapping.ToCellId(d.latitude, d.longitude);
            double edge = CellMapping.DistanceToBoundaryMeters(d.latitude, d.longitude);
            _coords = string.Format(
                "위도 {0:F6}\n경도 {1:F6}\n정확도 {2:F1}m\n셀 {3}\n경계까지 {4:F1}m",
                d.latitude, d.longitude, d.horizontalAccuracy, cellId, edge);

            UpdateMapCenter(d.latitude, d.longitude);

            yield return new WaitForSeconds(1f);
        }
    }

    /// <summary>
    /// GPS 갱신 좌표를 MapView 중심으로 반영.
    /// 가드:
    ///   - mapView 미연결 → 무시 (NRE 방지)
    ///   - (0,0) 부근 sentinel → 무시 (locationService 초기 프레임)
    ///   - 직전 호출과 minMoveDegrees 미만 차이 → 무시 (SetCenter 호출 폭주 방지)
    /// </summary>
    private void UpdateMapCenter(double lat, double lon)
    {
        if (mapView == null) return;

        // 위경도 절대값이 모두 0.001 미만이면 lastData 가 아직 채워지지 않은 sentinel.
        if (System.Math.Abs(lat) < 0.001 && System.Math.Abs(lon) < 0.001) return;

        if (!double.IsNaN(_lastSentLat) && minMoveDegrees > 0.0)
        {
            double dLat = System.Math.Abs(lat - _lastSentLat);
            double dLon = System.Math.Abs(lon - _lastSentLon);
            if (dLat < minMoveDegrees && dLon < minMoveDegrees) return;
        }

        mapView.SetCenter(lat, lon);
        _lastSentLat = lat;
        _lastSentLon = lon;
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
        // 지도가 화면 중앙을 차지하므로 GPS 좌표 라벨은 좌측 상단으로 이동 (M1 지도 셸 통합).
        // 가로 좌측 38%, 세로 상단 28% 영역, 좌측 정렬, 폰트도 한 단계 작게.
        int size = Mathf.Max(16, Screen.height / 48);
        _labelStyle.fontSize = size;
        _shadowStyle.fontSize = size;
        _labelStyle.alignment = TextAnchor.UpperLeft;
        _shadowStyle.alignment = TextAnchor.UpperLeft;

        // iOS safe area 고려 (노치/Dynamic Island)
        Rect safe = Screen.safeArea;
        // OnGUI 좌표는 좌상단 원점, 아래로 +y. Screen.safeArea 는 좌하단 원점이라 변환 필요.
        float topY = Screen.height - (safe.y + safe.height) + 8f;
        float leftX = safe.x + 12f;
        float w = Screen.width * 0.42f;
        float h = Screen.height * 0.30f;
        Rect rect = new Rect(leftX, topY, w, h);
        Rect shadow = new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height);

        string text = _status + (string.IsNullOrEmpty(_coords) ? "" : "\n\n" + _coords);
        GUI.Label(shadow, text, _shadowStyle);
        GUI.Label(rect, text, _labelStyle);
    }
}
