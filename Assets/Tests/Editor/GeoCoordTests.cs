// GeoCoordTests.cs
// TERRA × BIOSPHERE PoC — GeoCoord (Web Mercator) 단위 테스트.
//
// 검증 기준값은 표준 Slippy Map 공식으로 Python 사전 계산했다.

using NUnit.Framework;

public class GeoCoordTests
{
    private const double SeoulCityHallLat = 37.5663;
    private const double SeoulCityHallLon = 126.9779;
    private const int Zoom17 = 17;

    [Test]
    public void LatLonToTile_SeoulCityHall_Z17_매칭()
    {
        // Python 사전 계산: x=111767, y=50758
        var (x, y) = GeoCoord.LatLonToTile(SeoulCityHallLat, SeoulCityHallLon, Zoom17);
        Assert.AreEqual(111767, x, "tile X");
        Assert.AreEqual(50758, y, "tile Y");
    }

    [Test]
    public void LatLonToPixel_타일경계와_일치()
    {
        // LatLonToTile(...) 의 floor 값 × TileSize 가 LatLonToPixel 의 정수 부분과 같아야 함
        var (px, py) = GeoCoord.LatLonToPixel(SeoulCityHallLat, SeoulCityHallLon, Zoom17);
        var (tx, ty) = GeoCoord.LatLonToTile(SeoulCityHallLat, SeoulCityHallLon, Zoom17);
        Assert.AreEqual(tx, (int)System.Math.Floor(px / GeoCoord.TileSize));
        Assert.AreEqual(ty, (int)System.Math.Floor(py / GeoCoord.TileSize));
    }

    [Test]
    public void TileToLatLon_LatLonToTile_RoundTrip()
    {
        // 타일 좌표 → 타일 좌상단 위경도 → 다시 타일 좌표 = 원래 타일
        int origX = 111767, origY = 50758;
        var (lat, lon) = GeoCoord.TileToLatLon(origX, origY, Zoom17);
        var (rtX, rtY) = GeoCoord.LatLonToTile(lat, lon, Zoom17);
        Assert.AreEqual(origX, rtX);
        Assert.AreEqual(origY, rtY);
    }

    [Test]
    public void LatLonToPixel_RoundTrip_타일경계()
    {
        // 타일 좌상단 위경도의 픽셀 좌표는 정확히 타일 경계 (tile * 256) 여야 함
        int tx = 111767, ty = 50758;
        var (lat, lon) = GeoCoord.TileToLatLon(tx, ty, Zoom17);
        var (px, py) = GeoCoord.LatLonToPixel(lat, lon, Zoom17);
        // 부동소수 오차 허용 ±0.5 픽셀
        Assert.That(px, Is.EqualTo((double)tx * GeoCoord.TileSize).Within(0.5));
        Assert.That(py, Is.EqualTo((double)ty * GeoCoord.TileSize).Within(0.5));
    }

    [Test]
    public void DistanceMeters_같은점은_0()
    {
        double d = GeoCoord.DistanceMeters(SeoulCityHallLat, SeoulCityHallLon, SeoulCityHallLat, SeoulCityHallLon);
        Assert.That(d, Is.EqualTo(0.0).Within(0.001));
    }

    [Test]
    public void DistanceMeters_적도1도위도_약111km()
    {
        // 적도에서 위도 1° = 약 111.19km
        double d = GeoCoord.DistanceMeters(0, 0, 1, 0);
        Assert.That(d, Is.EqualTo(111195.0).Within(50.0));
    }

    [Test]
    public void DistanceMeters_서울위도_경도0_001도_약88m()
    {
        // 서울 위도(37.57°)에서 경도 0.001° 이동 ≈ 88m
        double d = GeoCoord.DistanceMeters(
            SeoulCityHallLat, SeoulCityHallLon,
            SeoulCityHallLat, SeoulCityHallLon + 0.001);
        Assert.That(d, Is.EqualTo(88.14).Within(1.0));
    }

    [Test]
    public void DistanceMeters_시청과_광화문_약1km()
    {
        // 서울시청 (37.5663, 126.9779) ↔ 광화문 (37.5760, 126.9769) ≈ 1082m
        double d = GeoCoord.DistanceMeters(37.5663, 126.9779, 37.5760, 126.9769);
        Assert.That(d, Is.EqualTo(1082.0).Within(10.0));
    }

    [Test]
    public void LatLonToPixel_고위도_clamp()
    {
        // 위도 89° 는 ±85.0511° 로 clamp 되어 발산하지 않아야 함
        var (_, py) = GeoCoord.LatLonToPixel(89.0, 0.0, Zoom17);
        Assert.That(double.IsNaN(py), Is.False);
        Assert.That(double.IsInfinity(py), Is.False);
    }

    [Test]
    public void TileSize_256_고정()
    {
        // OSM/Slippy 표준 — 변경되면 다른 모든 계산이 깨짐
        Assert.AreEqual(256, GeoCoord.TileSize);
    }
}
