// PlanetGeneratorTests.cs
// TERRA × BIOSPHERE PoC — PlanetGenerator 단위 테스트.
//
// 검증 포인트:
//   1) 1만 좌표 샘플 → 3타입 분포 30~37% (균등성)
//   2) Deterministic — 같은 seed → 같은 displayName / type / hueShift
//   3) 블랙리스트 회전 — (잔잔한, 용암 들녘) → 다른 명사로 회전
//   4) hueShift 범위 -15 ~ +15
//   5) speciesPool 4개 + bonusSpeciesId 1개

using System.Collections.Generic;
using NUnit.Framework;

public class PlanetGeneratorTests
{
    [Test]
    public void Generate_Deterministic_같은seed_같은결과()
    {
        ulong seed = 0xDEADBEEFCAFEBABEUL;
        var a = PlanetGenerator.Generate(seed);
        var b = PlanetGenerator.Generate(seed);
        Assert.AreEqual(a.type, b.type);
        Assert.AreEqual(a.displayName, b.displayName);
        Assert.AreEqual(a.hueShift, b.hueShift);
        Assert.AreEqual(a.bonusSpeciesId, b.bonusSpeciesId);
    }

    [Test]
    public void Generate_HueShift_15도이내()
    {
        // 256개 hue byte 모두 검사. (seed >> 8) & 0xFF.
        for (int b = 0; b < 256; b++)
        {
            ulong seed = (ulong)b << 8;
            var p = PlanetGenerator.Generate(seed);
            Assert.That(p.hueShift, Is.GreaterThanOrEqualTo(-15f).And.LessThanOrEqualTo(15f),
                "hue byte " + b + " produced " + p.hueShift);
        }
    }

    [Test]
    public void Generate_SpeciesPool_4개_BonusSpecies_포함()
    {
        for (int t = 0; t < 3; t++)
        {
            ulong seed = (ulong)t;
            var p = PlanetGenerator.Generate(seed);
            Assert.AreEqual(4, p.speciesPool.Length, "type " + p.type);
            Assert.IsNotEmpty(p.bonusSpeciesId);
            CollectionAssert.Contains(p.speciesPool, p.bonusSpeciesId,
                "bonus 종이 풀에 포함되어야 함 (type " + p.type + ")");
        }
    }

    [Test]
    public void Generate_타입분포_1만샘플_각30_37퍼센트()
    {
        // 실제 도시권 좌표 분포를 흉내내기 위해 한국 위경도 범위(33~38, 125~130) grid sweep.
        int[] counts = new int[3];
        int total = 0;
        for (int i = 0; i < 100; i++)
        {
            for (int j = 0; j < 100; j++)
            {
                double lat = 33.0 + i * 0.05; // 33 ~ 38
                double lon = 125.0 + j * 0.05; // 125 ~ 130
                ulong seed = PlanetSeed.Compute(lat, lon);
                var p = PlanetGenerator.Generate(seed);
                counts[(int)p.type]++;
                total++;
            }
        }
        Assert.AreEqual(10000, total);

        for (int t = 0; t < 3; t++)
        {
            double pct = counts[t] * 100.0 / total;
            Assert.That(pct, Is.GreaterThanOrEqualTo(28.0).And.LessThanOrEqualTo(38.5),
                "type " + (PlanetType)t + " 분포: " + pct.ToString("F2") + "% (counts=" +
                counts[0] + "/" + counts[1] + "/" + counts[2] + ")");
        }
    }

    [Test]
    public void Generate_DisplayName_형용사_명사조합()
    {
        // 표시 이름이 "{형용사} {명사}" 형식이어야 함.
        ulong seed = PlanetSeed.Compute(37.5663, 126.9779);
        var p = PlanetGenerator.Generate(seed);
        Assert.IsNotEmpty(p.displayName);
        var parts = p.displayName.Split(' ');
        Assert.GreaterOrEqual(parts.Length, 2, "displayName 은 형용사+명사 형식: " + p.displayName);

        // 형용사가 풀에 있는지.
        var adjs = new HashSet<string>(PlanetData.Adjectives);
        Assert.IsTrue(adjs.Contains(parts[0]), "형용사 " + parts[0] + " 가 풀에 있어야 함");
    }

