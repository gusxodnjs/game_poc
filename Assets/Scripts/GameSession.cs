// GameSession.cs
// TERRA × BIOSPHERE PoC — 세션 전역 상태 (현재 행성). DontDestroyOnLoad.
//
// 책임:
//   - PlanetIntroScene 에서 결정된 PlanetInstance 를 HelloScene 까지 캐리.
//   - PlayerPrefs("last_seed_cell") 저장/복원 (시나리오 v2 §7 폴백).
//
// 의도된 미니멀리즘: GPS 폴링/씬 전환은 각 씬 스크립트 책임. 본 클래스는 데이터만.

using System.Collections.Generic;
using UnityEngine;

public class GameSession : MonoBehaviour
{
    public const string PrefsKeyLastSeedCell = "last_seed_cell";
    private const string CollectionKeyPrefix = "collection_"; // + cellId → "id:count,id:count"

    private static GameSession _instance;
    public static GameSession Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("GameSession");
                _instance = go.AddComponent<GameSession>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    /// <summary>현재 표시 중인 행성. PlanetIntroScene 에서 SetPlanet 호출 전에는 null 가능.</summary>
    public PlanetInstance CurrentPlanet { get; private set; }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>행성을 설정하고 PlayerPrefs 에 마지막 셀을 저장.</summary>
    public void SetPlanet(PlanetInstance planet)
    {
        CurrentPlanet = planet;
        if (planet != null && !string.IsNullOrEmpty(planet.cellId))
        {
            PlayerPrefs.SetString(PrefsKeyLastSeedCell, planet.cellId);
            PlayerPrefs.Save();
            Debug.Log("[POC] GameSession.SetPlanet: " + planet.displayName +
                      " (type=" + planet.type + ", seed=" + planet.seed +
                      ", cell=" + planet.cellId + ")");
        }
        else
        {
            Debug.Log("[POC] GameSession.SetPlanet (no cellId): " +
                      (planet != null ? planet.displayName : "null"));
        }
    }

    /// <summary>
    /// 마지막 셀 ID 가 있으면 복원해 PlanetInstance 를 만들고 CurrentPlanet 으로 설정.
    /// GPS 사용 불가/지연 시 폴백용. 성공 시 true.
    /// </summary>
    public bool TryRestoreLastPlanet()
    {
        string cellId = PlayerPrefs.GetString(PrefsKeyLastSeedCell, "");
        if (string.IsNullOrEmpty(cellId)) return false;

        ulong seed = PlanetSeed.SeedFromCellId(cellId);
        if (seed == 0UL) return false;

        var planet = PlanetGenerator.Generate(seed);
        planet.cellId = cellId;
        PlanetGenerator.LoadAndAssignSprite(planet);
        CurrentPlanet = planet;
        Debug.Log("[POC] GameSession.TryRestoreLastPlanet: restored " +
                  planet.displayName + " from cell " + cellId);
        return true;
    }

    /// <summary>테스트/디버그용 — 강제 클리어.</summary>
    public void Clear()
    {
        CurrentPlanet = null;
    }

    // ---- 잡은 생물 컬렉션 (행성별, PlayerPrefs 백업) ----
    // "내 행성으로 전송"의 종착지: 잡은 종을 현재 행성 셀의 컬렉션에 누적.
    // 행성 위 실제 배치/발전 시각화는 후속 라운드(범위 밖). 여기선 기록+카운터만.

    private string CollectionKey()
    {
        string cell = (CurrentPlanet != null && !string.IsNullOrEmpty(CurrentPlanet.cellId))
            ? CurrentPlanet.cellId : "default";
        return CollectionKeyPrefix + cell;
    }

    /// <summary>현재 행성 셀의 잡은 종→마릿수 딕셔너리(읽기 전용 스냅샷).</summary>
    public Dictionary<string, int> GetCollection()
    {
        var dict = new Dictionary<string, int>();
        string raw = PlayerPrefs.GetString(CollectionKey(), "");
        if (string.IsNullOrEmpty(raw)) return dict;
        foreach (var entry in raw.Split(','))
        {
            if (string.IsNullOrEmpty(entry)) continue;
            int colon = entry.LastIndexOf(':');
            if (colon <= 0) continue;
            string id = entry.Substring(0, colon);
            if (int.TryParse(entry.Substring(colon + 1), out int c) && c > 0) dict[id] = c;
        }
        return dict;
    }

    /// <summary>한 종 +1 기록(현재 행성 셀). 저장 후 새 누적 총합 반환.</summary>
    public int AddCaught(string speciesId)
    {
        if (string.IsNullOrEmpty(speciesId)) return CaughtTotal;
        var dict = GetCollection();
        dict.TryGetValue(speciesId, out int cur);
        dict[speciesId] = cur + 1;

        var sb = new System.Text.StringBuilder();
        bool first = true;
        foreach (var kv in dict)
        {
            if (!first) sb.Append(',');
            sb.Append(kv.Key).Append(':').Append(kv.Value);
            first = false;
        }
        PlayerPrefs.SetString(CollectionKey(), sb.ToString());
        PlayerPrefs.Save();

        int total = 0; foreach (var v in dict.Values) total += v;
        Debug.Log("[POC] GameSession.AddCaught: " + speciesId + " → cell " +
                  (CurrentPlanet != null ? CurrentPlanet.cellId : "default") + " total=" + total);
        return total;
    }

    /// <summary>현재 행성 셀의 잡은 생물 총 마릿수.</summary>
    public int CaughtTotal
    {
        get { int t = 0; foreach (var v in GetCollection().Values) t += v; return t; }
    }
}
