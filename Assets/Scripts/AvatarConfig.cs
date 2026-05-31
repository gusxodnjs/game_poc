using UnityEngine;

/// <summary>
/// 아바타 외형 설정(커스터마이징 seam). 지금은 기본값 1세트만 쓰지만,
/// 향후 커스터마이징 UI/저장이 이 데이터만 바꾸면 PlayerAvatar 렌더가 따라오도록 외형을 데이터로 분리.
///
/// 범위 밖(미구현): 추가 스프라이트 세트, 모자 토글 실제 동작, 색/모자 선택 UI, 저장·로드.
/// 본 클래스는 "미래 교체 지점을 한 곳으로 모으는" 구조 준비일 뿐이다.
/// </summary>
[System.Serializable]
public class AvatarConfig
{
    [Tooltip("스프라이트 세트 식별자(미래 다중 세트 선택용). 현재 'walker' 1종.")]
    public string spriteSetId = "walker";

    [Tooltip("캐릭터 틴트 색(향후 색 커스터마이징). 기본 흰색 = 원본 색 그대로.")]
    public Color tint = Color.white;

    [Tooltip("모자 착용 여부(미래 토글 자리). 현재 모자 에셋 없음 → 항상 false.")]
    public bool hat = false;
}
