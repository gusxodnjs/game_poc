// TilemapRenderer.cs — ChunkCache 의 TileType 그리드를 픽셀 스프라이트로 렌더. 화면 주변만 풀.
// 외부 API: SetCenter(lat,lon) — GpsCheck 가 호출(MapView 대체). 1 타일=1 unit. 중심=world 원점.
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class TilemapRenderer : MonoBehaviour
{
    [Header("초기 중심 (서울시청)")]
    [SerializeField] private double initialLat = 37.5663;
    [SerializeField] private double initialLon = 126.9779;

    [Header("타일셋 (Grass/Path/Water/Forest)")]
    [SerializeField] private Texture2D grassTex;
    [SerializeField] private Texture2D pathTex;
    [SerializeField] private Texture2D waterTex;
    [SerializeField] private Texture2D forestTex;

    [Header("렌더")]
    [SerializeField] private Camera mapCamera;
    [SerializeField, Range(0, 4)] private int paddingTiles = 2;

    private double _centerLat, _centerLon;
    private Transform _root;
    private readonly Dictionary<(long, long), SpriteRenderer> _active = new();
    private readonly Stack<SpriteRenderer> _pool = new();
    private Sprite[] _sprites;

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
        BuildSprites();
    }

    private void BuildSprites()
    {
        _sprites = new Sprite[4];
        _sprites[(int)TileType.Grass]  = MakeSprite(grassTex);
        _sprites[(int)TileType.Path]   = MakeSprite(pathTex);
        _sprites[(int)TileType.Water]  = MakeSprite(waterTex);
        _sprites[(int)TileType.Forest] = MakeSprite(forestTex);
    }

    private Sprite MakeSprite(Texture2D t)
    {
        if (t == null) return null;
        t.filterMode = FilterMode.Point;
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height),
            new Vector2(0.5f, 0.5f), pixelsPerUnit: t.width, extrude: 0, meshType: SpriteMeshType.FullRect);
    }

    public void SetCenter(double lat, double lon)
    {
        if (System.Math.Abs(lat) < 0.001 && System.Math.Abs(lon) < 0.001) return;
        _centerLat = lat; _centerLon = lon;
        Refresh();
    }

    private void Start() => Refresh();

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

        var needed = new HashSet<(long, long)>();
        for (int dy = -rangeY; dy <= rangeY; dy++)
            for (int dx = -rangeX; dx <= rangeX; dx++)
            {
                long tx = centerTx + dx, ty = centerTy + dy;
                needed.Add((tx, ty));
                if (!_active.TryGetValue((tx, ty), out var sr))
                {
                    sr = Acquire();
                    _active[(tx, ty)] = sr;
                }
                float wx = (float)(tx + 0.5 - centerTxF);
                float wy = (float)-(ty + 0.5 - centerTyF);
                sr.transform.localPosition = new Vector3(wx, wy, 0f);
                sr.sprite = SpriteForTile(tx, ty);
            }

        List<(long, long)> rm = null;
        foreach (var kv in _active)
            if (!needed.Contains(kv.Key)) (rm ??= new()).Add(kv.Key);
        if (rm != null) foreach (var k in rm) { Release(_active[k]); _active.Remove(k); }

        var (ccx, ccy) = GeoTileGrid.TileToChunk(centerTx, centerTy);
        ChunkCache.Instance.TrimFar(ccx, ccy, 2);
    }

    private Sprite SpriteForTile(long tx, long ty)
    {
        var (cx, cy) = GeoTileGrid.TileToChunk(tx, ty);
        var cd = ChunkCache.Instance.TryGet(cx, cy);
        TileType tt = TileType.Grass;
        if (cd != null)
        {
            var (otx, oty) = GeoTileGrid.ChunkOriginTile(cx, cy);
            int lx = (int)(tx - otx), ly = (int)(ty - oty);
            int n = GeoTileGrid.ChunkTiles;
            if (lx >= 0 && lx < n && ly >= 0 && ly < n) tt = cd.Tiles[ly, lx];
        }
        return _sprites[(int)tt] ?? _sprites[(int)TileType.Grass];
    }

    private SpriteRenderer Acquire()
    {
        if (_pool.Count > 0) { var s = _pool.Pop(); s.gameObject.SetActive(true); return s; }
        var go = new GameObject("ETile");
        go.transform.SetParent(_root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 0;
        return sr;
    }

    private void Release(SpriteRenderer sr)
    {
        sr.sprite = null;
        sr.gameObject.SetActive(false);
        _pool.Push(sr);
    }

    private float _t;
    private void Update()
    {
        _t += Time.deltaTime;
        if (_t >= 1f) { _t = 0f; Refresh(); }
    }
}
