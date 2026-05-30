using NUnit.Framework;
using System.Collections.Generic;

public class FeatureRasterizerTests
{
    private (long cx, long cy) SeoulChunk()
    {
        var (tx, ty) = GeoTileGrid.LatLonToTile(37.5663, 126.9779);
        return GeoTileGrid.TileToChunk(tx, ty);
    }

    [Test] public void EmptyFeatures_AllGrass()
    {
        var (cx, cy) = SeoulChunk();
        var cd = FeatureRasterizer.Rasterize(cx, cy, new List<OsmFeature>());
        Assert.AreEqual(TileType.Grass, cd.Tiles[0, 0]);
        Assert.AreEqual(TileType.Grass, cd.Tiles[31, 31]);
    }

    [Test] public void WaterPolygon_CoveringChunk_FillsWater()
    {
        var (cx, cy) = SeoulChunk();
        var (clat, clon) = ChunkCenterLatLon(cx, cy);
        double d = 0.01; // ~1km >> 청크(96m)
        var poly = new OsmFeature { Type = TileType.Water, Geom = OsmGeom.Polygon };
        poly.Points.Add((clat - d, clon - d));
        poly.Points.Add((clat - d, clon + d));
        poly.Points.Add((clat + d, clon + d));
        poly.Points.Add((clat + d, clon - d));
        poly.Points.Add((clat - d, clon - d));
        var cd = FeatureRasterizer.Rasterize(cx, cy, new List<OsmFeature> { poly });
        Assert.AreEqual(TileType.Water, cd.Tiles[16, 16]);
    }

    [Test] public void Water_OverridesForest_Priority()
    {
        var (cx, cy) = SeoulChunk();
        var (clat, clon) = ChunkCenterLatLon(cx, cy);
        double d = 0.01;
        List<(double, double)> Square() => new List<(double, double)>{
            (clat-d,clon-d),(clat-d,clon+d),(clat+d,clon+d),(clat+d,clon-d),(clat-d,clon-d)};
        var forest = new OsmFeature { Type = TileType.Forest, Geom = OsmGeom.Polygon, Points = Square() };
        var water  = new OsmFeature { Type = TileType.Water,  Geom = OsmGeom.Polygon, Points = Square() };
        var cd = FeatureRasterizer.Rasterize(cx, cy, new List<OsmFeature> { forest, water });
        Assert.AreEqual(TileType.Water, cd.Tiles[16, 16]);
    }

    private (double lat, double lon) ChunkCenterLatLon(long cx, long cy)
    {
        var (tx, ty) = GeoTileGrid.ChunkOriginTile(cx, cy);
        return GeoTileGrid.TileCenterLatLon(tx + 16, ty + 16);
    }
}
