using NUnit.Framework;

public class AutoTileTests
{
    private TileType[,] Grid()
    {
        var g = new TileType[3, 3];
        g[1, 1] = TileType.Path;
        g[0, 1] = TileType.Path; // 위(N)
        g[1, 2] = TileType.Path; // 오른(E)
        return g;
    }

    [Test] public void Bitmask_NorthEastSame_Gives_N_plus_E()
    {
        Assert.AreEqual(3, AutoTile.NeighborMask(Grid(), 1, 1)); // N=1 + E=2
    }

    [Test] public void Bitmask_Isolated_Zero()
    {
        var g = new TileType[3, 3];
        g[1, 1] = TileType.Water;
        Assert.AreEqual(0, AutoTile.NeighborMask(g, 1, 1));
    }

    [Test] public void Bitmask_OutOfBounds_TreatedAsDifferent()
    {
        var g = new TileType[1, 1];
        g[0, 0] = TileType.Path;
        Assert.AreEqual(0, AutoTile.NeighborMask(g, 0, 0));
    }
}
