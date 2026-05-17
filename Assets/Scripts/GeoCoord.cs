// GeoCoord.cs
// TERRA × BIOSPHERE PoC — Web Mercator (EPSG:3857) 좌표 변환 유틸.
//
// OSM raster tile (https://tile.openstreetmap.org/{z}/{x}/{y}.png) 와 동일한
// Slippy Map tilename 규약을 따른다. 픽셀 좌표/거리 계산까지 묶어 두어
// MapView·TileCache·PlayerAvatar 등이 공통으로 참조한다.
//
// 모든 메서드는 순수 함수(상태 없음, allocation 없음) — Update 루프 내에서
// 자유롭게 호출해도 GC 압력이 발생하지 않는다.
//
// 참고:
//   - https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames
//   - https://en.wikipedia.org/wiki/Web_Mercator_projection
//
// 좌표계 메모:
//   - Web Mercator는 위도 ±85.0511° 범위 밖에서 발산하므로, 호출측은
//     해당 범위로 clamp 된 값을 넘긴다고 가정한다.
//   - Tile 좌표 (x, y) 원점은 좌상단 (북서) — Unity world 좌표계로 옮길 때는
//     Y 부호를 뒤집어야 한다 (MapView에서 처리).

using System;

public static class GeoCoord
{
    public const int TileSize = 256;
    private const double LatLimit = 85.05112878;

    /// <summary>위경도(도) → 슬리피맵 타일 좌표 (정수 부분).</summary>
    public static (int x, int y) LatLonToTile(double lat, double lon, int zoom)
    {
        var (px, py) = LatLonToPixel(lat, lon, zoom);
        return ((int)Math.Floor(px / TileSize), (int)Math.Floor(py / TileSize));
    }

    /// <summary>타일 좌표(정수) → 해당 타일의 좌상단(북서) 위경도.</summary>
    public static (double lat, double lon) TileToLatLon(int x, int y, int zoom)
    {
        double n = Math.Pow(2.0, zoom);
        double lon = x / n * 360.0 - 180.0;
        double latRad = Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * y / n)));
        double lat = latRad * 180.0 / Math.PI;
        return (lat, lon);
    }

    /// <summary>
    /// 위경도(도) → 전역 픽셀 좌표(상수 TileSize=256 단위).
    /// 정수 부분은 타일 좌표, 소수 부분은 타일 내 픽셀 오프셋.
    /// </summary>
    public static (double px, double py) LatLonToPixel(double lat, double lon, int zoom)
    {
        if (lat > LatLimit) lat = LatLimit;
        else if (lat < -LatLimit) lat = -LatLimit;

        double n = Math.Pow(2.0, zoom);
        double latRad = lat * Math.PI / 180.0;
        double px = (lon + 180.0) / 360.0 * n * TileSize;
        double py = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n * TileSize;
        return (px, py);
    }

    /// <summary>Haversine 공식으로 두 위경도 사이 대권 거리(미터).</summary>
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6371000.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadius * c;
    }
}
