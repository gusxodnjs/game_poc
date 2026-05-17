---
name: project-style-guide
description: 작은정복자들 (TERRA × BIOSPHERE PoC) 픽셀아트 톤 표준 — Unity 6 LTS iOS, 피크민 블룸 + 스타듀밸리 계열
metadata:
  type: project
---

작은정복자들은 Unity 6 LTS / iOS 기반 PoC 게임. 위치 기반 산책 게임 (피크민 블룸 유사) + 행성/바이오스피어 메타.

**비주얼 톤 표준:**
- 16-bit JRPG 스타일 픽셀아트 (Pokemon Black/White, Earthbound, Stardew Valley 계열)
- 피크민 블룸의 따뜻한 색감 + 스타듀밸리의 차분한 채도
- SF/하이테크 톤 X (행성이 등장해도 아기자기/자연 느낌 우선)
- 캐릭터: 4~5등신 인간, chibi/mascot/animal 톤 회피 (PR #9 회귀 사고 있었음)

**해상도 표준:**
- 캐릭터 정적/애니메이션 스프라이트: 64×64
- 캐릭터 마커 (그림자 등): 32×16
- UI 마커/링: 64×64
- 타일: 32×32
- 행성: 128×128
- 행성 카드 (예정): 256×256

**Unity Import 표준:**
- Texture Type: Sprite (2D and UI)
- Filter Mode: Point (no filter)
- Compression: None
- Pixels Per Unit: 64

**Why:** 톤이 흔들리면 사용자가 즉시 지적함 (PR #9 chibi 사고). walker 톤 = 모든 동반 자산의 앵커.

**How to apply:** 새 캐릭터/마커 자산 생성 시 항상 `assets/characters/walker_front_64x64.png`를 시각 앵커로 참조. 프롬프트에 `16-bit JRPG protagonist pixel art style like Pokemon Black White or Earthbound, Stardew Valley style` 포함. 마커/UI는 walker 의상 팔레트 (파란 청바지 톤, 차분한 채도)와 같은 계열 색상 우선.

관련: [[pixellab-generation-pattern]] [[asset-directory-conventions]]
