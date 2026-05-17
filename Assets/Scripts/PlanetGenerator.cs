// PlanetGenerator.cs
// TERRA × BIOSPHERE PoC — seed → PlanetInstance.
//
// 시나리오 v2 §5.0 알고리즘 구현 (PoC 변형):
//   type_idx  = (seed >> 0)  % 3   → Volcano / Ice / Desert
//   hue_off   = (seed >> 8)  % 31 - 15  → -15° ~ +15°
//   adj_idx   = (seed >> 16) % 24
//   noun_idx  = (seed >> 24) % 8
//   if (adj, noun) ∈ BLACKLIST: noun = (noun + 1) % 8 한 칸 회전
//
// 비트 분리 사용 — 같은 seed 의 서로 다른 영역을 쓰므로 4개 풀이 독립적.
// PlanetSeed.MixCellsToSeed 의 splitmix64 avalanche 가 각 8bit 영역의 균등성 보장.
//
// Sprite 로드는 Editor (AssetDatabase) / 런타임 (Resources) 환경이 달라
// 의존성 없이 분리. PlanetIntroScene 가 OnGUI 진입 직전 한 번 LoadAndAssignSprite 호출.

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class PlanetGenerator
{
    private const int AdjCount = 24;
    private const int NounCount = 8;
    private const int TypeCount = 3;

    /// <summary>seed → PlanetInstance 풀필드. Sprite 는 별도 LoadAndAssignSprite 가 채움.</summary>
    public static PlanetInstance Generate(ulong seed)
    {
        // 비트 영역 분리.
        int typeIdx = (int)((seed >> 0) & 0xFFUL) % TypeCount;
        int hueByte = (int)((seed >> 8) & 0xFFUL);
        int adjIdx = (int)((seed >> 16) & 0xFFUL) % AdjCount;
        int nounIdx = (int)((seed >> 24) & 0xFFUL) % NounCount;

        // hue: 0~30 범위로 mod 한 뒤 -15 시프트 → -15 ~ +15.
        float hueShift = (hueByte % 31) - 15f;

        var type = (PlanetType)typeIdx;

        // 블랙리스트 회전 — 한 칸 회전 후 재충돌 가능성은 사실상 0 이지만,
        // 안전을 위해 최대 NounCount 회 시도.
        for (int guard = 0; guard < NounCount; guard++)
        {
            if (!PlanetData.IsBlacklisted(type, adjIdx, nounIdx)) break;
            nounIdx = (nounIdx + 1) % NounCount;
        }

        var baseDef = PlanetData.GetBaseDef(type);
        string adjective = PlanetData.Adjectives[adjIdx];
        string noun = PlanetData.NounsFor(type)[nounIdx];

        return new PlanetInstance
        {
            seed = seed,
            type = type,
            displayName = adjective + " " + noun,
            baseTint = baseDef.baseTint,
            hueShift = hueShift,
            lore = baseDef.lore,
            speciesPool = baseDef.speciesPool,
            bonusSpeciesId = baseDef.bonusSpeciesId,
            introScenario = baseDef.introScenario,
            cardSprite = null,  // LoadAndAssignSprite 에서 채움
            cellId = null,
        };
    }

    /// <summary>
    /// 카드 Sprite 를 로드해 PlanetInstance.cardSprite 에 할당.
    /// Editor 에서는 AssetDatabase, 런타임에서는 Resources.Load 사용 가능.
    /// PoC 단계에서는 Editor + Play mode 둘 다 AssetDatabase 로 충분.
    /// 빌드/런타임에서는 Resources 폴더 이동이 필요하면 후속에서 도입.
    /// </summary>
    public static void LoadAndAssignSprite(PlanetInstance planet)
    {
        if (planet == null) return;
        if (planet.cardSprite != null) return;

        var baseDef = PlanetData.GetBaseDef(planet.type);

#if UNITY_EDITOR
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(baseDef.cardAssetPath);
        if (sprite == null)
        {
            // Sprite 가 아니라 Texture2D 로 import 된 경우 fallback.
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseDef.cardAssetPath);
            if (tex != null)
            {
                sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 64f);
            }
        }
        planet.cardSprite = sprite;
        if (sprite == null)
        {
            Debug.LogWarning("[POC] PlanetGenerator: card sprite not found at " + baseDef.cardAssetPath);
        }
#else
        // 빌드 런타임 — Editor scene 셋업에서 Sprite reference 를 PlanetIntroScene 컴포넌트 직렬화로 주입해야 함.
        // PocBuildPipeline.SetupPlanetIntroScene 가 PlanetIntroScene 의 카드 Sprite 배열을 채움.
        // 여기서는 fallback 으로 텍스처 직접 load 시도 (Resources 경로가 있는 경우).
        Debug.Log("[POC] PlanetGenerator: runtime sprite load skipped; scene-injected sprite expected for " + planet.type);
#endif
    }
}
