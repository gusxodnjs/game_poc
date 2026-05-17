// PlanetData.cs
// TERRA × BIOSPHERE PoC — 시나리오 v2 §2 / §3 / §5 전문 transcribe.
//
// 결정 메모: JSON 파일이 아닌 static class 로 보관.
// 이유:
//   (1) IMGUI-only 프로젝트 톤 일치 — Resources.Load / JsonUtility 도입 회피
//   (2) 빌드/테스트 시 런타임 파일 I/O 없음 → deterministic
//   (3) PlanetGeneratorTests 가 직접 import 해 분포/블랙리스트 검증 용이
//
// 변경 SOP: 시나리오 v2 가 바뀌면 본 파일도 동일 commit 으로 갱신.

using System.Collections.Generic;
using UnityEngine;

public static class PlanetData
{
    // ─────────────────────────────────────────────────────────
    // 자동 이름 풀 — 시나리오 v2 §5
    // ─────────────────────────────────────────────────────────

    /// <summary>형용사 24개 (v2 §5.1). 인덱스 = 풀 순서, 변경 금지.</summary>
    public static readonly string[] Adjectives = new string[]
    {
        "잠든",         // 0
        "고요한",       // 1
        "메마른",       // 2
        "얼어붙은",     // 3
        "식어버린",     // 4
        "잔잔한",       // 5
        "흩어진",       // 6
        "텅 빈",        // 7
        "잠잠한",       // 8
        "굳어진",       // 9
        "가라앉은",     // 10
        "멈춰선",       // 11
        "낡은",         // 12
        "바랜",         // 13
        "희미한",       // 14
        "가물거리는",   // 15
        "옅은",         // 16
        "무거운",       // 17
        "느린",         // 18
        "깊은",         // 19
        "외딴",         // 20
        "한적한",       // 21
        "비어버린",     // 22
        "오래된",       // 23
    };

    /// <summary>volcano 명사 풀 8개 (v2 §5.2).</summary>
    public static readonly string[] NounsVolcano = new string[]
    {
        "화산",             // 0
        "용암 들녘",        // 1
        "잿빛 언덕",        // 2
        "식은 봉우리",      // 3
        "검은 바위 자락",   // 4
        "재의 평원",        // 5
        "분화구",           // 6
        "굳은 협곡",        // 7
    };

    /// <summary>ice 명사 풀 8개.</summary>
    public static readonly string[] NounsIce = new string[]
    {
        "평원",         // 0
        "얼음 늪",      // 1
        "회청 들녘",    // 2
        "빙하 자락",    // 3
        "서리 언덕",    // 4
        "얼어붙은 강",  // 5
        "새벽 벌판",    // 6
        "흰 골짜기",    // 7
    };

    /// <summary>desert 명사 풀 8개.</summary>
    public static readonly string[] NounsDesert = new string[]
    {
        "들녘",         // 0
        "모래 언덕",    // 1
        "황토 평원",    // 2
        "마른 바람골",  // 3
        "갈라진 땅",    // 4
        "빈 사구",      // 5
        "햇빛 언덕",    // 6
        "메마른 협곡",  // 7
    };

    /// <summary>type 별 명사 풀 헬퍼.</summary>
    public static string[] NounsFor(PlanetType type)
    {
        switch (type)
        {
            case PlanetType.Volcano: return NounsVolcano;
            case PlanetType.Ice: return NounsIce;
            case PlanetType.Desert: return NounsDesert;
        }
        return NounsVolcano;
    }

    // ─────────────────────────────────────────────────────────
    // 부적합 조합 블랙리스트 — 시나리오 v2 §5.3 (총 19개)
    // (형용사 index, 명사 index) tuple. 매칭 시 noun_idx 를 (+1) % len 으로 회전.
    // ─────────────────────────────────────────────────────────

    /// <summary>volcano 블랙리스트 (7개).</summary>
    public static readonly (int adj, int noun)[] BlacklistVolcano = new (int, int)[]
    {
        (5, 1),  // (잔잔한, 용암 들녘)
        (3, 0),  // (얼어붙은, 화산)
        (3, 6),  // (얼어붙은, 분화구)
        (3, 1),  // (얼어붙은, 용암 들녘)
        (3, 3),  // (얼어붙은, 식은 봉우리)
        (4, 3),  // (식어버린, 식은 봉우리)
        (2, 1),  // (메마른, 용암 들녘)
    };

    /// <summary>ice 블랙리스트 (6개).</summary>
    public static readonly (int adj, int noun)[] BlacklistIce = new (int, int)[]
    {
        (2, 1),  // (메마른, 얼음 늪)
        (2, 5),  // (메마른, 얼어붙은 강)
        (4, 3),  // (식어버린, 빙하 자락)
        (4, 1),  // (식어버린, 얼음 늪)
        (4, 5),  // (식어버린, 얼어붙은 강)
        (0, 5),  // (잠든, 얼어붙은 강) — 형용사 중복
    };

