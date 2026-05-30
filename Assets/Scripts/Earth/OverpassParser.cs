// OverpassParser.cs — Overpass "out geom;" JSON → List<OsmFeature>. 순수 함수.
// 분류 규칙(태그 → TileType):
//   highway=*                                    → Path  (폴리라인, buffer 1)
//   waterway=river|stream                        → Water (폴리라인, buffer 1)
//   natural=water / water=* / waterway=riverbank → Water (폴리곤)
//   landuse=forest / natural=wood / leisure=park → Forest(폴리곤)
//   그 외 무시.
// Unity JsonUtility는 dictionary(tags) 미지원 → 내장 MiniJSON(Json.cs) 사용.
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
                if (p is Dictionary<string, object> pt
                    && pt.TryGetValue("lat", out var la) && pt.TryGetValue("lon", out var lo))
                    feat.Points.Add((ToD(la), ToD(lo)));
            if (feat.Points.Count == 0) continue;

            // 분류 규칙이 곧 기하 종류를 결정한다. 폴리곤 종류(natural=water 등)는
            // Overpass가 닫는 점을 생략해도 채움(fill) 대상이므로 항상 Polygon으로 둔다.
            // 폴리라인(highway/강)은 단 한 점만 있어도 Polyline.
            feat.Geom = defaultGeom;
            result.Add(feat);
        }
        return result;
    }

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

    private static bool Classify(Dictionary<string, object> tags, out TileType type, out OsmGeom geom, out int buffer)
    {
        type = TileType.Grass; geom = OsmGeom.Polyline; buffer = 0;
        if (tags.TryGetValue("highway", out var hwObj) && hwObj is string hw)
        {
            if (RoadHighways.Contains(hw)) { type = TileType.Road; geom = OsmGeom.Polyline; buffer = BigRoads.Contains(hw) ? 1 : 0; return true; }
            type = TileType.Path; geom = OsmGeom.Polyline; buffer = 0; return true;
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

    private static bool Has(Dictionary<string, object> t, string k, string v)
        => t.TryGetValue(k, out var got) && got is string s && s == v;

    private static double ToD(object o)
        => o is double d ? d : (o is long l ? l : double.Parse(o.ToString(),
            System.Globalization.CultureInfo.InvariantCulture));
}
