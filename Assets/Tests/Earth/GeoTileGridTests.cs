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
        var a = GeoTileGrid.LatLonToTile(37.5663, 126.9779);
        var b = GeoTileGrid.LatLonToTile(37.56631, 126.9779); // ~1.1m < 3m
        Assert.AreEqual(a, b);
    }

    [Test]
    public void FarPoints_DifferentTile()
    {
        var a = GeoTileGrid.LatLonToTile(37.5663, 126.9779);
        var b = GeoTileGrid.LatLonToTile(37.5673, 126.9779); // ~111m
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