    /// <summary>desert 블랙리스트 (6개).</summary>
    public static readonly (int adj, int noun)[] BlacklistDesert = new (int, int)[]
    {
        (3, 6),  // (얼어붙은, 햇빛 언덕)
        (3, 3),  // (얼어붙은, 마른 바람골)
        (3, 1),  // (얼어붙은, 모래 언덕)
        (3, 5),  // (얼어붙은, 빈 사구)
        (5, 4),  // (잔잔한, 갈라진 땅)
        (4, 6),  // (식어버린, 햇빛 언덕)
    };

    public static (int adj, int noun)[] BlacklistFor(PlanetType type)
    {
        switch (type)
        {
            case PlanetType.Volcano: return BlacklistVolcano;
            case PlanetType.Ice: return BlacklistIce;
            case PlanetType.Desert: return BlacklistDesert;
        }
        return BlacklistVolcano;
    }

    /// <summary>(adj, noun) 가 해당 type 블랙리스트에 있는지.</summary>
    public static bool IsBlacklisted(PlanetType type, int adjIdx, int nounIdx)
    {
        var bl = BlacklistFor(type);
        for (int i = 0; i < bl.Length; i++)
        {
            if (bl[i].adj == adjIdx && bl[i].noun == nounIdx) return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────
    // base lore / 색조 / 종 풀 — 시나리오 v2 §2
    // ─────────────────────────────────────────────────────────

    public struct BaseDef
    {
        public string baseName;        // 폴백/디버그용
        public string lore;            // 2문장
        public Color baseTint;         // shader hue/tint base
        public string[] speciesPool;   // 4종
        public string bonusSpeciesId;  // 1종
        public string introScenario;   // §3 진입 한 컷
        public string cardAssetPath;   // Sprite asset path (Assets/...)
    }

    public static BaseDef GetBaseDef(PlanetType type)
    {
        switch (type)
        {
            case PlanetType.Volcano:
                return new BaseDef
                {
                    baseName = "잠든 화산",
                    lore = "검은 용암이 식어 굳고, 갈라진 틈 사이로 흰 김이 가늘게 오른다.\n바위는 아직 따뜻하고, 무언가가 자랄 자리는 비어 있다.",
                    baseTint = HexToColor("#5C3B33"),
                    speciesPool = new[] { "dandelion", "foxtail_grass", "white_clover", "ladybug" },
                    bonusSpeciesId = "white_clover",
                    introScenario = "검은 바위가 신발 끝에 닿습니다.\n틈 사이로 흰 김이 한 번 오르고, 다시 가라앉습니다.\n첫 걸음을, 천천히 떼어 보세요.",
                    cardAssetPath = "Assets/world/planet_card_volcano_256.png",
                };

            case PlanetType.Ice:
                return new BaseDef
                {
                    baseName = "얼어붙은 평원",
                    lore = "회청색 얼음이 지평선까지 깔리고, 바람이 한 번 길게 분다.\n깊은 정적 아래, 무언가가 풀리기를 기다리고 있다.",
                    baseTint = HexToColor("#6E8AA0"),
                    speciesPool = new[] { "dandelion", "foxtail_grass", "honeybee", "ladybug" },
                    bonusSpeciesId = "honeybee",
                    introScenario = "얼음이 신발 아래에서 조용히 울립니다.\n바람이 한 번 길게 지나가고, 평원은 다시 잠잠해집니다.\n숨을 한 번 고르고, 한 걸음 내디뎌 보세요.",
                    cardAssetPath = "Assets/world/planet_card_ice_256.png",
                };

            case PlanetType.Desert:
                return new BaseDef
                {
                    baseName = "메마른 들녘",
                    lore = "황토색 모래가 끝없이 펼쳐지고, 갈라진 땅 위로 햇빛만 내려앉는다.\n바람 한 결이 어디선가, 작은 무언가를 실어 온다.",
                    baseTint = HexToColor("#B89968"),
                    speciesPool = new[] { "dandelion", "foxtail_grass", "cherry_blossom", "ladybug" },
                    bonusSpeciesId = "cherry_blossom",
                    introScenario = "마른 흙이 발끝에 부서집니다.\n바람 한 결이 어딘가에서, 작은 무언가를 실어 옵니다.\n그 결을 따라, 한 걸음 떼어 보세요.",
                    cardAssetPath = "Assets/world/planet_card_desert_256.png",
                };
        }
        return GetBaseDef(PlanetType.Volcano);
    }

    /// <summary>DiscoveryDetection fallback — GameSession 미초기화 시.</summary>
    public static readonly string[] DefaultSpeciesPool = new[]
    {
        "민들레", "강아지풀", "흰토끼풀", "벚꽃", "무당벌레", "꿀벌",
    };

    // ─────────────────────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────────────────────

    private static Color HexToColor(string hex)
    {
        if (hex.StartsWith("#")) hex = hex.Substring(1);
        if (hex.Length != 6) return Color.white;
        byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
        byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
        byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
