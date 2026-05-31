// OsmFeature.cs — Overpass 파서가 분류한 단일 OSM 피처.
// Rasterizer(다음 태스크)가 List<OsmFeature>를 소비한다.
using System.Collections.Generic;

public enum OsmGeom { Polyline, Polygon }

public class OsmFeature
{
    public TileType Type;        // Path / Water / Forest
    public OsmGeom Geom;
    public List<(double lat, double lon)> Points = new List<(double, double)>();
    public int BufferTiles;      // 폴리라인 버퍼 두께(타일). Path/강=1, 폴리곤=0.
}
