// AutoTile.cs — 4-이웃 비트마스크 (N=1,E=2,S=4,W=8). 같은 타입 이웃이면 비트 set.
// 경계 밖은 "다른 타입" 취급. 순수 함수.
// 인덱싱은 [y, x] (row-major, ChunkData.Tiles[localY, localX]와 동일). N = y-1, S = y+1.
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
