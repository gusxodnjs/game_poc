---
name: planet-seed-system
description: GPS 좌표 → 1km 셀 hash → 행성 자동 결정 시스템 (시나리오 v2 §5 / §7 구현)
metadata:
  type: project
---

PlanetIntroScene 가 SplashScene 와 HelloScene 사이에 위치. GPS 1회 fix → 1km 그리드(0.01° floor) hash → seed → PlanetGenerator.Generate → GameSession.SetPlanet 흐름.

**Why:** 사용자에게 "내가 서 있는 자리 = 내 행성" 메타포를 강제. 동네별 deterministic 행성 자동 재현. 시나리오 v2 §1 "지금 당신이 서 있는 자리에서…" 톤 보장.

**How to apply:**
- 행성 셀 그리드는 `PlanetSeed.GridResolution = 0.01` (1km). `CellMapping.GridResolution = 0.001` (~111m, 종 발견용)와 의도적 분리. 둘을 절대 합치지 말 것
- 시드 비트 영역: type=[0..7], hueShift=[8..15], adj=[16..23], noun=[24..31]. 변경 시 deterministic 깨짐
- 블랙리스트 (v2 §5.3, 총 19개) 매칭 시 `noun_idx = (noun_idx+1) % 8` 한 칸 회전. 무한루프 방지 위해 NounCount 횟수 guard
- 런타임에서 카드 Sprite 로드: `AssetDatabase` 는 Editor 전용. `PlanetIntroScene.cardSpritesByType` (Inspector serialized Sprite[3]) 에 PocBuildPipeline.SetupPlanetIntroScene 가 빌드 타임에 주입. Resources 폴더 사용 안 함
- GPS 폴백 순서 (PlanetIntroScene.DetermineCurrentPlanet): (1) PlayerPrefs("debug_force_seed_cell") → (2) Editor 의 debugForceLatLon → (3) Input.location.Start (15초 타임아웃) → (4) PlayerPrefs("last_seed_cell") 복원 → (5) 서울시청 fallback. 경고 없이 진행 (PoC §7)
- DiscoveryDetection 은 GameSession.Instance.CurrentPlanet.speciesPool 우선, null 이면 Inspector speciesNames fallback (NPE 가드 필수)
- 시드 알고리즘: prime-mix (FNV/splitmix64 변형). xxHash64 미사용 — 외부 의존성 회피. 분포 검증: 한국 위경도 grid 1만 샘플 → 각 타입 33% ±2% (PlanetGeneratorTests 가 28~38.5% 범위로 검증)

관련: [[project-terra-poc]] [[map-shell-architecture]]
