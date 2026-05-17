---
name: project-gps-seed-planet-2026-05
description: 2026-05-17 — 행성 시스템 방향 전환. 카드 선택형 폐기, GPS 좌표 hash → 시드 → 행성 자동 결정 ("내가 걷는 곳이 내 행성")
metadata:
  type: project
---

사용자 결정 (2026-05-17): 4개 옵션 중 **C** 선택 — "GPS 좌표 = 행성 시드". A(고정 3종) 폐기, B(시드 미세변주)와 D(완전 절차생성) 보류.

**Why:** "내가 걷는 곳이 내 행성"이라는 위치 기반 게임 정체성 확립. 산책 게임의 핵심 메타포 (걸음으로 푸르러짐)와 직결 — 행성이 GPS에 묶여야 "내 동네 = 내 행성"이 성립. 카드 선택형은 위치성과 단절되어 산책 게임 정체성 희석.

**How to apply:**
- 기존 시나리오 v1 (`docs/scenario_v1.md`) → v2로 승계. 고정 3종 lore/색조 톤 풀은 시드 매핑 테이블로 재사용 (3종 유지 시 자산 부담 0). [[project-planet-select-scenario-2026-05]] 결정의 핵심 항목 (행성 3종 / 톤 / 종 풀)은 그대로 유지, "선택" 의례만 GPS 자동으로 교체.
- PlanetSelectScene → PlanetIntroScene (개명). 선택 UI 제거, "내 행성 소개" 화면으로 전환. "여기서 시작" 버튼 한 개. 디버그 시드 입력 hidden 메뉴 (시연용).
- 시드 그리드: 1km (lat/lon 0.01°). 1세션 산책 거리(보통 500m~2km) 안에서 행성 1~2개 경계만 넘는 수준. CellMapping (0.001°, 111m)은 발견 셀 용도로 유지 — 시드 셀과 별개.
- 시드 hash → 행성 타입은 PoC에서 기존 3종 유지 (volcano/ice/desert) % 3 매핑. 색조 hue shift ±15°로 미세 변주.
- 영구성: 매 진입 시 현재 GPS로 행성 결정. 단순화. last_visited_planets 등 컬렉션 기능은 PoC 외.
- 친구 시드 공유 / 시드 입력 UI는 PoC 외 (본 제작에서).
- 완료된 Unity M1 코드 (GeoCoord/CellMapping/TileCache/MapView)는 그대로 재사용. 신규 PlanetSeed 모듈은 GeoCoord.LatLonToTile 호출 또는 자체 lat/lon floor 처리.
- 완료된 PixelLab 마커 (player_shadow_32x16, player_accuracy_ring_64x64) GPS 시드와 무관, 유지.
- 관련: [[project-planet-select-scenario-2026-05]] (대체 대상), [[project-map-player-avatar-2026-05]] (영향 없음, 통합 계속), [[game-tone-primitive-planets]] (원시 행성 톤 원칙 유지)
