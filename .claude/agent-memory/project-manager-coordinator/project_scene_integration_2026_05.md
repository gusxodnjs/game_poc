---
name: scene-integration-2026-05
description: 행성 인트로+지도+아바타 씬 통합은 신규 코딩 아님 — 셋업 코드는 PocBuildPipeline에 이미 커밋됨, 에디터 메뉴 재실행으로 산출물만 재생성
metadata:
  type: project
---

행성 인트로 + 지도 + 아바타 씬 통합 작업(2026-05-30 지시)의 본질: **신규 코딩이 아니라 에디터 메뉴 재실행으로 씬 산출물 재생성**.

핵심 사실 (코드에서 직접 확인, origin/main 6956bea 기준):
- `assets/Editor/PocBuildPipeline.cs`에 통합 셋업 코드가 **이미 전부 커밋**돼 있음:
  - `SetupHelloScene()` (메뉴 `TERRA PoC/1`) — MapView+PlayerAvatar+GpsCheck+DiscoveryDetection 4개 GameObject wiring. GpsCheck.mapView SerializeField에 MapView 주입. PlayerAvatar에 idle 4프레임/ring/shadow Texture 주입.
  - `SetupPlanetIntroScene()` (메뉴 `1c`) — PlanetIntroScene MonoBehaviour + cardSpritesByType[3] (0=Volcano,1=Ice,2=Desert) Sprite 주입.
  - `UpdateBuildScenes()` — EditorBuildSettings를 Splash→PlanetIntro→Hello로 원자적 재정합. 존재하는 씬만 등록.
- GameSession(DontDestroyOnLoad)이 PlanetIntroScene→HelloScene으로 PlanetInstance 캐리. DiscoveryDetection이 GameSession.CurrentPlanet.speciesPool 소비(연결 완료).
- 폐기된 건 산출물(.unity 씬 파일, EditorBuildSettings)뿐. 코드는 무손실.

**Why:** 사용자가 워킹트리 진행분을 clean tree로 폐기·stash 비움. 하지만 산출물을 생성하는 에디터 코드는 커밋돼 있어 복구 불필요.

**How to apply:** 이 통합/유사 씬 복구 작업은 단일 PR 권장(UpdateBuildScenes가 3씬을 원자 정합하므로 분리 시 중간 빌드불가 상태 발생). 실행은 batchmode로 `SetupSplashScene → SetupPlanetIntroScene → SetupHelloScene` 순서(Hello 마지막 = 빌드순서 정합 보장). 검증은 로그의 "cards injected 3/3", "avatar wired idle=4/4", "scenes: Splash→PlanetIntro→Hello", GpsCheck wiring 경고 없음. 손으로 .unity YAML 편집 금지 — 에디터 메뉴가 canonical 복구 경로. 관련: [[game_mechanic_gps_seed_planets]] [[workflow_pr_per_task]]
