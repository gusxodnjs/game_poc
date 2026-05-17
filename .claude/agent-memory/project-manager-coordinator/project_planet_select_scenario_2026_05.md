---
name: project-planet-select-scenario-2026-05
description: 2026-05-17 — 게임 시작 시 행성 선택 화면 + 시나리오 도입. PlanetSelectScene 신설, 행성마다 환경·종 풀·시작 자원 차별화
metadata:
  type: project
---

사용자 원문(2026-05-17): "게임시작 눌렀을 때, 본격적으로 게임이 시작되어야 해. 기획자는 시나리오를 기획해보자. 여러가지 행성중 한가지를 선택하는 화면을 먼저 보여주는건 어때? 행성마다 특징이 있고 시작하는 환경이 다른거야"

**Why:** 현재 SplashScene→HelloScene 구조에서 HelloScene은 GPS 검증용 더미. "본격 게임 시작" 요청에 답하려면 (a) 선택 의례 (b) 세계관 진입 시나리오 (c) 차별화된 시작 환경이 필요. 또한 [[project-map-player-avatar-2026-05]] 지도+캐릭터 작업과 동시 진행 중 — 두 작업을 PlayScene에서 합치는 통합 계획이 필수.

**How to apply:**
- 씬 흐름 신규: SplashScene → PlanetSelectScene → (선택 시 1회) IntroScenario → PlayScene. 이후 재진입 시 PlanetSelectScene을 건너뛰고 마지막 선택한 행성의 PlayScene으로 직행 (PlayerPrefs key: `selected_planet_id`).
- 행성 컨셉 PM 1차 권고: (B) "내 세계관 인스턴스" 안 — 환경 테마 행성 3종(숲/도시/해변). 사용자 원문 "시작하는 환경이 다른거야"와 가장 부합. 캐주얼 톤 유지하면서 피크민 블룸 스타일과 충돌 없음. (A) SF 톤은 추후 확장으로 보류, (C) 시즌 행성은 콘텐츠 부담 큼.
- 행성 개수: PoC 3개 권장 (시연 부담 vs 선택의 의미 균형). 추후 4번째는 "?? 잠겨있음" 카드로 미래 확장 암시.
- 차별화: 시각(행성 비주얼 + 지도 톤) + 시작 종 풀(species.json 부분집합 4종) + 시작 보너스 종 1개. 발견 빈도/안치 속도는 PoC에서는 동일하게 (밸런싱 부담 최소).
- 시나리오 분량: PoC는 텍스트 1~2단락 + 행성 카드 호버 시 짧은 lore. 컷신은 제외 (README "온보딩 컷신" 명시적 제외 정책 유지).
- 영구성: PoC는 "1회 선택 후 변경 불가" 권장. 추후 "행성 전환" UI는 본 제작에서. PlayerPrefs로만 저장.
- IMGUI vs Canvas 분기점: 행성 3개 + 가로 스크롤 카드 UI는 IMGUI로 가능하지만 UX가 거칠다. PM 권고는 **이번 작업까지 IMGUI 유지, PlayScene 들어가면 Canvas 도입 검토**. 사용자 결정사항으로 보고.
- 통합 매트릭스 ([[project-map-player-avatar-2026-05]]와): PlanetSelect는 선행, Map+Avatar는 PlayScene 내부. 충돌 X. 단 행성별 지도 톤 다르게 하면 Map M1 자산 부담 증가 — PoC에서는 "지도 색조 필터 1개 추가"로 최소화 권장.
- species.json 확장 필요: 행성별 종 매핑 메타 추가 (`planet_pool: {"forest": [...], "city": [...], "beach": [...]}`). 또는 별도 `planets.json` 신규.
- 신규 자산 부담: 행성 카드 비주얼 3개 (256×256, 픽셀아트). 기존 splash 8프레임 중 1~2장 재활용 가능(푸른 지구). 톤 다운된 도시/해변 행성은 PixelLab 신규.
- 관련: [[project-map-player-avatar-2026-05]] (동시 진행), [[project-splash-renewal-2026-05]] (선행), [[feedback-user-workflow]] (보고 패턴), [[feedback-pixellab-quirks]] (자산 생성 함정)
