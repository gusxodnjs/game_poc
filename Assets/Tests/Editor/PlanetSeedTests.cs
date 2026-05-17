// PlanetSeedTests.cs
// TERRA × BIOSPHERE PoC — PlanetSeed 단위 테스트.
//
// 검증 포인트:
//   1) Deterministic — 같은 좌표 → 같은 seed (run 간, 호출 간)
//   2) 1km 셀 (0.01°) 안의 모든 좌표 → 동일 seed
//   3) 셀 경계 너머 → 다른 seed
//   4) ToCellId ↔ SeedFromCellId round-trip
//   5) 극단값 (음수 위경도, 적도/그리니치) 안전

using NUnit.Framework;

public class PlanetSeedTests
{
    private const double SeoulCityHallLat = 37.5663;
    private const double SeoulCityHallLon = 126.9779;

    [Test]
    public void Compute_Deterministic_같은좌표_같은seed()
    {
        ulong a = PlanetSeed.Compute(SeoulCityHallLat, SeoulCityHallLon);
        ulong b = PlanetSeed.Compute(SeoulCityHallLat, SeoulCityHallLon);
        Assert.AreEqual(a, b);
    }

    [Test]
    public void Compute_같은1km셀_같은seed()
    {
        // 0.01° 셀 안의 두 점은 같은 seed 여야 함.
        // 서울시청 37.5663, 126.9779 → 셀 (3756, 12697). 그 안의 다른 좌표.
        ulong a = PlanetSeed.Compute(37.5663, 126.9779);
        ulong b = PlanetSeed.Compute(37.5699, 126.9701); // 같은 셀 (3756, 12697)
        Assert.AreEqual(a, b);
    }

    [Test]
    public void Compute_다른1km셀_다른seed()
    {
        ulong a = PlanetSeed.Compute(37.5663, 126.9779); // 셀 (3756, 12697)
        ulong b = PlanetSeed.Compute(37.5763, 126.9779); // 셀 (3757, 12697) — 한 칸 위
        Assert.AreNotEqual(a, b);
    }

    [Test]
    public void ToCell_서울시청_예상셀좌표()
    {
        var (latCell, lonCell) = PlanetSeed.ToCell(SeoulCityHallLat, SeoulCityHallLon);
        // 37.5663 / 0.01 = 3756.63 → floor = 3756
        // 126.9779 / 0.01 = 12697.79 → floor = 12697
        Assert.AreEqual(3756L, latCell);
        Assert.AreEqual(12697L, lonCell);
    }

    [Test]
    public void ToCellId_세미콜론형식()
    {
        string id = PlanetSeed.ToCellId(SeoulCityHallLat, SeoulCityHallLon);
        Assert.AreEqual("3756,12697", id);
    }

    [Test]
    public void SeedFromCellId_RoundTrip()
    {
        ulong direct = PlanetSeed.Compute(SeoulCityHallLat, SeoulCityHallLon);
        string id = PlanetSeed.ToCellId(SeoulCityHallLat, SeoulCityHallLon);
        ulong fromId = PlanetSeed.SeedFromCellId(id);
        Assert.AreEqual(direct, fromId);
    }

    [Test]
    public void SeedFromCellId_빈문자열_0반환()
    {
        Assert.AreEqual(0UL, PlanetSeed.SeedFromCellId(""));
        Assert.AreEqual(0UL, PlanetSeed.SeedFromCellId(null));
        Assert.AreEqual(0UL, PlanetSeed.SeedFromCellId("bad"));
        Assert.AreEqual(0UL, PlanetSeed.SeedFromCellId("3756"));
    }

    [Test]
    public void Compute_음수좌표_안전()
    {
        // 남반구 / 서반구 좌표도 seed 가 정상 생성되어야 함.
        ulong a = PlanetSeed.Compute(-37.5663, -126.9779);
        ulong b = PlanetSeed.Compute(-37.5663, -126.9779);
        Assert.AreEqual(a, b);
        Assert.AreNotEqual(0UL, a);
    }

    [Test]
    public void Compute_적도그리니치_특수처리없음()
    {
        ulong a = PlanetSeed.Compute(0.0, 0.0);
        // floor(0/0.01)=0 → (0,0) cell. seed 가 prime-mix 결과 0 일 가능성은 사실상 0.
        // 단, 검증은 deterministic 만.
        ulong b = PlanetSeed.Compute(0.005, 0.005);
        Assert.AreEqual(a, b); // 같은 셀
    }

    [Test]
    public void GridResolution_001고정()
    {
        // CellMapping (0.001) 과 의도적으로 분리. 변경 시 시나리오 v2 §7 영향.
        Assert.AreEqual(0.01, PlanetSeed.GridResolution);
    }

    [Test]
    public void Compute_50개_서로다른좌표_seed균등성_단순검증()
    {
        // seed 가 같은 값에 몰리지 않는지 sanity check.
        // 50개 좌표 → 50개 unique seed (충돌 0~1 허용).
        var seeds = new System.Collections.Generic.HashSet<ulong>();
        for (int i = 0; i < 50; i++)
        {
            double lat = 35.0 + i * 0.5;
            double lon = 125.0 + i * 0.3;
            seeds.Add(PlanetSeed.Compute(lat, lon));
        }
        Assert.GreaterOrEqual(seeds.Count, 49, "50개 좌표가 거의 모두 unique seed 여야 함");
    }
}
