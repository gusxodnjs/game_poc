// 청크 1개의 32×32 TileType. 디스크 직렬화는 칸당 1바이트.
public class ChunkData
{
    public readonly long Cx, Cy;
    public readonly TileType[,] Tiles; // [localY, localX], 0..31

    public ChunkData(long cx, long cy)
    {
        Cx = cx; Cy = cy;
        Tiles = new TileType[GeoTileGrid.ChunkTiles, GeoTileGrid.ChunkTiles]; // 기본 Grass(enum 0)
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
