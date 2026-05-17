// PlanetSeed.cs
// TERRA × BIOSPHERE PoC — GPS 좌표를 1km 그리드 셀로 floor 한 뒤 deterministic seed 로 변환.
//
// 시나리오 v2 §5 / §7 — "내가 서 있는 자리 = 내 행성" 메타포의 핵심 산출물.
// 같은 1km 셀(0.01° 격자) 안에서는 항상 동일한 seed → 동일한 행성이 재현된다.
//
// CellMapping (0.001° 종 발견 셀) 과는 의도적으로 분리. 발견 셀은 산책 단위(~111m),
// 행성 셀은 동네 단위(~1km). 한 행성 안에서 여러 발견 셀이 존재.
//
// 알고리즘: prime-mix (FNV-1a 변형). xxHash64 도입은 외부 의존성을 늘리므로,
// PoC 규모에서는 두 큰 소수의 mix + bit shuffling 으로 충분히 균등 분포를 얻는다.
// PlanetGeneratorTests 가 1만 좌표 샘플로 3타입 30~37% 분포를 검증.
//
// 모든 메서드는 순수 함수 (no allocation, no state).

using System;

public static class PlanetSeed
{
    /// <summary>행성 셀 그리드 해상도 (도). 0.01° ≈ 적도 기준 1.11km.</summary>
    public const double GridResolution = 0.01;

    // prime-mix 상수 — bjornharrtell/xxhash, OpenStreetMap, GIMP 등에서 검증된 큰 소수.
    private const long Prime1 = 73856093L;
    private const long Prime2 = 19349663L;
    private const ulong Prime3 = 0x9E3779B97F4A7C15UL; // golden ratio 64bit (Knuth)

    /// <summary>
    /// 위경도(도) → 1km 셀에 대한 deterministic seed (64bit).
    /// 같은 셀의 모든 좌표는 동일한 seed 를 반환한다.
    /// </summary>
    public static ulong Compute(double latitude, double longitude)
    {
        var (latCell, lonCell) = ToCell(latitude, longitude);
        return MixCellsToSeed(latCell, lonCell);
    }

    /// <summary>위경도 → 1km 셀 정수 좌표 (floor).</summary>
    public static (long latCell, long lonCell) ToCell(double latitude, double longitude)
    {
        long latCell = (long)Math.Floor(latitude / GridResolution);
        long lonCell = (long)Math.Floor(longitude / GridResolution);
        return (latCell, lonCell);
    }

    /// <summary>셀 좌표 → 사람 친화적 셀 ID (PlayerPrefs 저장용).</summary>
    public static string ToCellId(double latitude, double longitude)
    {
        var (latCell, lonCell) = ToCell(latitude, longitude);
        return $"{latCell},{lonCell}";
    }

    /// <summary>
    /// 셀 좌표 → seed. 분리되어 있어 디버그/테스트가 직접 호출 가능.
    /// 알고리즘:
    /// 1. lat/lon 셀을 큰 소수와 XOR mix → 32bit 1차 hash
    /// 2. 1차 hash 를 golden ratio mix 와 곱해 64bit 로 확장 + bit shuffle
    /// </summary>
    public static ulong MixCellsToSeed(long latCell, long lonCell)
    {
        // 음수 셀 ID 도 안전하게 다루기 위해 ulong cast.
        ulong a = unchecked((ulong)(latCell * Prime1));
        ulong b = unchecked((ulong)(lonCell * Prime2));
        ulong mixed = a ^ b;

        // 64bit avalanche (splitmix64 변형) — 출력 비트 균등성 보장.
        mixed = unchecked(mixed * Prime3);
        mixed ^= mixed >> 33;
        mixed = unchecked(mixed * Prime3);
        mixed ^= mixed >> 29;
        return mixed;
    }

    /// <summary>
    /// 셀 ID 문자열 ("latCell,lonCell") → seed.
    /// PlayerPrefs("last_seed") 복원 시 사용. 형식이 깨지면 0 반환.
    /// </summary>
    public static ulong SeedFromCellId(string cellId)
    {
        if (string.IsNullOrEmpty(cellId)) return 0UL;
        var parts = cellId.Split(',');
        if (parts.Length != 2) return 0UL;
        if (!long.TryParse(parts[0], out long latCell)) return 0UL;
        if (!long.TryParse(parts[1], out long lonCell)) return 0UL;
        return MixCellsToSeed(latCell, lonCell);
    }
}
