---
name: project-poc-scope
description: 작은정복자들 PoC v7.2 범위 — 무엇이 들어가고 무엇이 빠지는지의 경계선.
metadata:
  type: project
---

PoC는 **체험 시연** 목적의 Unity 6 LTS / iOS 빌드. 핵심 사이클 "산책 → 발견 → 안치"만 검증.

**Why:** 사용자/PM이 PoC 제외 범위를 명시. 시연 5분 내에 행성 3종 풍경을 모두 보여줘야 하므로 영구성 잠금 같은 "출시판 무게감" 장치는 의도적으로 배제.

**How to apply:** 아래 표를 디스패치 라운드마다 참조해 스코프 크리프 방지.

| 영역 | PoC 포함 | PoC 제외 (출시판 이월) |
|------|---------|---------------------|
| 화면 | Splash, PlanetSelect, PlayScene | 온보딩 컷신, 도감, 마이가든 |
| UI 기술 | IMGUI (PlanetSelect까지) | uGUI/UI Toolkit 마이그레이션 |
| 종 데이터 | 6종 (식물 4 + 곤충 2), 행성당 4+보너스1 | 희귀도, 어드밴티지, 도감 4단 해금 |
| 영상/사운드 | 정적 시퀀스 + IMGUI 텍스트 | 컷신, 사운드/음악 (전 영역) |
| 행성 차별화 | Shader Tint 1장 + 시작 종 풀 | 별도 맵·동물 AI·공생 애니 |
| 영구성 | 매 세션 자유 선택 (last_planet 기억만) | 1회 잠금, 행성 변경 의식 |
| 로컬라이즈 | 한국어 단일 | 다국어 |

관련: [[project-planets-v1]], [[scenario-v1-source]].
