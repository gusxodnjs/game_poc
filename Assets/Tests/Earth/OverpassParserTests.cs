using NUnit.Framework;
using System.Collections.Generic;

public class OverpassParserTests
{
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

    [Test] public void Parses_Highway_As_Path_Polyline()
    {
        var feats = OverpassParser.Parse(Json);
        var path = feats.Find(f => f.Type == TileType.Path);
        Assert.IsNotNull(path);
        Assert.AreEqual(OsmGeom.Polyline, path.Geom);
        Assert.AreEqual(2, path.Points.Count);
    }
    [Test] public void Parses_Water_As_Polygon()
    {
        var water = OverpassParser.Parse(Json).Find(f => f.Type == TileType.Water);
        Assert.IsNotNull(water);
        Assert.AreEqual(OsmGeom.Polygon, water.Geom);
    }
    [Test] public void Ignores_Unclassified_Tags()
    {
        Assert.AreEqual(2, OverpassParser.Parse(Json).Count); // cafe 무시
    }
    [Test] public void EmptyOrMalformed_ReturnsEmpty_NoThrow()
    {
        Assert.AreEqual(0, OverpassParser.Parse("").Count);
        Assert.AreEqual(0, OverpassParser.Parse("{}").Count);
        Assert.AreEqual(0, OverpassParser.Parse("garbage").Count);
    }
}
