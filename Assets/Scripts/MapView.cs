// MapView.cs
// TERRA × BIOSPHERE PoC — OSM raster tile 기반 지도 표시 컴포넌트.
//
// 외부 API:
//   - SetCenter(double lat, double lon, int zoom) : 중심 갱신
//   - Zoom { get; set; } : 줌 변경 (자동 갱신)
//
// 좌표/렌더 규약:
//   - 1 타일 = 1 Unity unit (PixelsPerUnit = TileSize)
//   - 중심 위경도를 world 원점 (0,0) 에 둔다
//   - Web Mercator pixel 좌표 차이를 그대로 Unity world 좌표로 사용
//     (X 동쪽 +, Y 북쪽 + — pixel Y 는 남쪽 + 이므로 부호 반전)
//   - SpriteRenderer 자식 GameObject 9~16개 동시 표시 (3×3 ~ 4×4)
//
// 렌더 정책:
//   - 카메라 화면 영역 + 1 타일 여유분만 활성화. 나머지는 풀에 반환.
//   - 타일 로드 전엔 placeholder (회색) 표시 → 로드 완료 시 자연스럽게 교체.
//
// 데모 모드 (Editor only):
//   - 화살표 키로 중심 위경도 이동 (가짜 GPS). 1 키 누름 ≈ 5m 이동.
//
// 알려진 한계:
//   - 줌 레벨 동적 보간 없음 (snap). 추후 PlayerAvatar 따라가는 카메라가 붙으면 충분.
//   - 위도 ±85° 부근에서 발산 가능 — GeoCoord 에서 clamp.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class MapView : MonoBehaviour
{
    [Header("초기 중심 (서울시청)")]
    [SerializeField] private double initialLat = 37.5663;
    [SerializeField] private double initialLon = 126.9779;
    [SerializeField, Range(1, 19)] private int initialZoom = 17;

    [Header("렌더")]
    [SerializeField] private Camera mapCamera;
    [Tooltip("화면 밖 여유 타일 수 (각 방향). 0 이면 화면 안 타일만, 1 이면 가장자리 한 줄 더.")]
    [SerializeField, Range(0, 3)] private int paddingTiles = 1;

    [Header("데모 (Editor)")]
    [SerializeField] private bool enableArrowKeyPan = true;
    [SerializeField] private float metersPerKeyPress = 5f;

    public int Zoom
    {
        get => _zoom;
        set
        {
            int clamped = Mathf.Clamp(value, 1, 19);
            if (clamped == _zoom) return;
            _zoom = clamped;
            RefreshTiles();
        }
    }

    private double _centerLat;
    private double _centerLon;
    private int _zoom;

    // 현재 표시 중인 타일 (key = "z/x/y" → instance)
    private readonly Dictionary<string, TileSlot> _active = new Dictionary<string, TileSlot>(32);
    // 재활용 풀
    private readonly Stack<TileSlot> _pool = new Stack<TileSlot>(32);

    private Transform _tileRoot;

    private class TileSlot
    {
        public GameObject Go;
        public SpriteRenderer Sr;
        public int Z, X, Y;
        public string Key;
        public Sprite CurrentSprite;
        public Texture2D CurrentTex;
    }

    private void Awake()
    {
        if (mapCamera == null) mapCamera = Camera.main;
        if (mapCamera != null && !mapCamera.orthographic)
        {
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = 1.5f;
        }
        var rootGo = new GameObject("Tiles");
        rootGo.transform.SetParent(transform, worldPositionStays: false);
        _tileRoot = rootGo.transform;

        _zoom = initialZoom;
        _centerLat = initialLat;
        _centerLon = initialLon;
    }

    private void Start()
    {
        // Awake 가 아니라 Start 에서 첫 RefreshTiles — TileCache.Instance 가
        // 다른 컴포넌트 Awake 와 경합하지 않도록 한 프레임 지연.
        RefreshTiles();
    }

    public void SetCenter(double lat, double lon, int zoom)
    {
        _centerLat = lat;
        _centerLon = lon;
        int clamped = Mathf.Clamp(zoom, 1, 19);
        bool zoomChanged = clamped != _zoom;
        _zoom = clamped;

        if (zoomChanged)
        {
            // 줌이 바뀌면 모든 슬롯 회수 (좌표 의미가 달라짐)
            RecycleAll();
        }
        RefreshTiles();
    }

    public void SetCenter(double lat, double lon)
    {
        SetCenter(lat, lon, _zoom);
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (enableArrowKeyPan) HandleArrowKeyPan();
#endif
    }

#if UNITY_EDITOR
    private void HandleArrowKeyPan()
    {
        bool moved = false;
        // 위도 1° ≈ 111km; 경도 1° ≈ 111km * cos(lat)
        double dLatPerMeter = 1.0 / 111000.0;
        double dLonPerMeter = 1.0 / (111000.0 * System.Math.Cos(_centerLat * System.Math.PI / 180.0));

        if (Input.GetKey(KeyCode.UpArrow))    { _centerLat += dLatPerMeter * metersPerKeyPress * Time.deltaTime * 30f; moved = true; }
        if (Input.GetKey(KeyCode.DownArrow))  { _centerLat -= dLatPerMeter * metersPerKeyPress * Time.deltaTime * 30f; moved = true; }
        if (Input.GetKey(KeyCode.RightArrow)) { _centerLon += dLonPerMeter * metersPerKeyPress * Time.deltaTime * 30f; moved = true; }
        if (Input.GetKey(KeyCode.LeftArrow))  { _centerLon -= dLonPerMeter * metersPerKeyPress * Time.deltaTime * 30f; moved = true; }

        if (moved) RefreshTiles();
    }
#endif

    /// <summary>현재 중심·줌 기준 필요한 타일 집합 재계산 → 부족분 로드, 잉여분 회수.</summary>
    private void RefreshTiles()
    {
        if (mapCamera == null) return;

        // 카메라 ortho 영역 → 필요한 타일 범위 산정
        // 1 unit = 1 tile. ortho size = 화면 절반 높이(unit).
        float halfH = mapCamera.orthographicSize;
        float halfW = halfH * mapCamera.aspect;
        int tilesX = Mathf.CeilToInt(halfW * 2f) + 2 * paddingTiles;
        int tilesY = Mathf.CeilToInt(halfH * 2f) + 2 * paddingTiles;

        // 중심 위경도 → 전역 픽셀 좌표
        var (cpx, cpy) = GeoCoord.LatLonToPixel(_centerLat, _centerLon, _zoom);
        int centerTx = (int)System.Math.Floor(cpx / GeoCoord.TileSize);
        int centerTy = (int)System.Math.Floor(cpy / GeoCoord.TileSize);

        // 중심 타일의 좌상단을 기준으로 한 sub-pixel 오프셋 (0~1)
        // → world 원점이 중심 위경도가 되도록 모든 타일을 평행이동.
        // 각 타일의 world 위치:
        //   x = (tx - centerTx) - (cpx/256 - centerTx) + 0.5
        //     = 0.5 + (tx - cpx/256)
        //   y = -((ty - centerTy) - (cpy/256 - centerTy)) - 0.5
        //     = -0.5 - (ty - cpy/256)
        double centerPxTile = cpx / GeoCoord.TileSize;
        double centerPyTile = cpy / GeoCoord.TileSize;

        int rangeX = tilesX / 2 + 1;
        int rangeY = tilesY / 2 + 1;

        var needed = new HashSet<string>();
        int maxXY = 1 << _zoom;

        for (int dy = -rangeY; dy <= rangeY; dy++)
        {
            for (int dx = -rangeX; dx <= rangeX; dx++)
            {
                int tx = centerTx + dx;
                int ty = centerTy + dy;
                if (ty < 0 || ty >= maxXY) continue;        // Y 는 0 ~ 2^z-1
                int wrappedTx = ((tx % maxXY) + maxXY) % maxXY; // X 는 wrap (경도 ±180 연결)

                string key = $"{_zoom}/{wrappedTx}/{ty}";
                needed.Add(key);

                if (!_active.TryGetValue(key, out var slot))
                {
                    slot = AcquireSlot();
                    slot.Z = _zoom;
                    slot.X = wrappedTx;
                    slot.Y = ty;
                    slot.Key = key;
                    _active[key] = slot;

                    // 즉시 placeholder 부착 — 로드 완료 전에도 회색 사각형이 보이도록
                    ApplyTexture(slot, TileCache.Instance.Placeholder);

                    StartCoroutine(LoadInto(slot));
                }

                // world 위치 갱신 (중심 이동 시에도 살아있는 슬롯은 재배치)
                float worldX = (float)(0.5 + (tx - centerPxTile));
                float worldY = (float)(-0.5 - (ty - centerPyTile));
                slot.Go.transform.localPosition = new Vector3(worldX, worldY, 0f);
            }
        }

        // 불필요해진 슬롯 회수
        // (Dictionary 순회 중 제거 못하므로 임시 리스트)
        List<string> toRemove = null;
        foreach (var kv in _active)
        {
            if (!needed.Contains(kv.Key))
            {
                if (toRemove == null) toRemove = new List<string>();
                toRemove.Add(kv.Key);
            }
        }
        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                var slot = _active[toRemove[i]];
                _active.Remove(toRemove[i]);
                ReleaseSlot(slot);
            }
        }
    }

    private IEnumerator LoadInto(TileSlot slot)
    {
        // 캡처: 비동기 콜백 도착 시 슬롯이 다른 타일로 재활용되었을 수 있다.
        int capZ = slot.Z, capX = slot.X, capY = slot.Y;
        string capKey = slot.Key;

        yield return TileCache.Instance.GetTile(capZ, capX, capY, tex =>
        {
            // 슬롯이 회수되었거나 다른 타일로 재할당되었으면 무시
            if (slot.Go == null || slot.Key != capKey) return;
            ApplyTexture(slot, tex);
        });
    }

    private void ApplyTexture(TileSlot slot, Texture2D tex)
    {
        if (tex == null) return;

        // 이전 sprite 정리 (placeholder 공유 텍스처는 destroy 하지 않는다 — TileCache 가 관리)
        if (slot.CurrentSprite != null)
        {
            var prev = slot.CurrentSprite;
            slot.CurrentSprite = null;
            Object.Destroy(prev);
        }

        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: GeoCoord.TileSize,
            extrude: 0,
            meshType: SpriteMeshType.FullRect);
        slot.Sr.sprite = sprite;
        slot.CurrentSprite = sprite;
        slot.CurrentTex = tex;
    }

    private TileSlot AcquireSlot()
    {
        if (_pool.Count > 0)
        {
            var s = _pool.Pop();
            s.Go.SetActive(true);
            return s;
        }
        var go = new GameObject("Tile");
        go.transform.SetParent(_tileRoot, worldPositionStays: false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 0;
        return new TileSlot { Go = go, Sr = sr };
    }

    private void ReleaseSlot(TileSlot slot)
    {
        if (slot.CurrentSprite != null)
        {
            Object.Destroy(slot.CurrentSprite);
            slot.CurrentSprite = null;
        }
        slot.Sr.sprite = null;
        slot.CurrentTex = null;
        slot.Key = null;
        slot.Go.SetActive(false);
        _pool.Push(slot);
    }

    private void RecycleAll()
    {
        foreach (var kv in _active) ReleaseSlot(kv.Value);
        _active.Clear();
    }

    private void OnDestroy()
    {
        // Sprite 만 정리 (텍스처는 TileCache 소유)
        foreach (var kv in _active)
        {
            if (kv.Value.CurrentSprite != null) Object.Destroy(kv.Value.CurrentSprite);
        }
        _active.Clear();
    }
}
