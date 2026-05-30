// FeatureRasterizer.cs — OSM 피처 → 청크 32×32 TileType. 순수 함수.
// 우선순위: Water > Path > Forest > Grass. 낮은 우선순위가 높은 걸 못 덮음.
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
        var cd = new ChunkData(cx, cy);
        var (originTx, originTy) = GeoTileGrid.ChunkOriginTile(cx, cy);
        foreach (var f in features)
        {
            if (f.Geom == OsmGeom.Polygon) RasterizePolygon(cd, originTx, originTy, f);
            else RasterizePolyline(cd, originTx, originTy, f);
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
        var pts = new List<(double x, double y)>(f.Points.Count);
        foreach (var (lat, lon) in f.Points)
        {
            var (tx, ty) = GeoTileGrid.LatLonToTile(lat, lon);
            pts.Add((tx, ty));
        }
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
