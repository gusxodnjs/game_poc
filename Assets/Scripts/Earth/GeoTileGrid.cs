// GeoTileGrid.cs
// lat/lon ↔ 전역 픽셀 타일 그리드(타일 1칸 ≈ 3m). Web Mercator world meter 기반.
// 같은 실제 위치는 항상 같은 (tx,ty) → 결정론적. 순수 함수, allocation 없음.
//
// GeoCoord(슬리피맵 zoom 기반 OSM 타일)와 달리, 여기서는 zoom-독립적인
// 고정 미터 격자(TileMeters=3)를 쓴다. Pikmin 스타일 픽셀 타일맵은 실제
// 지형을 3m 칸으로 양자화해야 하므로, 줌 레벨이 아니라 미터를 기준으로 한다.
using System;

public static class GeoTileGrid
{
    public const double TileMeters = 3.0;
    public const int ChunkTiles = 32;
    private const double EarthRadius = 6378137.0;
    private const double OriginShift = Math.PI * EarthRadius; // 20037508.34
    private const double LatLimit = 85.05112878;

    /// <summary>위경도(도) → Web Mercator world meter (mx, my). my는 북쪽이 양수.</summary>
    public static (double mx, double my) LatLonToMercatorMeters(double lat, double lon)
    {
        if (lat > LatLimit) lat = LatLimit;
        else if (lat < -LatLimit) lat = -LatLimit;
        double mx = lon * OriginShift / 180.0;
        double my = Math.Log(Math.Tan((90.0 + lat) * Math.PI / 360.0)) / (Math.PI / 180.0);
        my = my * OriginShift / 180.0;
        return (mx, my);
    }

    /// <summary>위경도(도) → 전역 타일 좌표 (tx, ty). 원점은 좌상단(북서), ty는 남쪽으로 증가.</summary>
    public static (long tx, long ty) LatLonToTile(double lat, double lon)
    {
        var (mx, my) = LatLonToMercatorMeters(lat, lon);
        long tx = (long)Math.Floor((mx + OriginShift) / TileMeters);
        long ty = (long)Math.Floor((OriginShift - my) / TileMeters);
        return (tx, ty);
    }

    /// <summary>타일 좌표 → 청크 좌표. 음수도 floor 방향으로 내림.</summary>
    public static (long cx, long cy) TileToChunk(long tx, long ty)
        => (FloorDiv(tx, ChunkTiles), FloorDiv(ty, ChunkTiles));

    /// <summary>청크 좌표 → 그 청크의 좌상단(원점) 타일 좌표.</summary>
    public static (long tx, long ty) ChunkOriginTile(long cx, long cy)
        => (cx * ChunkTiles, cy * ChunkTiles);

    /// <summary>타일 좌표 → 그 타일 중심의 위경도.</summary>
    public static (double lat, double lon) TileCenterLatLon(long tx, long ty)
    {
        double mx = (tx + 0.5) * TileMeters - OriginShift;
        double my = OriginShift - (ty + 0.5) * TileMeters;
        double lon = mx / OriginShift * 180.0;
        double lat = my / OriginShift * 180.0;
        lat = 180.0 / Math.PI * (2.0 * Math.Atan(Math.Exp(lat * Math.PI / 180.0)) - Math.PI / 2.0);
        return (lat, lon);
    }

    /// <summary>floor division (음수 피제수도 음의 무한대 방향으로 내림).</summary>
    private static long FloorDiv(long a, long b)
    {
        long q = a / b;
        if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
        return q;
    }
}