    [Test]
    public void Generate_블랙리스트_회전적용_volcano()
    {
        // 강제로 (adj=5 잔잔한, noun=1 용암 들녘, type=Volcano) seed 를 만든다.
        // 비트 구성: type=0 (volcano), adj=5, noun=1, hue=0.
        //   bits [0..7]   = type byte → % 3 → 0 = volcano
        //   bits [8..15]  = hue byte
        //   bits [16..23] = adj byte → % 24 → 5
        //   bits [24..31] = noun byte → % 8 → 1
        ulong seed = (0UL) | (0UL << 8) | ((ulong)5 << 16) | ((ulong)1 << 24);
        var p = PlanetGenerator.Generate(seed);
        Assert.AreEqual(PlanetType.Volcano, p.type);
        // (잔잔한, 용암 들녘) 은 블랙리스트 → noun_idx 가 (1+1) % 8 = 2 (잿빛 언덕) 로 회전.
        Assert.AreEqual("잔잔한 잿빛 언덕", p.displayName,
            "블랙리스트 회전 미적용: " + p.displayName);
    }

    [Test]
    public void Generate_블랙리스트_회전적용_ice()
    {
        // ice: (메마른=2, 얼음 늪=1) 블랙리스트.
        // type byte 가 % 3 → 1 (ice) 이어야. 1 사용.
        ulong seed = (1UL) | (0UL << 8) | ((ulong)2 << 16) | ((ulong)1 << 24);
        var p = PlanetGenerator.Generate(seed);
        Assert.AreEqual(PlanetType.Ice, p.type);
        // (메마른, 얼음 늪) → (메마른, 회청 들녘) 으로 회전.
        Assert.AreEqual("메마른 회청 들녘", p.displayName,
            "ice 블랙리스트 회전 미적용: " + p.displayName);
    }

    [Test]
    public void Generate_블랙리스트_회전적용_desert()
    {
        // desert: (얼어붙은=3, 햇빛 언덕=6) 블랙리스트.
        ulong seed = (2UL) | (0UL << 8) | ((ulong)3 << 16) | ((ulong)6 << 24);
        var p = PlanetGenerator.Generate(seed);
        Assert.AreEqual(PlanetType.Desert, p.type);
        // (얼어붙은, 햇빛 언덕) → (얼어붙은, 메마른 협곡) 으로 회전 ((6+1)%8 = 7).
        Assert.AreEqual("얼어붙은 메마른 협곡", p.displayName,
            "desert 블랙리스트 회전 미적용: " + p.displayName);
    }

    [Test]
    public void Generate_화이트리스트조합_정상생성_volcano()
    {
        // v2 §5.4 — "잠든 화산" (adj=0, noun=0) 은 정상 통과.
        ulong seed = (0UL) | (0UL << 8) | ((ulong)0 << 16) | ((ulong)0 << 24);
        var p = PlanetGenerator.Generate(seed);
        Assert.AreEqual("잠든 화산", p.displayName);
    }

    [Test]
    public void Generate_Lore_빈문자열아님()
    {
        for (int t = 0; t < 3; t++)
        {
            ulong seed = (ulong)t;
            var p = PlanetGenerator.Generate(seed);
            Assert.IsNotEmpty(p.lore, "type " + p.type + " lore 가 비어 있음");
            Assert.IsNotEmpty(p.introScenario, "type " + p.type + " introScenario 가 비어 있음");
        }
    }

    [Test]
    public void Generate_블랙리스트_무한루프없음_모든seed_종료()
    {
        // 안전성: 임의 seed 1000개 모두 정상 종료.
        for (int i = 0; i < 1000; i++)
        {
            ulong seed = unchecked((ulong)i * 0x9E3779B97F4A7C15UL);
            var p = PlanetGenerator.Generate(seed);
            Assert.IsNotEmpty(p.displayName);
        }
    }
}
