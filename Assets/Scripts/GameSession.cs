// GameSession.cs
// TERRA × BIOSPHERE PoC — 세션 전역 상태 (현재 행성). DontDestroyOnLoad.
//
// 책임:
//   - PlanetIntroScene 에서 결정된 PlanetInstance 를 HelloScene 까지 캐리.
//   - PlayerPrefs("last_seed_cell") 저장/복원 (시나리오 v2 §7 폴백).
//
// 의도된 미니멀리즘: GPS 폴링/씬 전환은 각 씬 스크립트 책임. 본 클래스는 데이터만.

using UnityEngine;

public class GameSession : MonoBehaviour
{
    public const string PrefsKeyLastSeedCell = "last_seed_cell";

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
}
