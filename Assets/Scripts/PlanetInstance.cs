// PlanetInstance.cs
// 단일 행성 인스턴스 — seed 로부터 PlanetGenerator.Generate 가 채워준 POCO.
// PlanetIntroScene 가 화면에 표시할 모든 필드를 담는다.
//
// 메모: Unity Editor 에서 직접 참조하지 않으므로 MonoBehaviour 일 필요 없음.
// GameSession.CurrentPlanet 으로 세션 전역 공유.

using UnityEngine;

public class PlanetInstance
{
    /// <summary>이 행성을 결정한 seed (1km 셀 hash).</summary>
    public ulong seed;

    /// <summary>행성 base type (lore/색조/종 풀의 키).</summary>
    public PlanetType type;

    /// <summary>자동 생성된 표시 이름 (예: "고요한 잿빛 언덕").</summary>
    public string displayName;

    /// <summary>base 색조 (시나리오 v2 §2 — Shader Tint 적용 전 기준).</summary>
    public Color baseTint;

    /// <summary>시드 변주 hue 오프셋 (-15 ~ +15 도).</summary>
    public float hueShift;

    /// <summary>base type 별 lore 텍스트 (1~2문장).</summary>
    public string lore;

    /// <summary>시작 종 풀 (4종).</summary>
    public string[] speciesPool;

    /// <summary>보너스 종 ID (1종).</summary>
    public string bonusSpeciesId;

    /// <summary>"여기서 첫 걸음" 클릭 후 표시되는 진입 시나리오 한 컷.</summary>
    public string introScenario;

    /// <summary>표시용 카드 Sprite (런타임 로드).</summary>
    public Sprite cardSprite;

    /// <summary>디버그용 셀 ID (예: "3756,12697"). 화면에 노출 안 함, 로그용.</summary>
    public string cellId;
}
