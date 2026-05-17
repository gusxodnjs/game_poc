---
name: project-map-player-avatar-2026-05
description: 2026-05-17 시작 — 피크민 블룸 스타일 지도 위 플레이어 아바타 이동. M1~M4 마일스톤, 지도 SDK 비교 검증, walker 자산 재사용
metadata:
  type: project
---

피크민 블룸(Pikmin Bloom) 참조 — 지도 위에서 GPS 따라 걷는 플레이어 아바타를 구현한다. 직전 스플래시 PR 이후 다음 코어 마일스톤.

**Why:** 사용자(2026-05-17 메시지) "GPS 동작도 확인했어. 피크민처럼 지도안에서 내가 걸어가는 모습이 보였으면 좋겠어." 산책→발견→안치 코어 루프 중 "산책"의 시각화를 만들어 PoC의 두 번째 질문("진짜 재미있는가?")에 답할 수 있게 함.

**How to apply:**
- 마일스톤 권장 순서: M1 지도 표시(노 캐릭터) → M2 플레이어 아바타 정적 표시 → M3 GPS 추적(좌표 이동) → M4 walk/idle 애니메이션 + 방향 처리. 각 마일스톤마다 iOS 빌드 시연.
- 지도 솔루션 3가지 비교 결과 PM 1순위 권고: **자체 OSM raster tile** (외부 SDK 의존성·API key 관리 부담 최소, PoC 1주 일정에 적합). Mapbox는 일러스트 톤 원할 시 2순위, Lightship은 PoC 단계 오버킬.
- 핵심 신규 컴포넌트: MapView · GeoCoord(테스트 가능한 순수 유틸) · PlayerAvatar · CameraFollow. 기존 GpsCheck/DiscoveryDetection은 보존하되 GpsCheck는 새 MapView로부터 좌표를 받는 형태로 흡수 가능.
- 자산 재사용: `assets/characters/walker_*` (front idle, side walk). 신규 필요 가능성: back view(위쪽 진행) — game-design-planner 결정 후 pixellab 의뢰. PM 잠정 권고는 "front/side만으로 4방향 대응(북=후방은 side flip + 톤 다운, 남=front, 동/서=side flip)"으로 신규 자산 생성 0건 시작.
- IMGUI 일관성 유지 정책은 UI에만 적용. 지도/캐릭터는 SpriteRenderer 또는 RawImage(Texture2D 동적 로드)로 처리하되, 동적 텍스처 생성 트라우마(검은 화면) 회피 위해 타일은 디스크 캐시 후 로드 패턴 권장.
- iOS 메모리: 타일 LRU 캐시(최대 ~50타일, ~10MB) + 화면 밖 unload. 관련 함정 [[feedback-pixellab-quirks]] 와 별개.
- 관련: [[project-splash-renewal-2026-05]] (선행 작업), [[feedback-user-workflow]] (보고 패턴)
