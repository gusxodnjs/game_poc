# 지구 산책 레이어 — Pikmin 실지형 픽셀 타일맵 (v1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** HelloScene의 지구 레이어를 OSM 래스터 지도에서 실제 도로·물·녹지를 픽셀 타일로 그리는 Pikmin 스타일 타일맵으로 교체한다.

**Architecture:** GPS 좌표를 전역 3m 타일 그리드로 매핑(`GeoTileGrid`) → 주변 청크의 OSM 벡터를 Overpass로 페치(`OverpassClient`) → 칸별 `TileType`으로 래스터화(`FeatureRasterizer`) → 디스크 캐시(`ChunkCache`) → 4-이웃 오토타일로 코드드리븐 렌더(`TilemapRenderer`). 아바타는 화면 중앙 고정, 맵이 패닝.

**Tech Stack:** Unity 6000.0.75f1 (C#), `UnityWebRequest`, Overpass API (OSM), PixelLab(타일셋 아트). 기존 `GeoCoord`/`PlayerAvatar`/`GpsCheck`/`DiscoveryDetection` 재활용.

**참조 스펙:** `docs/superpowers/specs/2026-05-30-earth-tilemap-pikmin-design.md` · **이슈:** #52 · **브랜치:** `feat/52-earth-tilemap-pikmin`

---

## File Structure

신규 (모두 `Assets/Scripts/Earth/`):
- `TileType.cs` — 타일 분류 enum
- `GeoTileGrid.cs` — lat/lon ↔ 전역 3m 타일/청크 좌표 (순수)
- `OsmFeature.cs` — 파싱된 OSM 피처 모델 (way 폴리라인/폴리곤 + 분류)
- `OverpassParser.cs` — Overpass JSON → `List<OsmFeature>` (순수)
- `OverpassClient.cs` — bbox 페치 (MonoBehaviour 싱글톤)
- `FeatureRasterizer.cs` — 피처 → 청크 `TileType[,]` (순수)
- `ChunkData.cs` — 청크 1개의 `TileType[,]` + 직렬화
- `ChunkCache.cs` — 청크 페치/래스터화/디스크캐시/로드언로드 (MonoBehaviour 싱글톤)
- `AutoTile.cs` — `TileType[,]` + 좌표 → 타일셋 스프라이트 인덱스 (순수)
- `TilemapRenderer.cs` — 그리드 렌더 + 패닝 + 풀링 (MonoBehaviour)

수정:
- `Assets/Scripts/GpsCheck.cs` — `mapView`(MapView) → `tilemap`(TilemapRenderer) 참조 교체
- `Assets/Editor/PocBuildPipeline.cs:47-118` — `SetupHelloScene` 배선 교체

삭제:
- `Assets/Scripts/MapView.cs` (+`.meta`), `Assets/Scripts/TileCache.cs` (+`.meta`)

자산 생성:
- `scripts/gen_earth_tileset.py` — PixelLab로 `Assets/world/tiles/` 타일셋 PNG 생성

테스트 (EditMode, asmdef 분리·빌드 제외):
- `Assets/Tests/Earth/GeoTileGridTests.cs`, `OverpassParserTests.cs`, `FeatureRasterizerTests.cs`, `AutoTileTests.cs`
- `Assets/Tests/Earth/EarthTests.asmdef` (`includePlatforms: [Editor]`, `overrideReferences: false`)

> ⚠️ **빌드 안정성:** 과거 `fix/remove-tests-poc-stage`로 Tests가 iOS 빌드를 막았다. asmdef를 **Editor 전용**으로 두고, Task 11에서 batchmode 빌드가 깨지지 않는지 반드시 확인한다. 빌드가 깨지면 테스트 코드를 제거하고 순수함수는 수동 검증으로 대체한다(빌드 > 커버리지).

---

## Task 1: TileType enum + GeoTileGrid (전역 타일/청크 좌표)

**Files:**
- Create: `Assets/Scripts/Earth/TileType.cs`
- Create: `Assets/Scripts/Earth/GeoTileGrid.cs`
- Create: `Assets/Tests/Earth/EarthTests.asmdef`
- Test: `Assets/Tests/Earth/GeoTileGridTests.cs`

- [ ] **Step 1: asmdef 작성 (Editor 전용)**

`Assets/Tests/Earth/EarthTests.asmdef`:
```json
{
    "name": "EarthTests",
    "references": ["UnityEngine.TestRunner", "UnityEditor.TestRunner"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "overrideReferences": false,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "noEngineReferences": false
}
```
> 게임 스크립트(`Assets/Scripts/`)는 asmdef가 없어 Assembly-CSharp에 들어가므로, 테스트 asmdef는 별도 reference 불필요(자동으로 Assembly-CSharp 참조). `UNITY_INCLUDE_TESTS` 제약으로 빌드시 제외.

- [ ] **Step 2: Write the failing test**

`Assets/Tests/Earth/GeoTileGridTests.cs`:
```csharp
using NUnit.Framework;

public class GeoTileGridTests
{
    [Test]
    public void SameLocation_GivesSameTile()
    {
        var a = GeoTileGrid.LatLonToTile(37.5663, 126.9779);
        var b = GeoTileGrid.LatLonToTile(37.5663, 126.9779);
        Assert.AreEqual(a, b);
    }

    [Test]
    public void NearbyPoints_WithinTileMeters_ShareTile()
    {
        // 서울시청. 위도 0.00001° ≈ 1.1m < 3m 타일 → 같은 칸이어야 한다.
        var a = GeoTileGrid.LatLonToTile(37.5663, 126.9779);
        var b = GeoTileGrid.LatLonToTile(37.56631, 126.9779);
        Assert.AreEqual(a, b);
    }

    [Test]
    public void FarPoints_DifferentTile()
    {
        var a = GeoTileGrid.LatLonToTile(37.5663, 126.9779);
        var b = GeoTileGrid.LatLonToTile(37.5673, 126.9779); // ~111m 북
        Assert.AreNotEqual(a, b);
    }

    [Test]
    public void TileToChunk_FloorsNegativeCorrectly()
    {
        Assert.AreEqual((-1L, -1L), GeoTileGrid.TileToChunk(-1, -1));
        Assert.AreEqual((0L, 0L), GeoTileGrid.TileToChunk(31, 31));
        Assert.AreEqual((1L, 1L), GeoTileGrid.TileToChunk(32, 32));
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Unity Test Runner(EditMode) 또는 batchmode:
```bash
/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -runTests -projectPath . -testPlatform EditMode \
  -testResults /tmp/earth_t1.xml -logFile /tmp/earth_t1.log
```
Expected: FAIL — `GeoTileGrid` 미정의(컴파일 에러).

- [ ] **Step 4: TileType enum 작성**

`Assets/Scripts/Earth/TileType.cs`:
```csharp
// 지구 레이어 타일 분류. v1 = Grass/Path/Water/Forest. v2 에서 Building 추가.
public enum TileType : byte
{
    Grass = 0,
    Path = 1,
    Water = 2,
    Forest = 3,
    // Building = 4,  // v2
}
```

- [ ] **Step 5: GeoTileGrid 구현**

`Assets/Scripts/Earth/GeoTileGrid.cs`:
```csharp
// GeoTileGrid.cs
// lat/lon ↔ 전역 픽셀 타일 그리드(타일 1칸 ≈ 3m). Web Mercator world meter 기반.
// 같은 실제 위치는 항상 같은 (tx,ty) → 결정론적. 순수 함수, allocation 없음.
//
// 타일 크기 메모: TileMeters 는 Web Mercator meter 기준. Mercator 는 위도가
// 높을수록 지면거리를 늘려 표현하므로, 한국(위도 ~37.5°)에서 지면 체감 ≈
// TileMeters * cos(37.5°) ≈ 3.0 * 0.79 ≈ 2.4m. PoC 허용. 필요시 상수 튜닝.
using System;

public static class GeoTileGrid
{
    public const double TileMeters = 3.0;     // Web Mercator meter / 타일
    public const int ChunkTiles = 32;         // 청크 = 32×32 타일 (≈96m)
    private const double EarthRadius = 6378137.0;
    private const double OriginShift = Math.PI * EarthRadius; // 20037508.34 (적도 반바퀴)
    private const double LatLimit = 85.05112878;

    /// <summary>위경도 → Web Mercator world meter (원점=적도·본초자오선, +x 동, +y 북).</summary>
    public static (double mx, double my) LatLonToMercatorMeters(double lat, double lon)
    {
        if (lat > LatLimit) lat = LatLimit;
        else if (lat < -LatLimit) lat = -LatLimit;
        double mx = lon * OriginShift / 180.0;
        double my = Math.Log(Math.Tan((90.0 + lat) * Math.PI / 360.0)) / (Math.PI / 180.0);
        my = my * OriginShift / 180.0;
        return (mx, my);
    }

    /// <summary>위경도 → 전역 타일 좌표. ty 는 북쪽이 작아지도록(화면 위=작은 y) flip.</summary>
    public static (long tx, long ty) LatLonToTile(double lat, double lon)
    {
        var (mx, my) = LatLonToMercatorMeters(lat, lon);
        long tx = (long)Math.Floor((mx + OriginShift) / TileMeters);
        long ty = (long)Math.Floor((OriginShift - my) / TileMeters);
        return (tx, ty);
    }

    /// <summary>전역 타일 좌표 → 청크 좌표 (음수도 바닥 나눗셈).</summary>
    public static (long cx, long cy) TileToChunk(long tx, long ty)
    {
        return (FloorDiv(tx, ChunkTiles), FloorDiv(ty, ChunkTiles));
    }

    /// <summary>청크 좌상단(최소 tx,ty) 타일 좌표.</summary>
    public static (long tx, long ty) ChunkOriginTile(long cx, long cy)
    {
        return (cx * ChunkTiles, cy * ChunkTiles);
    }

    /// <summary>타일 좌표 → 그 타일 중심의 위경도 (래스터라이저 점-in-폴리곤 샘플용).</summary>
    public static (double lat, double lon) TileCenterLatLon(long tx, long ty)
    {
        double mx = (tx + 0.5) * TileMeters - OriginShift;
        double my = OriginShift - (ty + 0.5) * TileMeters;
        double lon = mx / OriginShift * 180.0;
        double lat = my / OriginShift * 180.0;
        lat = 180.0 / Math.PI * (2.0 * Math.Atan(Math.Exp(lat * Math.PI / 180.0)) - Math.PI / 2.0);
        return (lat, lon);
    }

    private static long FloorDiv(long a, long b)
    {
        long q = a / b;
        if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
        return q;
    }
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: 위 batchmode runTests 재실행. Expected: 4 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Earth/TileType.cs Assets/Scripts/Earth/GeoTileGrid.cs Assets/Tests/Earth/
git commit -m "feat(earth): GeoTileGrid 전역 3m 타일/청크 좌표 + TileType (#52)"
```

---

## Task 2: PixelLab 베이스 타일셋 생성

**Files:**
- Create: `scripts/gen_earth_tileset.py`
- Output: `Assets/world/tiles/{grass,path,water,forest}_32.png` (32×32, 16-타일 Wang 시트 또는 개별)

- [ ] **Step 1: 타일셋 생성 스크립트 작성**

16-타일 Wang을 위해 각 타입은 **베이스 + 4-이웃 전이**가 필요. v1 단순화: 베이스 타일 1장씩 먼저 생성(오토타일은 Task 7에서 코드로 블렌딩/마스킹). PixelLab MCP 사용(메모리 `pixellab_mcp_quirks`: size=256 → review, inline polling 필수).

`scripts/gen_earth_tileset.py` — 기존 `scripts/gen_assets.py` 패턴 따라 `pixflux` 호출:
```python
# 32×32 픽셀 타일. 톱다운, 심리스, 픽셀아트. 각 타입 베이스.
PROMPTS = {
    "grass_32":  "seamless top-down pixel-art grass tile, lush green, subtle texture, 32x32, GBA Pokemon style",
    "path_32":   "seamless top-down pixel-art dirt path tile, light brown packed earth, 32x32, GBA Pokemon style",
    "water_32":  "seamless top-down pixel-art shallow water tile, blue with ripples, 32x32, GBA Pokemon style",
    "forest_32": "seamless top-down pixel-art dense forest canopy tile, dark green trees, 32x32, GBA Pokemon style",
}
OUT_DIR = "Assets/world/tiles"
# 각 PixelLab generate-image-pixflux 호출 → base64 decode → PNG 검증 → 저장 (+_result.json)
```
> PixelLab 최소 캔버스 32×32 — 그대로 32 생성. 메모리 `pixellab_polling_quirk`: 저장 후 `file` 로 PNG 시그니처 검증 필수.

- [ ] **Step 2: 생성 실행 + 검증**

```bash
python3 scripts/gen_earth_tileset.py
file Assets/world/tiles/*_32.png   # 모두 "PNG image data, 32 x 32" 확인
```
Expected: 4개 PNG, 각 32×32, 유효 시그니처.

- [ ] **Step 3: Unity import 설정 (Point filter, no-mip, sprite)**

`PocBuildPipeline`에 `EnsureTilesetTextureSettings()` 추가(기존 `EnsurePlayerTextureSettings` 패턴 복제): `filterMode=Point`, `mipmapEnabled=false`, `textureType=Sprite`, `spritePixelsPerUnit=32`, `alphaIsTransparency=true`. Task 10에서 호출.

- [ ] **Step 4: Commit**

```bash
git add scripts/gen_earth_tileset.py Assets/world/tiles/
git commit -m "feat(earth): PixelLab 베이스 타일셋 grass/path/water/forest (#52)"
```

---

## Task 3: OSM 피처 모델 + Overpass 파서

**Files:**
- Create: `Assets/Scripts/Earth/OsmFeature.cs`
- Create: `Assets/Scripts/Earth/OverpassParser.cs`
- Test: `Assets/Tests/Earth/OverpassParserTests.cs`

분류 규칙(태그 → TileType):
- `highway=*` (footway/path/residential/...) → **Path** (way = 폴리라인)
- `natural=water` / `water=*` / `waterway=riverbank` → **Water** (way = 폴리곤)
- `waterway=river|stream` → **Water** (폴리라인, 버퍼)
- `landuse=forest` / `natural=wood` / `leisure=park`(나무로 표현) → **Forest** (폴리곤)
- 그 외 → 무시(기본 Grass)

- [ ] **Step 1: OsmFeature 모델 작성**

`Assets/Scripts/Earth/OsmFeature.cs`:
```csharp
using System.Collections.Generic;

public enum OsmGeom { Polyline, Polygon }

public class OsmFeature
{
    public TileType Type;        // Path / Water / Forest
    public OsmGeom Geom;
    public List<(double lat, double lon)> Points = new List<(double, double)>();
    // 폴리라인(도로/강) 버퍼 반경(타일 수). Path=1, 강=1. 폴리곤은 0.
    public int BufferTiles;
}
```

- [ ] **Step 2: Write the failing test**

`Assets/Tests/Earth/OverpassParserTests.cs`:
```csharp
using NUnit.Framework;
using System.Collections.Generic;

public class OverpassParserTests
{
    // Overpass JSON: way + 노드 좌표가 geometry 로 inline ("out geom;").
    private const string Json = @"{
      ""elements"": [
        { ""type"":""way"", ""id"":1, ""tags"":{""highway"":""footway""},
          ""geometry"":[{""lat"":37.5,""lon"":127.0},{""lat"":37.5001,""lon"":127.0}] },
        { ""type"":""way"", ""id"":2, ""tags"":{""natural"":""water""},
          ""geometry"":[{""lat"":37.5,""lon"":127.0},{""lat"":37.5,""lon"":127.001},{""lat"":37.501,""lon"":127.0}] },
        { ""type"":""way"", ""id"":3, ""tags"":{""amenity"":""cafe""},
          ""geometry"":[{""lat"":37.5,""lon"":127.0}] }
      ]
    }";

    [Test]
    public void Parses_Highway_As_Path_Polyline()
    {
        var feats = OverpassParser.Parse(Json);
        var path = feats.Find(f => f.Type == TileType.Path);
        Assert.IsNotNull(path);
        Assert.AreEqual(OsmGeom.Polyline, path.Geom);
        Assert.AreEqual(2, path.Points.Count);
    }

    [Test]
    public void Parses_Water_As_Polygon()
    {
        var feats = OverpassParser.Parse(Json);
        var water = feats.Find(f => f.Type == TileType.Water);
        Assert.IsNotNull(water);
        Assert.AreEqual(OsmGeom.Polygon, water.Geom);
    }

    [Test]
    public void Ignores_Unclassified_Tags()
    {
        var feats = OverpassParser.Parse(Json);
        Assert.AreEqual(2, feats.Count); // cafe 무시
    }

    [Test]
    public void EmptyOrMalformed_ReturnsEmpty_NoThrow()
    {
        Assert.AreEqual(0, OverpassParser.Parse("").Count);
        Assert.AreEqual(0, OverpassParser.Parse("{}").Count);
        Assert.AreEqual(0, OverpassParser.Parse("garbage").Count);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: batchmode runTests. Expected: FAIL — `OverpassParser` 미정의.

- [ ] **Step 4: OverpassParser 구현**

Unity의 `JsonUtility`는 dictionary(tags) 미지원 → 가벼운 수동 파서 사용(`SimpleJson` 도입 회피, PoC 의존성 최소화). 구조가 단순하므로 `MiniJSON` 스타일 재귀 파서를 내장.

`Assets/Scripts/Earth/OverpassParser.cs`:
```csharp
// OverpassParser.cs
// Overpass "out geom;" JSON → List<OsmFeature>. 순수 함수.
// 의존성 회피 위해 최소 JSON 파서 내장(중첩 object/array/string/number/bool/null).
using System.Collections.Generic;

public static class OverpassParser
{
    public static List<OsmFeature> Parse(string json)
    {
        var result = new List<OsmFeature>();
        if (string.IsNullOrEmpty(json)) return result;

        object root;
        try { root = Json.Deserialize(json); }
        catch { return result; }

        if (!(root is Dictionary<string, object> obj)) return result;
        if (!obj.TryGetValue("elements", out var elsObj) || !(elsObj is List<object> els)) return result;

        foreach (var e in els)
        {
            if (!(e is Dictionary<string, object> el)) continue;
            if (!(el.TryGetValue("tags", out var tObj) && tObj is Dictionary<string, object> tags)) continue;
            if (!(el.TryGetValue("geometry", out var gObj) && gObj is List<object> geom)) continue;

            if (!Classify(tags, out TileType type, out OsmGeom defaultGeom, out int buffer)) continue;

            var feat = new OsmFeature { Type = type, BufferTiles = buffer };
            foreach (var p in geom)
            {
                if (p is Dictionary<string, object> pt
                    && pt.TryGetValue("lat", out var la) && pt.TryGetValue("lon", out var lo))
                {
                    feat.Points.Add((ToD(la), ToD(lo)));
                }
            }
            if (feat.Points.Count == 0) continue;

            // 폐합 여부로 폴리곤/폴리라인 판정 (강/도로는 강제 폴리라인).
            bool closed = feat.Points.Count >= 4
                && feat.Points[0] == feat.Points[feat.Points.Count - 1];
            feat.Geom = (defaultGeom == OsmGeom.Polygon && closed) ? OsmGeom.Polygon : OsmGeom.Polyline;
            if (defaultGeom == OsmGeom.Polyline) feat.Geom = OsmGeom.Polyline;
            result.Add(feat);
        }
        return result;
    }

    // 태그 → 분류. 반환 false 면 무시.
    private static bool Classify(Dictionary<string, object> tags, out TileType type, out OsmGeom geom, out int buffer)
    {
        type = TileType.Grass; geom = OsmGeom.Polyline; buffer = 0;
        if (tags.ContainsKey("highway")) { type = TileType.Path; geom = OsmGeom.Polyline; buffer = 1; return true; }
        if (Has(tags, "waterway", "river") || Has(tags, "waterway", "stream"))
            { type = TileType.Water; geom = OsmGeom.Polyline; buffer = 1; return true; }
        if (Has(tags, "natural", "water") || tags.ContainsKey("water") || Has(tags, "waterway", "riverbank"))
            { type = TileType.Water; geom = OsmGeom.Polygon; buffer = 0; return true; }
        if (Has(tags, "landuse", "forest") || Has(tags, "natural", "wood") || Has(tags, "leisure", "park"))
            { type = TileType.Forest; geom = OsmGeom.Polygon; buffer = 0; return true; }
        return false;
    }

    private static bool Has(Dictionary<string, object> t, string k, string v)
        => t.TryGetValue(k, out var got) && got is string s && s == v;

    private static double ToD(object o)
        => o is double d ? d : (o is long l ? l : double.Parse(o.ToString(),
            System.Globalization.CultureInfo.InvariantCulture));
}
```

- [ ] **Step 5: 최소 JSON 파서 추가**

`Assets/Scripts/Earth/Json.cs` — MiniJSON(공개 도메인, Calvin Rien) 축약본 포함. `Json.Deserialize(string) → object` (Dictionary/List/string/double/long/bool/null). 표준 MiniJSON 소스를 그대로 둔다(약 200줄). 라이선스 헤더 유지.
> 출처: https://gist.github.com/darktable/1411710 (MIT/공개도메인). 파일 상단에 출처·라이선스 주석.

- [ ] **Step 6: Run test to verify it passes**

Run: batchmode runTests. Expected: 4 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Earth/OsmFeature.cs Assets/Scripts/Earth/OverpassParser.cs Assets/Scripts/Earth/Json.cs Assets/Tests/Earth/OverpassParserTests.cs
git commit -m "feat(earth): Overpass JSON 파서 + OSM 피처 분류 (#52)"
```

---

## Task 4: FeatureRasterizer (피처 → 청크 TileType 그리드)

**Files:**
- Create: `Assets/Scripts/Earth/ChunkData.cs`
- Create: `Assets/Scripts/Earth/FeatureRasterizer.cs`
- Test: `Assets/Tests/Earth/FeatureRasterizerTests.cs`

알고리즘: 청크(cx,cy)의 32×32 칸 각각에 대해 우선순위 **Water > Path > Forest > Grass**로 칠한다.
- 폴리곤(Water/Forest): 칸 중심 위경도 → 타일좌표, 폴리곤 점-in-폴리곤(ray casting).
- 폴리라인(Path/강): 각 세그먼트를 타일좌표로 변환 후 Bresenham + BufferTiles 두께로 칠함.

- [ ] **Step 1: ChunkData 작성**

`Assets/Scripts/Earth/ChunkData.cs`:
```csharp
// 청크 1개의 32×32 TileType. 디스크 직렬화는 칸당 1바이트.
public class ChunkData
{
    public readonly long Cx, Cy;
    public readonly TileType[,] Tiles; // [localY, localX], 0..31

    public ChunkData(long cx, long cy)
    {
        Cx = cx; Cy = cy;
        Tiles = new TileType[GeoTileGrid.ChunkTiles, GeoTileGrid.ChunkTiles];
        // 기본 Grass (enum 0)
    }

    public byte[] Serialize()
    {
        int n = GeoTileGrid.ChunkTiles;
        var bytes = new byte[n * n];
        int i = 0;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                bytes[i++] = (byte)Tiles[y, x];
        return bytes;
    }

    public static ChunkData Deserialize(long cx, long cy, byte[] bytes)
    {
        var cd = new ChunkData(cx, cy);
        int n = GeoTileGrid.ChunkTiles;
        if (bytes == null || bytes.Length != n * n) return cd;
        int i = 0;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                cd.Tiles[y, x] = (TileType)bytes[i++];
        return cd;
    }
}
```

- [ ] **Step 2: Write the failing test**

`Assets/Tests/Earth/FeatureRasterizerTests.cs`:
```csharp
using NUnit.Framework;
using System.Collections.Generic;

public class FeatureRasterizerTests
{
    // 청크 (0이 아닌 실제 좌표) 를 한 곳 고정: 서울시청 근처 타일 → 청크.
    private (long cx, long cy) SeoulChunk()
    {
        var (tx, ty) = GeoTileGrid.LatLonToTile(37.5663, 126.9779);
        return GeoTileGrid.TileToChunk(tx, ty);
    }

    [Test]
    public void EmptyFeatures_AllGrass()
    {
        var (cx, cy) = SeoulChunk();
        var cd = FeatureRasterizer.Rasterize(cx, cy, new List<OsmFeature>());
        Assert.AreEqual(TileType.Grass, cd.Tiles[0, 0]);
        Assert.AreEqual(TileType.Grass, cd.Tiles[31, 31]);
    }

    [Test]
    public void WaterPolygon_CoveringChunk_FillsWater()
    {
        var (cx, cy) = SeoulChunk();
        // 청크 전체를 덮는 큰 사각 폴리곤(청크 중심 ± 충분히 큰 위경도).
        var (clat, clon) = ChunkCenterLatLon(cx, cy);
        double d = 0.01; // ~1km, 청크(96m)보다 훨씬 큼
        var poly = new OsmFeature { Type = TileType.Water, Geom = OsmGeom.Polygon };
        poly.Points.Add((clat - d, clon - d));
        poly.Points.Add((clat - d, clon + d));
        poly.Points.Add((clat + d, clon + d));
        poly.Points.Add((clat + d, clon - d));
        poly.Points.Add((clat - d, clon - d)); // 폐합
        var cd = FeatureRasterizer.Rasterize(cx, cy, new List<OsmFeature> { poly });
        Assert.AreEqual(TileType.Water, cd.Tiles[16, 16]);
    }

    [Test]
    public void Water_OverridesForest_Priority()
    {
        var (cx, cy) = SeoulChunk();
        var (clat, clon) = ChunkCenterLatLon(cx, cy);
        double d = 0.01;
        List<(double, double)> Square()
        {
            return new List<(double, double)>{
                (clat-d,clon-d),(clat-d,clon+d),(clat+d,clon+d),(clat+d,clon-d),(clat-d,clon-d)};
        }
        var forest = new OsmFeature { Type = TileType.Forest, Geom = OsmGeom.Polygon, Points = Square() };
        var water  = new OsmFeature { Type = TileType.Water,  Geom = OsmGeom.Polygon, Points = Square() };
        var cd = FeatureRasterizer.Rasterize(cx, cy, new List<OsmFeature> { forest, water });
        Assert.AreEqual(TileType.Water, cd.Tiles[16, 16]); // Water 우선
    }

    private (double lat, double lon) ChunkCenterLatLon(long cx, long cy)
    {
        var (tx, ty) = GeoTileGrid.ChunkOriginTile(cx, cy);
        return GeoTileGrid.TileCenterLatLon(tx + 16, ty + 16);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: batchmode runTests. Expected: FAIL — `FeatureRasterizer` 미정의.

- [ ] **Step 4: FeatureRasterizer 구현**

`Assets/Scripts/Earth/FeatureRasterizer.cs`:
```csharp
// FeatureRasterizer.cs
// OSM 피처 목록 → 청크 32×32 TileType 그리드. 순수 함수.
// 우선순위: Water > Path > Forest > Grass(기본). 낮은 우선순위가 높은 걸 못 덮음.
using System.Collections.Generic;

public static class FeatureRasterizer
{
    private static int Priority(TileType t) => t switch
    {
        TileType.Water => 3,
        TileType.Path => 2,
        TileType.Forest => 1,
        _ => 0,
    };

    public static ChunkData Rasterize(long cx, long cy, List<OsmFeature> features)
    {
        int n = GeoTileGrid.ChunkTiles;
        var cd = new ChunkData(cx, cy);
        var (originTx, originTy) = GeoTileGrid.ChunkOriginTile(cx, cy);

        // 폴리곤 먼저(Forest→Water 순서 무관, 우선순위로 가드).
        foreach (var f in features)
        {
            if (f.Geom == OsmGeom.Polygon)
                RasterizePolygon(cd, originTx, originTy, f);
            else
                RasterizePolyline(cd, originTx, originTy, f);
        }
        return cd;
    }

    private static void Paint(ChunkData cd, int lx, int ly, TileType t)
    {
        int n = GeoTileGrid.ChunkTiles;
        if (lx < 0 || lx >= n || ly < 0 || ly >= n) return;
        if (Priority(t) >= Priority(cd.Tiles[ly, lx])) cd.Tiles[ly, lx] = t;
    }

    private static void RasterizePolygon(ChunkData cd, long originTx, long originTy, OsmFeature f)
    {
        int n = GeoTileGrid.ChunkTiles;
        // 폴리곤 점들을 타일 좌표(double)로 변환.
        var pts = new List<(double x, double y)>(f.Points.Count);
        foreach (var (lat, lon) in f.Points)
        {
            var (tx, ty) = GeoTileGrid.LatLonToTile(lat, lon);
            pts.Add((tx, ty));
        }
        // 청크 각 칸 중심을 점-in-폴리곤 테스트.
        for (int ly = 0; ly < n; ly++)
            for (int lx = 0; lx < n; lx++)
            {
                double sx = originTx + lx + 0.5;
                double sy = originTy + ly + 0.5;
                if (PointInPolygon(sx, sy, pts)) Paint(cd, lx, ly, f.Type);
            }
    }

    private static void RasterizePolyline(ChunkData cd, long originTx, long originTy, OsmFeature f)
    {
        for (int i = 0; i + 1 < f.Points.Count; i++)
        {
            var (tx0, ty0) = GeoTileGrid.LatLonToTile(f.Points[i].lat, f.Points[i].lon);
            var (tx1, ty1) = GeoTileGrid.LatLonToTile(f.Points[i + 1].lat, f.Points[i + 1].lon);
            DrawLine(cd, originTx, originTy, tx0, ty0, tx1, ty1, f.Type, f.BufferTiles);
        }
    }

    // Bresenham + 반경 buffer(맨해튼) 두께.
    private static void DrawLine(ChunkData cd, long oTx, long oTy,
        long x0, long y0, long x1, long y1, TileType t, int buffer)
    {
        long dx = System.Math.Abs(x1 - x0), dy = -System.Math.Abs(y1 - y0);
        long sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        long err = dx + dy;
        while (true)
        {
            for (int by = -buffer; by <= buffer; by++)
                for (int bx = -buffer; bx <= buffer; bx++)
                    Paint(cd, (int)(x0 + bx - oTx), (int)(y0 + by - oTy), t);
            if (x0 == x1 && y0 == y1) break;
            long e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }

    // ray casting. pts 는 폐합 가정(마지막=처음 아니어도 wrap 처리).
    private static bool PointInPolygon(double px, double py, List<(double x, double y)> pts)
    {
        bool inside = false;
        int j = pts.Count - 1;
        for (int i = 0; i < pts.Count; i++)
        {
            if (((pts[i].y > py) != (pts[j].y > py)) &&
                (px < (pts[j].x - pts[i].x) * (py - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x))
                inside = !inside;
            j = i;
        }
        return inside;
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: batchmode runTests. Expected: 3 tests PASS (+이전 태스크 테스트 모두 PASS).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Earth/ChunkData.cs Assets/Scripts/Earth/FeatureRasterizer.cs Assets/Tests/Earth/FeatureRasterizerTests.cs
git commit -m "feat(earth): FeatureRasterizer 피처→청크 TileType 그리드 (#52)"
```

---

## Task 5: AutoTile (4-이웃 비트마스크 → 스프라이트 인덱스)

**Files:**
- Create: `Assets/Scripts/Earth/AutoTile.cs`
- Test: `Assets/Tests/Earth/AutoTileTests.cs`

v1 16-타일 Wang: 한 타일의 상/하/좌/우 이웃이 **같은 타입인지** 비트(N=1,E=2,S=4,W=8)로 0~15 인덱스. 베이스 타일이 1장뿐인 v1에서는 인덱스를 "가장자리 페더링"에 쓰되, 최소 동작은 Grass 위 Path/Water/Forest를 베이스로 깔고 인덱스는 렌더러가 가장자리 알파/오프셋에 활용. 본 태스크는 **순수 인덱스 계산만** 책임.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Earth/AutoTileTests.cs`:
```csharp
using NUnit.Framework;

public class AutoTileTests
{
    private TileType[,] Grid()
    {
        // 3×3, 중앙(1,1)=Path, 상·우 이웃=Path, 하·좌=Grass
        var g = new TileType[3, 3];
        g[1, 1] = TileType.Path;
        g[0, 1] = TileType.Path; // 위(N)
        g[1, 2] = TileType.Path; // 오른(E)
        return g;
    }

    [Test]
    public void Bitmask_NorthEastSame_Gives_N_plus_E()
    {
        int mask = AutoTile.NeighborMask(Grid(), 1, 1);
        // N=1, E=2 → 3
        Assert.AreEqual(3, mask);
    }

    [Test]
    public void Bitmask_Isolated_Zero()
    {
        var g = new TileType[3, 3];
        g[1, 1] = TileType.Water; // 이웃 모두 Grass
        Assert.AreEqual(0, AutoTile.NeighborMask(g, 1, 1));
    }

    [Test]
    public void Bitmask_OutOfBounds_TreatedAsDifferent()
    {
        var g = new TileType[1, 1];
        g[0, 0] = TileType.Path;
        Assert.AreEqual(0, AutoTile.NeighborMask(g, 0, 0)); // 모든 이웃 OOB → 0
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: batchmode runTests. Expected: FAIL — `AutoTile` 미정의.

- [ ] **Step 3: AutoTile 구현**

`Assets/Scripts/Earth/AutoTile.cs`:
```csharp
// AutoTile.cs
// 4-이웃 비트마스크 (N=1,E=2,S=4,W=8). 같은 타입 이웃이면 비트 set.
// 경계 밖은 "다른 타입" 취급(가장자리 그려짐). 순수 함수.
public static class AutoTile
{
    public static int NeighborMask(TileType[,] grid, int x, int y)
    {
        var self = grid[y, x];
        int mask = 0;
        if (Same(grid, x, y - 1, self)) mask |= 1; // N
        if (Same(grid, x + 1, y, self)) mask |= 2; // E
        if (Same(grid, x, y + 1, self)) mask |= 4; // S
        if (Same(grid, x - 1, y, self)) mask |= 8; // W
        return mask;
    }

    private static bool Same(TileType[,] grid, int x, int y, TileType self)
    {
        int h = grid.GetLength(0), w = grid.GetLength(1);
        if (x < 0 || x >= w || y < 0 || y >= h) return false;
        return grid[y, x] == self;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: batchmode runTests. Expected: 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Earth/AutoTile.cs Assets/Tests/Earth/AutoTileTests.cs
git commit -m "feat(earth): AutoTile 4-이웃 비트마스크 (#52)"
```

---

## Task 6: OverpassClient (bbox 벡터 페치)

**Files:**
- Create: `Assets/Scripts/Earth/OverpassClient.cs`

네트워크 의존이라 단위테스트 없음 — Task 11 수동 검증. `TileCache` 싱글톤 패턴 차용.

- [ ] **Step 1: OverpassClient 구현**

`Assets/Scripts/Earth/OverpassClient.cs`:
```csharp
// OverpassClient.cs
// Overpass API 로 bbox 내 도로/물/녹지 벡터 페치. 싱글톤 MonoBehaviour.
// OSM 매너: User-Agent 필수, 동시요청 1개, 실패시 지수 백오프.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class OverpassClient : MonoBehaviour
{
    private const string Endpoint = "https://overpass-api.de/api/interpreter";
    private const string UserAgent = "terra-poc/0.2 (gusxodnjs@gmail.com)";

    private static OverpassClient _instance;
    public static OverpassClient Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("OverpassClient");
                _instance = go.AddComponent<OverpassClient>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private bool _busy;

    /// <summary>bbox(south,west,north,east) 페치 → onDone(json or null). 동시요청 1개로 직렬화.</summary>
    public IEnumerator Fetch(double south, double west, double north, double east, Action<string> onDone)
    {
        while (_busy) yield return null;
        _busy = true;
        string query =
            $"[out:json][timeout:25];(" +
            $"way[\"highway\"]({south},{west},{north},{east});" +
            $"way[\"natural\"=\"water\"]({south},{west},{north},{east});" +
            $"way[\"water\"]({south},{west},{north},{east});" +
            $"way[\"waterway\"]({south},{west},{north},{east});" +
            $"way[\"landuse\"=\"forest\"]({south},{west},{north},{east});" +
            $"way[\"natural\"=\"wood\"]({south},{west},{north},{east});" +
            $"way[\"leisure\"=\"park\"]({south},{west},{north},{east});" +
            $");out geom;";

        string result = null;
        int attempt = 0;
        while (attempt < 2 && result == null)
        {
            using (var req = UnityWebRequest.Post(Endpoint, "data=" + UnityWebRequest.EscapeURL(query)))
            {
                req.SetRequestHeader("User-Agent", UserAgent);
                req.timeout = 30;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                    result = req.downloadHandler.text;
                else
                {
                    Debug.LogWarning($"[Earth] Overpass fail (attempt {attempt}): {req.error}");
                    yield return new WaitForSeconds(2f * (attempt + 1)); // 백오프
                }
            }
            attempt++;
        }
        _busy = false;
        onDone?.Invoke(result);
    }
}
```

- [ ] **Step 2: 컴파일 확인 + Commit**

batchmode 컴파일(아래 Task 10/11에서 통합 확인) 전, 단독 컴파일 깨짐 없는지 Editor 콘솔 확인.
```bash
git add Assets/Scripts/Earth/OverpassClient.cs
git commit -m "feat(earth): OverpassClient bbox 벡터 페치 (#52)"
```

---

## Task 7: ChunkCache (페치·래스터화·디스크 캐시·로드/언로드)

**Files:**
- Create: `Assets/Scripts/Earth/ChunkCache.cs`

- [ ] **Step 1: ChunkCache 구현**

`Assets/Scripts/Earth/ChunkCache.cs`:
```csharp
// ChunkCache.cs
// 청크 단위 ChunkData 관리: 메모리 캐시 + 디스크 캐시 + Overpass 페치→래스터화.
// 싱글톤 MonoBehaviour. 검은화면 금지: 미로드 청크는 null → 렌더러가 Grass placeholder.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ChunkCache : MonoBehaviour
{
    private static ChunkCache _instance;
    public static ChunkCache Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("ChunkCache");
                _instance = go.AddComponent<ChunkCache>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private readonly Dictionary<(long, long), ChunkData> _mem = new();
    private readonly HashSet<(long, long)> _loading = new();
    private string _dir;

    private void Awake()
    {
        _dir = Path.Combine(Application.persistentDataPath, "earthtiles");
        Directory.CreateDirectory(_dir);
    }

    /// <summary>메모리에 있으면 반환, 없으면 null(+백그라운드 로드 시작).</summary>
    public ChunkData TryGet(long cx, long cy)
    {
        var key = (cx, cy);
        if (_mem.TryGetValue(key, out var cd)) return cd;
        if (!_loading.Contains(key)) StartCoroutine(Load(cx, cy));
        return null;
    }

    private string PathFor(long cx, long cy) => Path.Combine(_dir, $"{cx}_{cy}.bin");

    private IEnumerator Load(long cx, long cy)
    {
        var key = (cx, cy);
        _loading.Add(key);

        // 1) 디스크 캐시
        string p = PathFor(cx, cy);
        if (File.Exists(p))
        {
            byte[] bytes = null;
            try { bytes = File.ReadAllBytes(p); } catch { }
            if (bytes != null)
            {
                _mem[key] = ChunkData.Deserialize(cx, cy, bytes);
                _loading.Remove(key);
                yield break;
            }
        }

        // 2) Overpass 페치 → 래스터화 (청크 bbox = 청크 모서리 위경도 + 1타일 여유)
        var (otx, oty) = GeoTileGrid.ChunkOriginTile(cx, cy);
        int n = GeoTileGrid.ChunkTiles;
        var (latNW, lonNW) = GeoTileGrid.TileCenterLatLon(otx - 1, oty - 1);
        var (latSE, lonSE) = GeoTileGrid.TileCenterLatLon(otx + n, oty + n);
        double south = Mathf.Min((float)latNW, (float)latSE);
        double north = Mathf.Max((float)latNW, (float)latSE);
        double west = Mathf.Min((float)lonNW, (float)lonSE);
        double east = Mathf.Max((float)lonNW, (float)lonSE);

        string json = null;
        yield return OverpassClient.Instance.Fetch(south, west, north, east, r => json = r);

        ChunkData cd;
        if (!string.IsNullOrEmpty(json))
        {
            var feats = OverpassParser.Parse(json);
            cd = FeatureRasterizer.Rasterize(cx, cy, feats);
            try { File.WriteAllBytes(p, cd.Serialize()); } catch { } // 캐시 저장(실패 무시)
        }
        else
        {
            // 페치 실패 → 전부 Grass. 캐시 저장 안 함(다음에 재시도).
            cd = new ChunkData(cx, cy);
        }
        _mem[key] = cd;
        _loading.Remove(key);
    }

    /// <summary>중심에서 먼 청크 메모리 언로드(디스크는 유지). radius 청크 밖 제거.</summary>
    public void TrimFar(long centerCx, long centerCy, int radius)
    {
        List<(long, long)> rm = null;
        foreach (var k in _mem.Keys)
        {
            if (System.Math.Abs(k.Item1 - centerCx) > radius || System.Math.Abs(k.Item2 - centerCy) > radius)
                (rm ??= new()).Add(k);
        }
        if (rm != null) foreach (var k in rm) _mem.Remove(k);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Earth/ChunkCache.cs
git commit -m "feat(earth): ChunkCache 페치·래스터화·디스크캐시 (#52)"
```

---

## Task 8: TilemapRenderer (렌더 + 패닝 + 풀링)

**Files:**
- Create: `Assets/Scripts/Earth/TilemapRenderer.cs`

`MapView`의 카메라/풀링/패닝 규약 계승: 1 타일 = 1 Unity unit, 중심 위경도를 world 원점, ortho 카메라. 외부 API `SetCenter(lat, lon)` (GpsCheck 호환).

- [ ] **Step 1: TilemapRenderer 구현**

`Assets/Scripts/Earth/TilemapRenderer.cs`:
```csharp
// TilemapRenderer.cs
// ChunkCache 의 TileType 그리드를 픽셀 스프라이트로 렌더. 화면 주변 타일만 풀로 표시.
// 외부 API: SetCenter(lat,lon) — GpsCheck 가 호출(MapView 대체).
// 1 타일 = 1 Unity unit. 중심 위경도가 world 원점(0,0). 아바타는 화면중앙 고정.
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class TilemapRenderer : MonoBehaviour
{
    [Header("초기 중심 (서울시청)")]
    [SerializeField] private double initialLat = 37.5663;
    [SerializeField] private double initialLon = 126.9779;

    [Header("타일셋 (Grass/Path/Water/Forest 순)")]
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
    private Sprite[] _sprites; // TileType 인덱스별 1장 (v1: 베이스만)

    private void Awake()
    {
        if (mapCamera == null) mapCamera = Camera.main;
        if (mapCamera != null && !mapCamera.orthographic)
        {
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = 4.5f; // 화면높이 9 unit ≈ 9타일 ≈ 27m 가시
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
        var (cMx, cMy) = GeoTileGrid.LatLonToMercatorMeters(_centerLat, _centerLon);
        // 중심 타일(소수) — world 원점 정렬용.
        double centerTxF = (cMx + Mercator()) / GeoTileGrid.TileMeters;
        double centerTyF = (Mercator() - cMy) / GeoTileGrid.TileMeters;

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
                // world 위치: 중심 타일 소수 정렬, Y 부호 반전(north-up).
                float wx = (float)(tx + 0.5 - centerTxF);
                float wy = (float)-(ty + 0.5 - centerTyF);
                sr.transform.localPosition = new Vector3(wx, wy, 0f);
                sr.sprite = SpriteForTile(tx, ty);
            }

        // 잉여 회수
        List<(long, long)> rm = null;
        foreach (var kv in _active)
            if (!needed.Contains(kv.Key)) (rm ??= new()).Add(kv.Key);
        if (rm != null) foreach (var k in rm) { Release(_active[k]); _active.Remove(k); }

        // 먼 청크 메모리 정리
        var (ccx, ccy) = GeoTileGrid.TileToChunk(centerTx, centerTy);
        ChunkCache.Instance.TrimFar(ccx, ccy, 2);
    }

    private Sprite SpriteForTile(long tx, long ty)
    {
        var (cx, cy) = GeoTileGrid.TileToChunk(tx, ty);
        var cd = ChunkCache.Instance.TryGet(cx, cy);
        TileType tt = TileType.Grass; // placeholder = Grass (검은화면 금지)
        if (cd != null)
        {
            var (otx, oty) = GeoTileGrid.ChunkOriginTile(cx, cy);
            int lx = (int)(tx - otx), ly = (int)(ty - oty);
            int n = GeoTileGrid.ChunkTiles;
            if (lx >= 0 && lx < n && ly >= 0 && ly < n) tt = cd.Tiles[ly, lx];
        }
        return _sprites[(int)tt] ?? _sprites[(int)TileType.Grass];
    }

    private double Mercator() => System.Math.PI * 6378137.0; // OriginShift

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

    // 청크가 비동기 로드되면 다음 GPS 갱신/주기 호출 때 자연 교체. 1초마다 강제 갱신.
    private float _t;
    private void Update()
    {
        _t += Time.deltaTime;
        if (_t >= 1f) { _t = 0f; Refresh(); }
    }
}
```
> 참고: v1은 베이스 타일만 사용(오토타일 인덱스는 `AutoTile`로 계산해 두되 렌더 적용은 v3 디테일에서). 화면 가시 ≈ 27m로 잡아(ortho 4.5) Pikmin 줌감.

- [ ] **Step 2: Commit**

```bash
git add Assets/Scripts/Earth/TilemapRenderer.cs
git commit -m "feat(earth): TilemapRenderer 렌더+패닝+풀링 (#52)"
```

---

## Task 9: GpsCheck 재배선 + 구 파일 삭제

**Files:**
- Modify: `Assets/Scripts/GpsCheck.cs`
- Delete: `Assets/Scripts/MapView.cs` (+`.meta`), `Assets/Scripts/TileCache.cs` (+`.meta`)

- [ ] **Step 1: GpsCheck 의 MapView 참조를 TilemapRenderer 로 교체**

`Assets/Scripts/GpsCheck.cs` 변경:
- L8: `[SerializeField] private MapView mapView;` → `[SerializeField] private TilemapRenderer tilemap;`
- L7 Tooltip 문구: "MapView" → "TilemapRenderer"
- L80: `if (mapView == null) return;` → `if (tilemap == null) return;`
- L92: `mapView.SetCenter(lat, lon);` → `tilemap.SetCenter(lat, lon);`

(`SetCenter(double,double)` 시그니처 동일하므로 호출부 외 변경 없음.)

- [ ] **Step 2: 구 파일 삭제**

```bash
git rm Assets/Scripts/MapView.cs Assets/Scripts/MapView.cs.meta \
       Assets/Scripts/TileCache.cs Assets/Scripts/TileCache.cs.meta
```

- [ ] **Step 3: 잔존 참조 없음 확인**

```bash
grep -rn "MapView\|TileCache" Assets/Scripts Assets/Editor || echo "no refs"
```
Expected: `PocBuildPipeline.cs`의 참조만 남음(Task 10에서 교체). 그 외 없음.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/GpsCheck.cs
git commit -m "refactor(earth): GpsCheck→TilemapRenderer 재배선 + 구 래스터 MapView/TileCache 삭제 (#52)"
```

---

## Task 10: PocBuildPipeline.SetupHelloScene 배선 교체

**Files:**
- Modify: `Assets/Editor/PocBuildPipeline.cs:47-118` (`SetupHelloScene`) + 타일셋 경로 상수/import 헬퍼 추가

- [ ] **Step 1: 타일셋 경로 상수 + import 헬퍼 추가**

`PocBuildPipeline` 상수부(L41 부근)에 추가:
```csharp
private const string TilesetDir = "Assets/world/tiles";
private static readonly (TileTypeKey key, string path)[] TilesetPaths = {
    (TileTypeKey.Grass,  TilesetDir + "/grass_32.png"),
    (TileTypeKey.Path,   TilesetDir + "/path_32.png"),
    (TileTypeKey.Water,  TilesetDir + "/water_32.png"),
    (TileTypeKey.Forest, TilesetDir + "/forest_32.png"),
};
private enum TileTypeKey { Grass, Path, Water, Forest }
```
타일셋 import 헬퍼(`EnsurePlayerTextureSettings` 복제):
```csharp
private static void EnsureTilesetTextureSettings()
{
    foreach (var (_, path) in TilesetPaths)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) continue;
        importer.textureType = TextureImporterType.Sprite;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 32;
        importer.SaveAndReimport();
    }
}
```

- [ ] **Step 2: SetupHelloScene 의 MapRoot 블록 교체**

`PocBuildPipeline.cs` L54-87 의 카메라/MapRoot/GpsCheck 배선을 다음으로 교체:
```csharp
// 카메라: TilemapRenderer 가 SpriteRenderer 기반 → Orthographic.
var cam = Camera.main;
if (cam != null)
{
    cam.orthographic = true;
    cam.orthographicSize = 4.5f; // ≈27m 가시 (Pikmin 줌)
    cam.transform.position = new Vector3(0f, 0f, -10f);
    cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = new Color32(0x2e, 0x7d, 0x32, 0xff); // 풀밭 톤 placeholder
    cam.nearClipPlane = 0.1f;
    cam.farClipPlane = 100f;
}

EnsureTilesetTextureSettings();

var map = new GameObject("MapRoot");
var tilemap = map.AddComponent<TilemapRenderer>();
// 타일셋 텍스처 4종 주입 (SerializeField: grassTex/pathTex/waterTex/forestTex)
var tmSo = new SerializedObject(tilemap);
tmSo.FindProperty("grassTex").objectReferenceValue  = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/grass_32.png");
tmSo.FindProperty("pathTex").objectReferenceValue   = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/path_32.png");
tmSo.FindProperty("waterTex").objectReferenceValue  = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/water_32.png");
tmSo.FindProperty("forestTex").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(TilesetDir + "/forest_32.png");
tmSo.ApplyModifiedPropertiesWithoutUndo();
int tilesetLoaded = 0;
foreach (var (_, path) in TilesetPaths)
    if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) != null) tilesetLoaded++;
Debug.Log("[POC] TilemapRenderer wired: tileset=" + tilesetLoaded + "/4");

var gps = new GameObject("GpsRoot");
var gpsCheck = gps.AddComponent<GpsCheck>();
var so = new SerializedObject(gpsCheck);
var prop = so.FindProperty("tilemap"); // MapView→TilemapRenderer 로 필드명 변경됨
if (prop != null)
{
    prop.objectReferenceValue = tilemap;
    so.ApplyModifiedPropertiesWithoutUndo();
}
else
{
    Debug.LogWarning("[POC] GpsCheck.tilemap SerializedProperty not found — GPS→Tilemap wiring skipped.");
}
```
> `DiscoveryRoot`/`PlayerRoot` 배선(L89-112)과 `SaveScene`/`UpdateBuildScenes`(L114-117)는 그대로 유지.

- [ ] **Step 3: Commit**

```bash
git add Assets/Editor/PocBuildPipeline.cs
git commit -m "feat(earth): SetupHelloScene 타일맵 배선 교체 (#52)"
```

---

## Task 11: 통합 빌드/씬 재생성 + 수동 검증

**Files:** (산출물) `Assets/Scenes/HelloScene.unity`, `ProjectSettings/EditorBuildSettings.asset`

- [ ] **Step 1: EditMode 테스트 전체 통과 확인**

```bash
/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -runTests -projectPath . -testPlatform EditMode \
  -testResults /tmp/earth_all.xml -logFile /tmp/earth_all.log
```
Expected: GeoTileGrid/OverpassParser/FeatureRasterizer/AutoTile 테스트 전부 PASS.

- [ ] **Step 2: 씬 재생성 (HelloScene 배선 반영)**

```bash
/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . -logFile /tmp/earth_setup.log \
  -executeMethod PocBuildPipeline.SetupHelloScene
grep -E "TilemapRenderer wired|PlayerAvatar wired|tilemap SerializedProperty" /tmp/earth_setup.log
```
Expected: `TilemapRenderer wired: tileset=4/4`, `PlayerAvatar wired: idle=4/4...`, `tilemap` wiring 경고 없음.

- [ ] **Step 3: HelloScene 직렬화 확인 (MapView 흔적 없음, TilemapRenderer 존재)**

```bash
git show :Assets/Scenes/HelloScene.unity 2>/dev/null | true
grep -oE 'guid: [a-f0-9]+' Assets/Scenes/HelloScene.unity | awk '{print $2}' | sort -u | while read g; do
  f=$(grep -rl "guid: $g" Assets/Scripts/*.cs.meta Assets/Scripts/Earth/*.cs.meta 2>/dev/null)
  [ -n "$f" ] && basename "$f" .cs.meta
done
```
Expected: `TilemapRenderer`, `GpsCheck`, `DiscoveryDetection`, `PlayerAvatar` 매칭. `MapView` 없음.

- [ ] **Step 4: iOS 빌드 무결성 (테스트 asmdef가 빌드 안 막는지)**

```bash
/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath . -logFile /tmp/earth_build.log \
  -executeMethod PocBuildPipeline.DoAll
tail -30 /tmp/earth_build.log
```
Expected: Xcode 프로젝트 `build/ios/` 생성, 컴파일 에러 없음.
- ⚠️ 만약 테스트 asmdef로 빌드가 깨지면: `Assets/Tests/` 삭제 후 재빌드(순수함수는 통과 기록으로 충분). 빌드 안정성 우선.

- [ ] **Step 5: Editor 수동 시각 검증 (실제 OSM 좌표)**

Unity Editor에서 HelloScene 열고, `TilemapRenderer.initialLat/Lon`을 OSM 데이터 풍부한 좌표로 임시 변경(예: 한강 인접 `37.5160, 126.9966` 또는 큰 공원). Play → 흙길/물/숲 타일이 실제 지형 형태로 보이는지, 아바타가 중앙 고정되는지, (Editor라 GPS 없으면 initial 고정) 확인. Overpass 응답 로그 확인.
> 실기기 GPS 흐름은 별도 후속(이슈 #52 범위에서 v1 수용기준은 "Editor에서 실 좌표 타일 렌더 + 빌드 성공").

- [ ] **Step 6: 산출물 커밋**

```bash
git add Assets/Scenes/HelloScene.unity Assets/Scenes/HelloScene.unity.meta ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(earth): HelloScene 타일맵 산출물 재생성 (#52)"
```

- [ ] **Step 7: PR 생성**

```bash
git push -u origin feat/52-earth-tilemap-pikmin
gh pr create --base main --title "feat(earth): 지구 산책 레이어 Pikmin 실지형 픽셀 타일맵 v1 (#52)" \
  --body "Closes #52 (v1). 설계: docs/superpowers/specs/2026-05-30-earth-tilemap-pikmin-design.md

## 변경
- 신규 Earth 타일맵 시스템: GeoTileGrid·OverpassClient·OverpassParser·FeatureRasterizer·ChunkCache·AutoTile·TilemapRenderer
- PixelLab 베이스 타일셋(grass/path/water/forest)
- GpsCheck 재배선, 구 MapView/TileCache 삭제
- SetupHelloScene 배선 교체 + 씬 재생성

## 검증
- EditMode 단위테스트 (GeoTileGrid/Parser/Rasterizer/AutoTile) PASS
- batchmode 셋업 로그: TilemapRenderer wired 4/4, tilemap wiring 경고 없음
- DoAll iOS 빌드 성공

## 범위 밖
- 건물(v2), 발견 생물 맵표시(v3), 나의 행성 발전 시스템"
```

---

## Self-Review (작성자 체크)

**1. Spec coverage:**
- §3.1 컴포넌트 전부 태스크화: GeoTileGrid(T1)·OverpassClient(T6)·FeatureRasterizer(T4)·ChunkCache(T7)·TilemapRenderer(T8)·TileType(T1) ✔. OverpassParser는 스펙 §3.1 OverpassClient 책임을 파서로 분리(T3) — 순수함수 TDD 위함, 정당.
- §4 데이터흐름: GPS→SetCenter→Refresh→ChunkCache.TryGet→Overpass→Rasterize→render ✔ (T7,T8).
- §4.1 오토타일: AutoTile(T5). v1은 인덱스 계산만, 렌더적용 v3 — 스펙 §8 v3와 일치 ✔.
- §5 에러처리: Overpass 백오프(T6), Grass placeholder(T7,T8), GPS 폴백=initialLat/Lon(T8) ✔.
- §6 테스트: 4개 순수함수 EditMode + asmdef Editor전용 + 빌드안정성 가드(T1,T11) ✔.
- §7 씬통합: GpsCheck 재배선(T9), SetupHelloScene(T10), MapView/TileCache 삭제(T9), GeoCoord 보존(미삭제) ✔.

**2. Placeholder scan:** "적절한 에러처리" 류 없음. 모든 코드 스텝에 실제 코드. MiniJSON은 외부 출처 명시(T3 S5) — 구현자가 표준 소스 첨부. ✔

**3. Type consistency:** `SetCenter(double,double)` (TilemapRenderer/GpsCheck 일치), `TryGet(long,long)→ChunkData?`, `Rasterize(long,long,List<OsmFeature>)→ChunkData`, `NeighborMask(TileType[,],int,int)→int`, `Parse(string)→List<OsmFeature>`, 필드명 `tilemap`(GpsCheck)·`grassTex/pathTex/waterTex/forestTex`(TilemapRenderer)·SetupHelloScene 주입 일치 ✔.

**알려진 트레이드오프(구현자 인지):**
- T8의 `Mercator()` 상수는 GeoTileGrid.OriginShift와 동일값 중복 — 필요시 GeoTileGrid에 public 노출로 DRY화 가능(선택).
- v1 베이스 타일만 렌더(오토타일 시각효과 미적용) — Pikmin 가장자리 전이는 v3. 첫 빌드는 "블록형 픽셀맵"으로 보임이 정상.
