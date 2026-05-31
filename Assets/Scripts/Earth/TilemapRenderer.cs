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

    [Header("오브젝트 (나무/덤불/그루터기, 투명·하단앵커)")]
    [SerializeField] private Texture2D treePineA;
    [SerializeField] private Texture2D treePineB;
    [SerializeField] private Texture2D bushTex;
    [SerializeField] private Texture2D stumpTex;

    [Header("건물 → 하우스 (64x64 투명·하단앵커)")]
    [SerializeField] private Texture2D houseA;
    [SerializeField] private Texture2D houseB;

    [Header("잔디 데코 (풀포기/꽃, 16x16 투명·하단앵커)")]
    [SerializeField] private Texture2D tuftA;
    [SerializeField] private Texture2D tuftB;
    [SerializeField] private Texture2D tuftC;
    [SerializeField] private Texture2D flowerWhite;
    [SerializeField] private Texture2D flowerRed;

    [Header("렌더")]
    [SerializeField] private Camera mapCamera;
    [SerializeField, Range(0, 4)] private int paddingTiles = 2;

    private double _centerLat, _centerLon;
    private double _gpsLat, _gpsLon;     // 마지막 GPS 위치(리센터용)
    private bool _followGps = true;       // true=플레이어 추적, false=자유 둘러보기(드래그)
    private bool _dragging = false;
    private Vector2 _lastDragPos;
    private float _prevPinchDist = -1f;
    private GUIStyle _btnStyle;
    private Transform _root;

    private readonly Dictionary<(long, long), SpriteRenderer> _baseActive = new();
    private readonly Stack<SpriteRenderer> _basePool = new();
    private readonly Dictionary<(long, long), SpriteRenderer> _ovActive = new();
    private readonly Stack<SpriteRenderer> _ovPool = new();
    private readonly Dictionary<(long, long), SpriteRenderer> _objActive = new();
    private readonly Stack<SpriteRenderer> _objPool = new();

    private Sprite[] _grassSprites; // 변형 4종
    private Sprite[][] _autoSheets; // [(int)TileType][cornerIndex 0..15]
    private Sprite _treeA, _treeB, _bush, _stump; // 오브젝트(하단앵커)
    private Sprite _houseA, _houseB; // 건물 → 하우스(하단앵커)
    private Sprite _tuftA, _tuftB, _tuftC, _flowerW, _flowerR; // 잔디 데코(하단앵커)

    // 플레이어(GPS)의 화면 중심 대비 GUI 오프셋(px). PlayerAvatar 가 소비.
    public Vector2 PlayerGuiOffset { get; private set; }

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

        _treeA = MakeObjSprite(treePineA);
        _treeB = MakeObjSprite(treePineB);
        _bush  = MakeObjSprite(bushTex);
        _stump = MakeObjSprite(stumpTex);
        _houseA = MakeObjSprite(houseA); _houseB = MakeObjSprite(houseB);

        _tuftA = MakeObjSprite(tuftA); _tuftB = MakeObjSprite(tuftB); _tuftC = MakeObjSprite(tuftC);
        _flowerW = MakeObjSprite(flowerWhite); _flowerR = MakeObjSprite(flowerRed);
    }

    // 오브젝트 스프라이트: 하단-중앙 pivot 으로 베이스가 지면에 닿도록.
    private Sprite MakeObjSprite(Texture2D t)
    {
        if (t == null) return null;
        t.filterMode = FilterMode.Point;
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height),
            new Vector2(0.5f, 0f), pixelsPerUnit: 32, extrude: 0, meshType: SpriteMeshType.FullRect); // pivot bottom-center
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

        // 플레이어(GPS)의 화면 중심 대비 GUI 오프셋(px). 추적 중이면 0(중앙).
        {
            var (gpsTxF, gpsTyF) = GeoTileGrid.LatLonToTileFractional(_gpsLat, _gpsLon);
            float ppu2 = Screen.height / (2f * mapCamera.orthographicSize);
            PlayerGuiOffset = new Vector2(
                (float)((gpsTxF - centerTxF) * ppu2),   // 동쪽(+) = 화면 오른쪽(+x, GUI)
                (float)((gpsTyF - centerTyF) * ppu2));   // 남쪽(ty 증가) = 화면 아래(+y, GUI)
        }

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
                if (f == TileType.Grass || f == TileType.Building) continue; // 건물 풋프린트는 잔디 + 하우스 오브젝트로 표현
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

        // ---- 오브젝트 산포(나무/덤불/그루터기), Y-sort ----
        var objNeeded = new HashSet<(long, long)>();
        for (int dy = -rangeY; dy <= rangeY + 2; dy++) // 위쪽 여유: 캐노피가 위 타일까지 침범
            for (int dx = -rangeX; dx <= rangeX; dx++)
            {
                long tx = centerTx + dx, ty = centerTy + dy;
                Sprite obj = PickObject(tx, ty, out float jx, out float jy, out float scale);
                if (obj == null) continue;
                objNeeded.Add((tx, ty));
                if (!_objActive.TryGetValue((tx, ty), out var sr)) { sr = AcquireObj(); _objActive[(tx, ty)] = sr; }
                float wx = (float)(tx + 0.5 - centerTxF) + jx;
                float wy = (float)-(ty + 0.5 - centerTyF) + jy;
                sr.transform.localPosition = new Vector3(wx, wy, 0f);
                sr.transform.localScale = new Vector3(scale, scale, 1f); // (A) 인스턴스 스케일 변형
                sr.sprite = obj;
                uint hh = TileHash(tx, ty);
                sr.flipX = ((hh >> 16) & 1u) == 1u;
                // Y-sort: 화면 아래(낮은 wy)일수록 앞(높은 order). 지형(0~5) 위로 띄움.
                sr.sortingOrder = 100 + Mathf.RoundToInt((50f - wy) * 8f);
            }
        ReleaseUnneeded(_objActive, objNeeded, _objPool);

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

    // (tx,ty) 주변 반경 r 안에 타입 t 가 하나라도 있나.
    private bool NearType(long tx, long ty, TileType t, int r)
    {
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (TileTypeAt(tx + dx, ty + dy) == t) return true;
            }
        return false;
    }

    // 저주파(coarse 셀) 결정론 노이즈. 4타일 셀 격자에 셀당 난수 → smoothstep 보간 → 0..1.
    // (B)/(C) deco 밀도·꽃밭 패치를 부드러운 군집/공터로 변조하는 데 사용.
    private const uint DecoSeed = 0xA53Fu;   // deco 밀도 클러스터
    private const uint FlowerSeed = 0x1BC7u; // 꽃밭 패치

    private static float CellRand(long cx, long cy, uint seed)
    {
        ulong h = (ulong)(cx * 0x27d4eb2dL) ^ (ulong)(cy * 0x165667b1L) ^ (seed * 0x9e3779b9UL);
        h ^= h >> 13; h *= 0x5bd1e995UL; h ^= h >> 15;
        return (uint)h / 4294967295f;
    }

    private static float ClusterValue(long tx, long ty, uint seed)
    {
        long cx = tx >> 2, cy = ty >> 2;                 // 4타일 셀(산술 시프트=floor)
        float fx = (tx - (cx << 2)) / 4f, fy = (ty - (cy << 2)) / 4f;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy); // smoothstep
        float a = Mathf.Lerp(CellRand(cx, cy, seed),     CellRand(cx + 1, cy, seed),     fx);
        float b = Mathf.Lerp(CellRand(cx, cy + 1, seed), CellRand(cx + 1, cy + 1, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }

    // 타일에 놓을 오브젝트 결정(결정론적 hash). scale=인스턴스 스케일 변형(A).
    // forest=나무 밀도 높음, grass=클러스터 변조된 풀/덤불 + 꽃밭 패치 군락.
    private Sprite PickObject(long tx, long ty, out float jx, out float jy, out float scale)
    {
        uint hh = TileHash(tx, ty);
        jx = (((hh >> 4) & 0xFFu) / 255f - 0.5f) * 0.8f;   // (D) ±0.4 격자감 제거
        jy = (((hh >> 12) & 0xFFu) / 255f - 0.5f) * 0.8f;
        scale = 1f;
        TileType tt = TileTypeAt(tx, ty);

        if (tt == TileType.Building)
        {
            // 풋프린트 좌상단 타일에만 집 1채(왼쪽·위가 건물이면 스킵 → 중복 방지)
            bool leftB = TileTypeAt(tx - 1, ty) == TileType.Building;
            bool upB   = TileTypeAt(tx, ty - 1) == TileType.Building;
            if (!leftB && !upB) { jx = 0.5f; jy = 0.3f; return ((hh >> 1) & 1u) == 0u ? _houseA : _houseB; }
            return null; // 나머지 건물 타일은 비움(잔디)
        }

        float treeScale = 0.82f + (((hh >> 20) & 0xFFu) / 255f) * 0.40f; // 0.82~1.22
        float decoScale = 0.85f + (((hh >> 20) & 0xFFu) / 255f) * 0.30f; // 0.85~1.15

        if (tt == TileType.Forest)
        {
            // 거의 모든 forest 타일에 나무 → 빽빽한 숲
            if ((hh % 100u) < 90u) { scale = treeScale; return ((hh >> 1) & 1u) == 0u ? _treeA : _treeB; }
            scale = decoScale; return _bush; // 나머지는 덤불로 메움
        }
        if (tt == TileType.Grass)
        {
            // 숲 가장자리: 숲에 인접한 잔디 → 나무로 자연스러운 군락 falloff(숲이 잔디로 번짐)
            if (NearType(tx, ty, TileType.Forest, 1) && (hh % 100u) < 55u)
            { scale = treeScale; return ((hh >> 1) & 1u) == 0u ? _treeA : _treeB; }
            // 길/도로 옆 가로수 라인(구도 프레이밍)
            if ((NearType(tx, ty, TileType.Road, 1) || NearType(tx, ty, TileType.Path, 1)) && (hh % 100u) < 22u)
            { scale = treeScale; return ((hh >> 1) & 1u) == 0u ? _treeA : _treeB; }

            // (B) 클러스터 밀도 변조: 공터 ↔ 군집. 평균 밀도는 현행보다 하향.
            float c = ClusterValue(tx, ty, DecoSeed);          // 0..1 저주파
            uint r = hh % 1000u;
            uint tuftCut  = (uint)(c * 430f);                  // 군집에서 최대 ~43%
            uint bushCut  = tuftCut + (uint)(c * c * 45f);     // 덤불은 빽빽한 군집에만
            uint stumpCut = bushCut + 6u;                      // 그루터기 드물게 전역
            if (r < tuftCut)  { scale = decoScale; uint t = (hh >> 5) % 3u; return t == 0u ? _tuftA : (t == 1u ? _tuftB : _tuftC); }
            if (r < bushCut)  { scale = decoScale; return _bush; }
            if (r < stumpCut) { scale = decoScale; return _stump; }

            // (C) 꽃 클럼핑: 꽃밭 패치 셀에서만 군락(패치 밖 잔디엔 거의 없음)
            float fp = ClusterValue(tx, ty, FlowerSeed);
            if (fp > 0.60f)
            {
                float dens = (fp - 0.60f) / 0.40f;             // 패치 내 0..1
                uint fr = (hh >> 7) % 1000u;
                if (fr < (uint)(dens * 520f)) { scale = decoScale; return ((hh >> 6) & 1u) == 0u ? _flowerW : _flowerR; }
            }
            return null;
        }
        return null;
    }

    private SpriteRenderer AcquireObj()
    {
        if (_objPool.Count > 0) { var s = _objPool.Pop(); s.gameObject.SetActive(true); return s; }
        var go = new GameObject("EObj"); go.transform.SetParent(_root, false);
        var sr = go.AddComponent<SpriteRenderer>(); sr.sortingOrder = 100; return sr;
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
#if UNITY_EDITOR
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f) SetZoom(mapCamera.orthographicSize * (1f - scroll * 0.1f));
        if (!Input.GetMouseButton(0)) { _dragging = false; return; }
        Vector2 mpos = (Vector2)Input.mousePosition;
        if (!_dragging) { _dragging = true; _lastDragPos = mpos; return; }
        PanByDelta(mpos - _lastDragPos);
        _lastDragPos = mpos;
#else
        if (Input.touchCount >= 2)
        {
            // 두 손가락 = 핀치 줌만 (팬 안 함)
            var t0 = Input.GetTouch(0); var t1 = Input.GetTouch(1);
            float dist = Vector2.Distance(t0.position, t1.position);
            if (_prevPinchDist > 1f && Mathf.Abs(dist - _prevPinchDist) > 1f)
                SetZoom(mapCamera.orthographicSize * (_prevPinchDist / Mathf.Max(1f, dist)));
            _prevPinchDist = dist;
            _dragging = false; // 팬 상태 리셋(손가락 떼고 한 손가락 전환 시 점프 방지)
            return;
        }
        _prevPinchDist = -1f;
        if (Input.touchCount == 1)
        {
            Vector2 pos = Input.GetTouch(0).position;
            if (!_dragging) { _dragging = true; _lastDragPos = pos; return; }
            PanByDelta(pos - _lastDragPos);
            _lastDragPos = pos;
        }
        else { _dragging = false; }
#endif
    }

    // 화면 픽셀 delta 만큼 지도 중심 이동(자유 둘러보기). 기존 팬 수식 추출.
    private void PanByDelta(Vector2 delta)
    {
        if (delta.sqrMagnitude < 0.25f) return;
        float ppu = Screen.height / (2f * mapCamera.orthographicSize);
        double dTx = delta.x / ppu, dTy = delta.y / ppu;
        _followGps = false;
        var (txf, tyf) = GeoTileGrid.LatLonToTileFractional(_centerLat, _centerLon);
        txf -= dTx; tyf += dTy;
        var (la, lo) = GeoTileGrid.TileFractionalToLatLon(txf, tyf);
        _centerLat = la; _centerLon = lo;
        Refresh();
    }

    private void SetZoom(float ortho)
    {
        float c = Mathf.Clamp(ortho, 2.5f, 9f);
        if (Mathf.Approximately(c, mapCamera.orthographicSize)) return;
        mapCamera.orthographicSize = c;
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
