// TilemapRenderer.cs — grass 베이스(데이터 그리드) + 피처 dual-grid 4-corner 오토타일 오버레이.
// PixelLab create_topdown_tileset 산출이 4-corner Wang 이라 dual-grid 렌더(Assets/world/tiles/tileset_layout.md Layout A).
// cornerIndex = NW*8+NE*4+SW*2+SE*1, col=ci%4, row=ci/4(row0=상단). 외부 API: SetCenter(lat,lon). 1타일=1unit.
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class TilemapRenderer : MonoBehaviour
{
    [Header("초기 중심 (서울시청)")]
    [SerializeField] private double initialLat = 37.5663;
    [SerializeField] private double initialLon = 126.9779;

    [Header("베이스 (grass 변형 4종, 32x32)")]
    [SerializeField] private Texture2D[] grassVariants;

    [Header("오토타일 시트 (각 128x128, 4-corner Wang, Layout A)")]
    [SerializeField] private Texture2D pathSheet;
    [SerializeField] private Texture2D roadSheet;
    [SerializeField] private Texture2D waterSheet;
    [SerializeField] private Texture2D forestSheet;
    [SerializeField] private Texture2D buildingSheet;

    [Header("렌더")]
    [SerializeField] private Camera mapCamera;
    [SerializeField, Range(0, 4)] private int paddingTiles = 2;

    private double _centerLat, _centerLon;
    private double _gpsLat, _gpsLon;     // 마지막 GPS 위치(리센터용)
    private bool _followGps = true;       // true=플레이어 추적, false=자유 둘러보기(드래그)
    private bool _dragging = false;
    private Vector2 _lastDragPos;
    private GUIStyle _btnStyle;
    private Transform _root;

    private readonly Dictionary<(long, long), SpriteRenderer> _baseActive = new();
    private readonly Stack<SpriteRenderer> _basePool = new();
    private readonly Dictionary<(long, long), SpriteRenderer> _ovActive = new();
    private readonly Stack<SpriteRenderer> _ovPool = new();

    private Sprite[] _grassSprites; // 변형 4종
    private Sprite[][] _autoSheets; // [(int)TileType][cornerIndex 0..15]

    private void Awake()
    {
        if (mapCamera == null) mapCamera = Camera.main;
        if (mapCamera != null && !mapCamera.orthographic)
        {
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = 4.5f;
        }
        var rootGo = new GameObject("EarthTiles");
        rootGo.transform.SetParent(transform, false);
        _root = rootGo.transform;
        _centerLat = initialLat; _centerLon = initialLon;
        _gpsLat = initialLat; _gpsLon = initialLon;
        BuildSprites();
    }

    private void BuildSprites()
    {
        _grassSprites = new Sprite[grassVariants != null ? grassVariants.Length : 0];
        for (int i = 0; i < _grassSprites.Length; i++) _grassSprites[i] = MakeSprite(grassVariants[i]);
        _autoSheets = new Sprite[6][];
        _autoSheets[(int)TileType.Path]     = SliceSheet(pathSheet);
        _autoSheets[(int)TileType.Road]     = SliceSheet(roadSheet);
        _autoSheets[(int)TileType.Water]    = SliceSheet(waterSheet);
        _autoSheets[(int)TileType.Forest]   = SliceSheet(forestSheet);
        _autoSheets[(int)TileType.Building] = SliceSheet(buildingSheet);
    }

    private Sprite MakeSprite(Texture2D t)
    {
        if (t == null) return null;
        t.filterMode = FilterMode.Point;
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height),
            new Vector2(0.5f, 0.5f), 32, 0, SpriteMeshType.FullRect);
    }

    private Sprite[] SliceSheet(Texture2D sheet)
    {
        if (sheet == null) return null;
        sheet.filterMode = FilterMode.Point;
        var sprites = new Sprite[16];
        for (int ci = 0; ci < 16; ci++)
        {
            int col = ci % 4, row = ci / 4;
            float x = col * 32f;
            float y = sheet.height - (row + 1) * 32f; // row0=상단
            if (x < 0 || y < 0 || x + 32 > sheet.width || y + 32 > sheet.height) { sprites[ci] = null; continue; }
            sprites[ci] = Sprite.Create(sheet, new Rect(x, y, 32, 32),
                new Vector2(0.5f, 0.5f), 32, 0, SpriteMeshType.FullRect);
        }
        return sprites;
    }

    public void SetCenter(double lat, double lon)
    {
        if (System.Math.Abs(lat) < 0.001 && System.Math.Abs(lon) < 0.001) return;
        _gpsLat = lat; _gpsLon = lon;
        if (_followGps) { _centerLat = lat; _centerLon = lon; Refresh(); }
        // 자유 둘러보기 중이면 화면 중심은 그대로 두고 GPS만 기록.
    }

    private void Start() => Refresh();

    private TileType TileTypeAt(long tx, long ty)
    {
        var (cx, cy) = GeoTileGrid.TileToChunk(tx, ty);
        var cd = ChunkCache.Instance.TryGet(cx, cy);
        if (cd == null) return TileType.Grass;
        var (otx, oty) = GeoTileGrid.ChunkOriginTile(cx, cy);
        int lx = (int)(tx - otx), ly = (int)(ty - oty);
        int n = GeoTileGrid.ChunkTiles;
        if (lx < 0 || lx >= n || ly < 0 || ly >= n) return TileType.Grass;
        return cd.Tiles[ly, lx];
    }

    private static int Priority(TileType t) => t switch
    {
        TileType.Building => 5, TileType.Water => 4, TileType.Road => 3,
        TileType.Path => 2, TileType.Forest => 1, _ => 0,
    };

    private static void Consider(ref TileType best, ref int bestP, TileType t)
    {
        if (t == TileType.Grass) return;
        int p = Priority(t);
        if (p > bestP) { bestP = p; best = t; }
    }

    private static TileType TopFeature(TileType a, TileType b, TileType c, TileType d)
    {
        TileType best = TileType.Grass; int bestP = 0;
        Consider(ref best, ref bestP, a); Consider(ref best, ref bestP, b);
        Consider(ref best, ref bestP, c); Consider(ref best, ref bestP, d);
        return best;
    }

    // 타일좌표 → 결정론적 해시(같은 위치=항상 같은 변형/flip).
    private static uint TileHash(long tx, long ty)
    {
        ulong h = (ulong)(tx * 73856093L) ^ (ulong)(ty * 19349663L);
        h ^= h >> 13; h *= 0x5bd1e995UL; h ^= h >> 15;
        return (uint)h;
    }

    private void Refresh()
    {
        if (mapCamera == null) return;
        var (centerTxF, centerTyF) = GeoTileGrid.LatLonToTileFractional(_centerLat, _centerLon);
        float halfH = mapCamera.orthographicSize;
        float halfW = halfH * mapCamera.aspect;
        int rangeX = Mathf.CeilToInt(halfW) + paddingTiles;
        int rangeY = Mathf.CeilToInt(halfH) + paddingTiles;
        long centerTx = (long)System.Math.Floor(centerTxF);
        long centerTy = (long)System.Math.Floor(centerTyF);

        // 베이스 grass (데이터 그리드)
        var baseNeeded = new HashSet<(long, long)>();
        for (int dy = -rangeY; dy <= rangeY; dy++)
            for (int dx = -rangeX; dx <= rangeX; dx++)
            {
                long tx = centerTx + dx, ty = centerTy + dy;
                baseNeeded.Add((tx, ty));
                if (!_baseActive.TryGetValue((tx, ty), out var sr)) { sr = AcquireBase(); _baseActive[(tx, ty)] = sr; }
                sr.transform.localPosition = new Vector3((float)(tx + 0.5 - centerTxF), (float)-(ty + 0.5 - centerTyF), 0f);
                if (_grassSprites != null && _grassSprites.Length > 0)
                {
                    uint hh = TileHash(tx, ty);
                    var gs = _grassSprites[hh % (uint)_grassSprites.Length];
                    sr.sprite = gs;
                    sr.flipX = ((hh >> 8) & 1u) == 1u;
                    sr.flipY = ((hh >> 9) & 1u) == 1u;
                }
                else { sr.sprite = null; }
            }
        ReleaseUnneeded(_baseActive, baseNeeded, _basePool);

        // dual-grid 오버레이 (디스플레이 셀 = 데이터 타일 (gx,gy)의 NW 코너)
        var ovNeeded = new HashSet<(long, long)>();
        for (int dy = -rangeY; dy <= rangeY + 1; dy++)
            for (int dx = -rangeX; dx <= rangeX + 1; dx++)
            {
                long gx = centerTx + dx, gy = centerTy + dy;
                TileType nw = TileTypeAt(gx - 1, gy - 1);
                TileType ne = TileTypeAt(gx,     gy - 1);
                TileType sw = TileTypeAt(gx - 1, gy);
                TileType se = TileTypeAt(gx,     gy);
                TileType f = TopFeature(nw, ne, sw, se);
                if (f == TileType.Grass) continue;
                var sheet = _autoSheets[(int)f];
                int ci = (nw == f ? 8 : 0) + (ne == f ? 4 : 0) + (sw == f ? 2 : 0) + (se == f ? 1 : 0);
                Sprite s = (sheet != null) ? sheet[ci] : null;
                if (s == null) continue;
                ovNeeded.Add((gx, gy));
                if (!_ovActive.TryGetValue((gx, gy), out var sr)) { sr = AcquireOverlay(); _ovActive[(gx, gy)] = sr; }
                sr.transform.localPosition = new Vector3((float)(gx - centerTxF), (float)(centerTyF - gy), 0f);
                sr.sprite = s;
                sr.sortingOrder = Priority(f);
            }
        ReleaseUnneeded(_ovActive, ovNeeded, _ovPool);

        var (ccx, ccy) = GeoTileGrid.TileToChunk(centerTx, centerTy);
        ChunkCache.Instance.TrimFar(ccx, ccy, 2);
    }

    private void ReleaseUnneeded(Dictionary<(long, long), SpriteRenderer> active,
        HashSet<(long, long)> needed, Stack<SpriteRenderer> pool)
    {
        List<(long, long)> rm = null;
        foreach (var kv in active) if (!needed.Contains(kv.Key)) (rm ??= new()).Add(kv.Key);
        if (rm != null) foreach (var k in rm)
        {
            var sr = active[k]; sr.sprite = null; sr.gameObject.SetActive(false); pool.Push(sr); active.Remove(k);
        }
    }

    private SpriteRenderer AcquireBase()
    {
        if (_basePool.Count > 0) { var s = _basePool.Pop(); s.gameObject.SetActive(true); return s; }
        var go = new GameObject("ETileBase"); go.transform.SetParent(_root, false);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 0; return sr;
    }

    private SpriteRenderer AcquireOverlay()
    {
        if (_ovPool.Count > 0) { var s = _ovPool.Pop(); s.gameObject.SetActive(true); return s; }
        var go = new GameObject("ETileOverlay"); go.transform.SetParent(_root, false);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 1; return sr;
    }

    private float _t;
    private void Update()
    {
        HandleDrag();
        _t += Time.deltaTime;
        if (_t >= 1f) { _t = 0f; Refresh(); }
    }

    private void HandleDrag()
    {
        if (mapCamera == null) return;
        Vector2 pos; bool active;
        if (Input.touchCount > 0)
        {
            var tch = Input.GetTouch(0);
            pos = tch.position;
            active = tch.phase != TouchPhase.Ended && tch.phase != TouchPhase.Canceled;
        }
        else if (Input.GetMouseButton(0)) { pos = (Vector2)Input.mousePosition; active = true; }
        else { _dragging = false; return; }

        if (!active) { _dragging = false; return; }
        if (!_dragging) { _dragging = true; _lastDragPos = pos; return; }

        Vector2 delta = pos - _lastDragPos;
        _lastDragPos = pos;
        if (delta.sqrMagnitude < 0.25f) return;

        float ppu = Screen.height / (2f * mapCamera.orthographicSize); // 픽셀/타일
        double dTx = delta.x / ppu;
        double dTy = delta.y / ppu;

        _followGps = false;
        var (txf, tyf) = GeoTileGrid.LatLonToTileFractional(_centerLat, _centerLon);
        // 손가락 오른쪽 드래그 → 지도 내용 오른쪽 이동 → 중심 왼쪽: txf 감소.
        // 손가락 위로 드래그(스크린 +y) → 내용 위로 → 중심 남쪽(ty 증가).
        txf -= dTx;
        tyf += dTy;
        var (la, lo) = GeoTileGrid.TileFractionalToLatLon(txf, tyf);
        _centerLat = la; _centerLon = lo;
        Refresh();
    }

    private void OnGUI()
    {
        if (_followGps) return;
        if (_btnStyle == null)
        {
            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.Max(16, Screen.height / 40) };
        }
        float w = Screen.width * 0.30f, h = Screen.height * 0.07f;
        Rect safe = Screen.safeArea;
        float bx = safe.x + safe.width - w - 16f;
        float by = Screen.height - (safe.y) - h - 16f; // 우하단 safe
        if (GUI.Button(new Rect(bx, by, w, h), "내 위치", _btnStyle))
        {
            _followGps = true;
            _centerLat = _gpsLat; _centerLon = _gpsLon;
            _dragging = false;
            Refresh();
        }
    }
}
