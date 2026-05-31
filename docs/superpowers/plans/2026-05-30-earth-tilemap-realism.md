# 지구 타일맵 리얼리티 개선 (타입 추가 + 오토타일) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`).

**Goal:** 지구 타일맵을 하드 블록 4종에서 → 포장도로/건물 포함 6종 + grass-base 위 피처별 16-타일 오토타일 가장자리 전이로 바꿔 실제 지도처럼 보이게 한다.

**Architecture:** OSM 분류를 세분(highway→Road/Path, building→Building)하고, 렌더러를 멀티패스로 — 모든 칸에 grass 베이스 + 비-grass 피처는 4-이웃 비트마스크(`AutoTile.NeighborMask`)로 인덱싱한 16-타일 시트 스프라이트를 우선순위 sortingOrder로 오버레이. 청크 경계 이웃은 `ChunkCache`를 통해 전역 타일좌표로 조회.

**Tech Stack:** Unity 6 (C#), 기존 Earth 어셈블리, PixelLab(디자이너 오토타일셋). 브랜치 `feat/52-earth-tilemap-pikmin` (PR #53에 이어서).

**참조 스펙:** `docs/superpowers/specs/2026-05-30-earth-tilemap-realism-design.md` · 이슈 #52

---

## File Structure
- Modify `Assets/Scripts/Earth/TileType.cs` — Road=4, Building=5 추가
- Modify `Assets/Scripts/Earth/OverpassParser.cs` — highway 세분 + building 분류
- Modify `Assets/Scripts/Earth/OverpassClient.cs` — building 쿼리 추가
- Modify `Assets/Scripts/Earth/FeatureRasterizer.cs` — Priority 6종
- Modify `Assets/Scripts/Earth/TilemapRenderer.cs` — 멀티패스 오버레이 + 청크경계 mask + 시트 슬라이스
- Modify `Assets/Editor/PocBuildPipeline.cs` — 오토타일 시트 5종 주입
- Tests: `OverpassParserTests.cs`, `FeatureRasterizerTests.cs` (케이스 추가)
- Assets (디자이너): `Assets/world/tiles/{path,road,water,forest,building}_auto_128.png` + `Assets/world/tiles/tileset_layout.md`

---

## Task 1: (디자이너) 오토타일 16-타일 시트 5종 + 레이아웃 매핑

> **디자이너(pixellab-asset-designer) 전담.** 개발자는 PixelLab 호출 금지.

**Files:**
- Create: `Assets/world/tiles/path_auto_128.png`, `road_auto_128.png`, `water_auto_128.png`, `forest_auto_128.png`, `building_auto_128.png`
- Create: `Assets/world/tiles/tileset_layout.md`

- [ ] **Step 1: 5개 피처 오토타일 시트 생성**

각 피처가 **grass 배경으로 자연스럽게 번지는** 16-타일 오토타일 세트(top-down, GBA Pokémon 톤). PixelLab **`create_topdown_tileset`** MCP 도구 우선(오토타일 전이 타일 생성용). 산출 타일을 **128×128 시트(4×4, 32px 셀)**로 배치.
- path = 흙길(밝은 갈색 비포장), road = 포장 차도(아스팔트 회색 + 옅은 중앙선 톤), water = 얕은 물+물가, forest = 짙은 수관+가장자리, building = 지붕/벽 풋프린트.
- 셀 배치 규약(권장): `index = mask(0~15)`, `col = index%4`, `row = index/4` (row 0 = 시트 상단). mask 비트 N=1,E=2,S=4,W=8 = 같은 피처 이웃. 예: mask15=내부 풀필, mask0=사방 가장자리 고립.

- [ ] **Step 2: `file` 검증**
```bash
file Assets/world/tiles/*_auto_128.png   # 모두 "PNG image data, 128 x 128"
```

- [ ] **Step 3: 레이아웃 매핑 문서**

`Assets/world/tiles/tileset_layout.md` — `mask(0~15) → (col,row)` 매핑표. PixelLab 산출이 권장 규약과 다르면 실제 배치대로 표를 작성(개발자 Task 3가 이 표대로 인덱싱). 5개 시트가 모두 동일 배치를 따르도록 통일.

- [ ] **Step 4: Commit**
```bash
git add Assets/world/tiles/*_auto_128.png Assets/world/tiles/tileset_layout.md
git commit -m "feat(earth): 오토타일 16-타일 시트 5종(path/road/water/forest/building) (#52)"
```

---

## Task 2: TileType 확장 + OSM 분류/우선순위 + 파서 테스트

**Files:**
- Modify: `Assets/Scripts/Earth/TileType.cs`, `OverpassParser.cs`, `OverpassClient.cs`, `FeatureRasterizer.cs`
- Test: `Assets/Tests/Earth/OverpassParserTests.cs`, `FeatureRasterizerTests.cs`

- [ ] **Step 1: TileType 확장**

`Assets/Scripts/Earth/TileType.cs` — enum 본문에 추가(값 고정):
```csharp
public enum TileType : byte
{
    Grass = 0,
    Path = 1,
    Water = 2,
    Forest = 3,
    Road = 4,
    Building = 5,
}
```

- [ ] **Step 2: 실패 테스트 추가**

`Assets/Tests/Earth/OverpassParserTests.cs` — 새 테스트 메서드 추가(기존 클래스에):
```csharp
[Test] public void Highway_Primary_Is_Road()
{
    string json = @"{""elements"":[{""type"":""way"",""tags"":{""highway"":""primary""},
      ""geometry"":[{""lat"":37.5,""lon"":127.0},{""lat"":37.5001,""lon"":127.0}]}]}";
    var f = OverpassParser.Parse(json);
    Assert.AreEqual(1, f.Count);
    Assert.AreEqual(TileType.Road, f[0].Type);
    Assert.AreEqual(OsmGeom.Polyline, f[0].Geom);
}

[Test] public void Highway_Footway_Is_Path()
{
    string json = @"{""elements"":[{""type"":""way"",""tags"":{""highway"":""footway""},
      ""geometry"":[{""lat"":37.5,""lon"":127.0},{""lat"":37.5001,""lon"":127.0}]}]}";
    var f = OverpassParser.Parse(json);
    Assert.AreEqual(TileType.Path, f[0].Type);
}

[Test] public void Building_Is_Building_Polygon()
{
    string json = @"{""elements"":[{""type"":""way"",""tags"":{""building"":""yes""},
      ""geometry"":[{""lat"":37.5,""lon"":127.0},{""lat"":37.5,""lon"":127.001},{""lat"":37.501,""lon"":127.001},{""lat"":37.5,""lon"":127.0}]}]}";
    var f = OverpassParser.Parse(json);
    Assert.AreEqual(TileType.Building, f[0].Type);
    Assert.AreEqual(OsmGeom.Polygon, f[0].Geom);
}
```

`Assets/Tests/Earth/FeatureRasterizerTests.cs` — 우선순위 테스트 추가(기존 `ChunkCenterLatLon` 헬퍼 재사용):
```csharp
[Test] public void Building_OverridesWater_Priority()
{
    var (cx, cy) = SeoulChunk();
    var (clat, clon) = ChunkCenterLatLon(cx, cy);
    double d = 0.01;
    System.Collections.Generic.List<(double, double)> Sq() => new(){
        (clat-d,clon-d),(clat-d,clon+d),(clat+d,clon+d),(clat+d,clon-d),(clat-d,clon-d)};
    var water = new OsmFeature { Type = TileType.Water, Geom = OsmGeom.Polygon, Points = Sq() };
    var bld   = new OsmFeature { Type = TileType.Building, Geom = OsmGeom.Polygon, Points = Sq() };
    var cd = FeatureRasterizer.Rasterize(cx, cy, new System.Collections.Generic.List<OsmFeature>{ water, bld });
    Assert.AreEqual(TileType.Building, cd.Tiles[16, 16]);
}
```

- [ ] **Step 3: Run tests — FAIL**
```bash
/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -runTests -projectPath . -testPlatform EditMode -testResults /tmp/r_t2.xml -logFile /tmp/r_t2.log
```
Expected: 새 3+1 테스트 FAIL (Road/Building 미분류).

- [ ] **Step 4: OverpassParser.Classify 갱신**

`Assets/Scripts/Earth/OverpassParser.cs` — `Classify` 교체 + 클래스에 도로 분류 상수 추가:
```csharp
private static readonly System.Collections.Generic.HashSet<string> RoadHighways = new()
{
    "motorway","trunk","primary","secondary","tertiary","unclassified",
    "residential","service","living_street",
    "motorway_link","trunk_link","primary_link","secondary_link","tertiary_link",
};
private static readonly System.Collections.Generic.HashSet<string> BigRoads = new()
{
    "motorway","trunk","primary","secondary","motorway_link","trunk_link","primary_link",
};

private static bool Classify(System.Collections.Generic.Dictionary<string, object> tags,
    out TileType type, out OsmGeom geom, out int buffer)
{
    type = TileType.Grass; geom = OsmGeom.Polyline; buffer = 0;
    if (tags.TryGetValue("highway", out var hwObj) && hwObj is string hw)
    {
        if (RoadHighways.Contains(hw)) { type = TileType.Road; geom = OsmGeom.Polyline; buffer = BigRoads.Contains(hw) ? 2 : 1; return true; }
        type = TileType.Path; geom = OsmGeom.Polyline; buffer = 1; return true; // footway/path/track/...
    }
    if (tags.ContainsKey("building")) { type = TileType.Building; geom = OsmGeom.Polygon; buffer = 0; return true; }
    if (Has(tags, "waterway", "river") || Has(tags, "waterway", "stream"))
        { type = TileType.Water; geom = OsmGeom.Polyline; buffer = 1; return true; }
    if (Has(tags, "natural", "water") || tags.ContainsKey("water") || Has(tags, "waterway", "riverbank"))
        { type = TileType.Water; geom = OsmGeom.Polygon; buffer = 0; return true; }
    if (Has(tags, "landuse", "forest") || Has(tags, "natural", "wood") || Has(tags, "leisure", "park"))
        { type = TileType.Forest; geom = OsmGeom.Polygon; buffer = 0; return true; }
    return false;
}
```

- [ ] **Step 5: OverpassClient building 쿼리 추가**

`Assets/Scripts/Earth/OverpassClient.cs` — `query` 문자열의 way 목록에 building 추가(landuse=forest 줄 다음 등):
```csharp
$"way[\"building\"]({south},{west},{north},{east});" +
```
(기존 highway/water/waterway/landuse/natural/leisure 줄은 유지.)

- [ ] **Step 6: FeatureRasterizer.Priority 6종**

`Assets/Scripts/Earth/FeatureRasterizer.cs` — `Priority` 교체:
```csharp
private static int Priority(TileType t) => t switch
{
    TileType.Building => 5,
    TileType.Water => 4,
    TileType.Road => 3,
    TileType.Path => 2,
    TileType.Forest => 1,
    _ => 0, // Grass
};
```

- [ ] **Step 7: Run tests — PASS**
Re-run the batchmode runTests command. Expected: all tests PASS (기존 14 + 신규 4 = 18, 변경 없으면 그 수).

- [ ] **Step 8: Commit**
```bash
git add Assets/Scripts/Earth/TileType.cs Assets/Scripts/Earth/OverpassParser.cs Assets/Scripts/Earth/OverpassClient.cs Assets/Scripts/Earth/FeatureRasterizer.cs Assets/Tests/Earth/OverpassParserTests.cs Assets/Tests/Earth/FeatureRasterizerTests.cs
git commit -m "feat(earth): Road/Building 타입 + OSM 분류 세분 + 우선순위 (#52)"
```

---

## Task 3: TilemapRenderer 멀티패스 오토타일 오버레이

**Files:**
- Modify: `Assets/Scripts/Earth/TilemapRenderer.cs`

렌더러를 단일 스프라이트 → **칸당 base(grass) + overlay(피처 오토타일)** 2-레이어로. 청크 경계 이웃은 `ChunkCache`로 전역 조회.

- [ ] **Step 1: TilemapRenderer 교체**

`Assets/Scripts/Earth/TilemapRenderer.cs` 전체를 아래로 교체(기존 카메라/패닝/풀링 규약 계승, `SetCenter(double,double)` 시그니처 유지):
```csharp
// TilemapRenderer.cs — grass 베이스 + 피처별 16-타일 오토타일 오버레이 멀티패스.
// 외부 API: SetCenter(lat,lon). 1 타일=1 unit, 중심=world 원점. 아바타는 화면중앙 고정(별도).
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class TilemapRenderer : MonoBehaviour
{
    [Header("초기 중심 (서울시청)")]
    [SerializeField] private double initialLat = 37.5663;
    [SerializeField] private double initialLon = 126.9779;

    [Header("베이스 (grass 단일 32x32)")]
    [SerializeField] private Texture2D grassTex;

    [Header("오토타일 시트 (각 128x128, 16-타일 4x4)")]
    [SerializeField] private Texture2D pathSheet;
    [SerializeField] private Texture2D roadSheet;
    [SerializeField] private Texture2D waterSheet;
    [SerializeField] private Texture2D forestSheet;
    [SerializeField] private Texture2D buildingSheet;

    [Header("렌더")]
    [SerializeField] private Camera mapCamera;
    [SerializeField, Range(0, 4)] private int paddingTiles = 2;

    private double _centerLat, _centerLon;
    private Transform _root;

    private class TileSlot { public SpriteRenderer Base; public SpriteRenderer Overlay; }
    private readonly Dictionary<(long, long), TileSlot> _active = new();
    private readonly Stack<TileSlot> _pool = new();

    private Sprite _grassSprite;
    // _autoSheets[(int)TileType][mask 0..15]. Grass 인덱스는 null(베이스 전용).
    private Sprite[][] _autoSheets;

    // mask(0..15) → 시트 셀(col,row). 디자이너 tileset_layout.md 와 일치해야 함.
    // 기본: col=mask%4, row=mask/4 (row 0 = 시트 상단). 디자이너 배치가 다르면 이 표만 수정.
    private static readonly (int col, int row)[] MaskCell = BuildDefaultMaskCells();
    private static (int, int)[] BuildDefaultMaskCells()
    {
        var a = new (int, int)[16];
        for (int m = 0; m < 16; m++) a[m] = (m % 4, m / 4);
        return a;
    }

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
        _grassSprite = MakeSprite(grassTex);
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
            new Vector2(0.5f, 0.5f), pixelsPerUnit: 32, extrude: 0, meshType: SpriteMeshType.FullRect);
    }

    // 128x128 시트 → mask별 32x32 Sprite 16장. 실패/null → null 배열(렌더러가 베이스 폴백).
    private Sprite[] SliceSheet(Texture2D sheet)
    {
        if (sheet == null) return null;
        sheet.filterMode = FilterMode.Point;
        var sprites = new Sprite[16];
        for (int m = 0; m < 16; m++)
        {
            var (col, row) = MaskCell[m];
            // Unity 텍스처 y는 하단 기준 → row 0(상단)은 y = height-32.
            float x = col * 32f;
            float y = sheet.height - (row + 1) * 32f;
            if (x < 0 || y < 0 || x + 32 > sheet.width || y + 32 > sheet.height) { sprites[m] = null; continue; }
            sprites[m] = Sprite.Create(sheet, new Rect(x, y, 32, 32),
                new Vector2(0.5f, 0.5f), pixelsPerUnit: 32, extrude: 0, meshType: SpriteMeshType.FullRect);
        }
        return sprites;
    }

    public void SetCenter(double lat, double lon)
    {
        if (System.Math.Abs(lat) < 0.001 && System.Math.Abs(lon) < 0.001) return;
        _centerLat = lat; _centerLon = lon;
        Refresh();
    }

    private void Start() => Refresh();

    // 전역 타일좌표의 TileType (청크 경계 넘어 조회). 미로드 → Grass.
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

    // 같은 피처 4-이웃 비트마스크 (N=1,E=2,S=4,W=8). 청크경계 안전.
    private int NeighborMask(long tx, long ty, TileType self)
    {
        int m = 0;
        if (TileTypeAt(tx, ty - 1) == self) m |= 1; // N
        if (TileTypeAt(tx + 1, ty) == self) m |= 2; // E
        if (TileTypeAt(tx, ty + 1) == self) m |= 4; // S
        if (TileTypeAt(tx - 1, ty) == self) m |= 8; // W
        return m;
    }

    private static int Priority(TileType t) => t switch
    {
        TileType.Building => 5, TileType.Water => 4, TileType.Road => 3,
        TileType.Path => 2, TileType.Forest => 1, _ => 0,
    };

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
                if (!_active.TryGetValue((tx, ty), out var slot))
                {
                    slot = Acquire();
                    _active[(tx, ty)] = slot;
                }
                float wx = (float)(tx + 0.5 - centerTxF);
                float wy = (float)-(ty + 0.5 - centerTyF);
                slot.Base.transform.localPosition = new Vector3(wx, wy, 0f);
                slot.Overlay.transform.localPosition = new Vector3(wx, wy, 0f);

                slot.Base.sprite = _grassSprite;
                TileType tt = TileTypeAt(tx, ty);
                if (tt == TileType.Grass)
                {
                    slot.Overlay.sprite = null;
                }
                else
                {
                    var sheet = _autoSheets[(int)tt];
                    int mask = NeighborMask(tx, ty, tt);
                    Sprite s = (sheet != null) ? sheet[mask] : null;
                    slot.Overlay.sprite = s;            // 시트 없으면 null → grass 베이스만(폴백)
                    slot.Overlay.sortingOrder = Priority(tt);
                }
            }

        List<(long, long)> rm = null;
        foreach (var kv in _active)
            if (!needed.Contains(kv.Key)) (rm ??= new()).Add(kv.Key);
        if (rm != null) foreach (var k in rm) { Release(_active[k]); _active.Remove(k); }

        var (ccx, ccy) = GeoTileGrid.TileToChunk(centerTx, centerTy);
        ChunkCache.Instance.TrimFar(ccx, ccy, 2);
    }

    private TileSlot Acquire()
    {
        if (_pool.Count > 0) { var s = _pool.Pop(); s.Base.gameObject.SetActive(true); s.Overlay.gameObject.SetActive(true); return s; }
        var baseGo = new GameObject("ETileBase");
        baseGo.transform.SetParent(_root, false);
        var baseSr = baseGo.AddComponent<SpriteRenderer>();
        baseSr.sortingOrder = 0;
        var ovGo = new GameObject("ETileOverlay");
        ovGo.transform.SetParent(_root, false);
        var ovSr = ovGo.AddComponent<SpriteRenderer>();
        ovSr.sortingOrder = 1;
        return new TileSlot { Base = baseSr, Overlay = ovSr };
    }

    private void Release(TileSlot slot)
    {
        slot.Base.sprite = null; slot.Overlay.sprite = null;
        slot.Base.gameObject.SetActive(false);
        slot.Overlay.gameObject.SetActive(false);
        _pool.Push(slot);
    }

    private float _t;
    private void Update()
    {
        _t += Time.deltaTime;
        if (_t >= 1f) { _t = 0f; Refresh(); }
    }
}
```

- [ ] **Step 2: 컴파일 확인 (EditMode 테스트로 게이트)**
```bash
/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -runTests -projectPath . -testPlatform EditMode -testResults /tmp/r_t3.xml -logFile /tmp/r_t3.log
grep -c "error CS" /tmp/r_t3.log   # 0
```
Expected: 0 컴파일 에러, 기존+신규 테스트 PASS(렌더러는 테스트 없음, 컴파일만 확인).

- [ ] **Step 3: Commit**
```bash
git add Assets/Scripts/Earth/TilemapRenderer.cs
git commit -m "feat(earth): TilemapRenderer grass-base + 오토타일 오버레이 멀티패스 (#52)"
```

---

## Task 4: SetupHelloScene 시트 주입 + 씬 재생성 + 빌드 + 검증

**Files:**
- Modify: `Assets/Editor/PocBuildPipeline.cs`
- 산출물: `Assets/Scenes/HelloScene.unity`

- [ ] **Step 1: 타일셋 경로/주입 갱신**

`Assets/Editor/PocBuildPipeline.cs` — `TilesetPaths`를 grass + 5개 시트로 교체하고, SetupHelloScene 주입 블록을 시트 5종 SerializeField에 맞게 갱신:
```csharp
private static readonly string[] TilesetPaths = {
    TilesetDir + "/grass_32.png",
    TilesetDir + "/path_auto_128.png",
    TilesetDir + "/road_auto_128.png",
    TilesetDir + "/water_auto_128.png",
    TilesetDir + "/forest_auto_128.png",
    TilesetDir + "/building_auto_128.png",
};
```
`EnsureTilesetTextureSettings()`는 그대로(모든 TilesetPaths에 Sprite/Point/no-mip/PPU32 적용 — 시트도 동일 PPU32, Single sprite여도 코드 슬라이스이므로 무방).
SetupHelloScene 의 TilemapRenderer 주입부 교체:
```csharp
var tmSo = new SerializedObject(tilemap);
tmSo.FindProperty("grassTex").objectReferenceValue     = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/grass_32.png");
tmSo.FindProperty("pathSheet").objectReferenceValue    = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/path_auto_128.png");
tmSo.FindProperty("roadSheet").objectReferenceValue    = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/road_auto_128.png");
tmSo.FindProperty("waterSheet").objectReferenceValue   = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/water_auto_128.png");
tmSo.FindProperty("forestSheet").objectReferenceValue  = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/forest_auto_128.png");
tmSo.FindProperty("buildingSheet").objectReferenceValue= AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/building_auto_128.png");
tmSo.ApplyModifiedPropertiesWithoutUndo();
int tilesetLoaded = 0;
foreach (var path in TilesetPaths) if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) tilesetLoaded++;
Debug.Log("[POC] TilemapRenderer wired: tileset=" + tilesetLoaded + "/6");
```
(GpsRoot/DiscoveryRoot/PlayerRoot/SaveScene/UpdateBuildScenes 등 나머지는 유지.)

- [ ] **Step 2: 씬 재생성 + 로그 검증**
```bash
/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . -logFile /tmp/r_setup.log -executeMethod PocBuildPipeline.SetupHelloScene
grep -E "TilemapRenderer wired|PlayerAvatar wired|tilemap SerializedProperty|Scene saved" /tmp/r_setup.log
```
Expected: `TilemapRenderer wired: tileset=6/6`, PlayerAvatar 4/4, wiring 경고 없음, Scene saved.

- [ ] **Step 3: iOS 빌드 무결성**
```bash
/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . -logFile /tmp/r_build.log -executeMethod PocBuildPipeline.DoAll
grep -c "error CS" /tmp/r_build.log   # 0
```
디스크 모니터링(>1GB 유지). 환경사유 실패면 deferred 기록, 컴파일 에러면 BLOCKER.

- [ ] **Step 4: Editor 수동 시각 검증**

HelloScene 열고 `initialLat/Lon`을 도로·건물·물이 풍부한 좌표(예 `37.5663,126.9779` 시청 주변)로 두고 Play → 도로(회색)·흙길·건물·물·숲이 구분되고 가장자리가 grass로 부드럽게 전이되는지, 블록 경계가 사라졌는지 확인. (실기기 GPS는 후속.)

- [ ] **Step 5: 산출물 커밋 + 푸시**
```bash
git add Assets/Editor/PocBuildPipeline.cs Assets/Scenes/HelloScene.unity Assets/Scenes/HelloScene.unity.meta Assets/world/tiles/*_auto_128.png.meta ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(earth): HelloScene 오토타일 시트 배선 + 씬 재생성 (#52)"
git push
```
(PR #53 갱신. 무관한 packages-lock/PlanetIntro/Splash churn은 스테이지 금지.)

---

## Self-Review (작성자 체크)
**1. Spec coverage:** §2 타입확장(T2)·OSM분류(T2)·우선순위(T2) ✔; §3 오토타일 16-타일/AutoTile 재활용/디자이너 계약(T1)·인덱싱(T3) ✔; §4 멀티패스(T3)·청크경계 mask(T3 `TileTypeAt`/`NeighborMask`)·시트 슬라이스(T3 `SliceSheet`) ✔; §5 폴백(T3 시트 null→베이스) ✔; §6 테스트(T2 파서/래스터) ✔; §7 단계=T1~T4 ✔.
**2. Placeholder scan:** 모든 코드 스텝 실제 코드. 디자이너 `tileset_layout.md` 매핑은 산출물(계약), `MaskCell` 기본표 제공 + "디자이너 배치 다르면 이 표만 수정" 명시 → 플레이스홀더 아님.
**3. Type consistency:** `TileType{Grass..Building}` 값 일치(파서/래스터/렌더 Priority 6종 동일 순서 Building5>Water4>Road3>Path2>Forest1>Grass0) ✔; 렌더러 SerializeField명 `grassTex/pathSheet/roadSheet/waterSheet/forestSheet/buildingSheet` = SetupHelloScene FindProperty 일치 ✔; `SliceSheet`/`TileTypeAt`/`NeighborMask`/`MaskCell` 시그니처 자기참조 일치 ✔; `GeoTileGrid.LatLonToTileFractional` (v1에 추가됨) 사용 ✔.

**알려진 트레이드오프:** T3 `MaskCell` 기본표(col=mask%4,row=mask/4)는 디자이너 `tileset_layout.md`와 반드시 대조 — 불일치 시 T4 수동검증에서 가장자리가 어긋나 보이므로, T3/T4 사이에 디자이너 매핑표대로 `MaskCell` 조정 필요. 16-타일이라 인너코너는 단순(스펙 §8 후속).
