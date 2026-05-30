# 지구 산책 레이어 — Pikmin 스타일 실지형 픽셀 타일맵 재구현

- **이슈:** #52
- **작성일:** 2026-05-30
- **상태:** 설계 승인 대기
- **범위:** 지구(산책·발견) 레이어 v1. 나의 행성(발전) 시스템은 별도 후속.

---

## 1. 배경 & 컨셉

### 1.1 2레이어 모델
이 게임은 두 개의 분리된 레이어로 구성된다.

- **🌍 지구 (산책·발견 레이어)** — 플레이어가 실제로 걷는 현실 세계. 실제 내 동네(도로·물·녹지·건물)를 Pikmin Bloom처럼 큐트한 픽셀 타일로 렌더한다. 여기서 **생물을 발견**한다.
- **🪐 나의 행성 (발전 레이어)** — 발견한 생물을 보내서 키우는 나만의 행성. "원시 행성·도시 금지" 톤은 *이쪽*에만 적용된다.

코어 루프: **산책(지구 픽셀맵) → 발견(생물) → 전송(→내 행성) → 행성 발전**

본 문서는 **지구 레이어**만 다룬다. 현재 이 레이어(HelloScene)는 OSM **래스터** 타일(`MapView`/`TileCache`)로 실사 지도를 그린다. 이를 실지형 **픽셀 타일맵**으로 교체한다.

### 1.2 컨셉 충돌 해소
이전 메모리(`game_tone_primitive_planets`: 원시행성·도시금지, `game_mechanic_gps_seed_planets`: GPS 셀→픽션행성)는 *나의 행성* 레이어에 대한 것이다. 지구 레이어가 실제 도로·건물을 보여주는 것과 충돌하지 않는다. 두 메모리는 유효하되 "행성" 한정임을 명확히 한다(메모리 갱신 예정).

---

## 2. 핵심 결정 (브레인스토밍 산출)

| 항목 | 결정 |
|---|---|
| 지형 충실도 | **완전 실지형** — 실제 도로/물/녹지/건물을 픽셀로 스타일라이즈 (진짜 Pikmin) |
| 접근법 | **A. 타일그리드 + OSM Overpass** (무료, API 키 없음) |
| 타일 해상도 | **1 타일 ≈ 실세계 3m** (튜너블 상수) |
| 렌더 기술 | **코드드리븐 청크 렌더러** (Unity Tilemap 컴포넌트 미사용 — prefab-free 컨벤션 유지) |
| GPS | 기존 `Input.location` 유지. 아바타 화면 중앙 고정, 맵 패닝 |
| 타일셋 아트 | PixelLab 신규 제작 (풀·흙길+가장자리·물+물가·숲) |
| v1 표현요소 | 도로/길, 물, 녹지 (건물 v2, 생물 맵표시 v3) |

---

## 3. 아키텍처

타일 1칸 = 실세계 약 3m. Web Mercator 기준 전역 타일 그리드를 정의하여 같은 실제 위치는 항상 같은 타일 좌표(결정론적)가 되도록 한다.

### 3.1 컴포넌트 (단일 책임)

| 컴포넌트 | 책임 | 의존 | 종류 |
|---|---|---|---|
| `GeoTileGrid` | lat/lon ↔ 전역 타일좌표(tileX,tileY) 변환, 청크 ID 계산. 순수함수 | `GeoCoord` | static |
| `TileType` | 타일 분류 enum: `Grass, Path, Water, Forest`(+v2 `Building`) | — | enum |
| `OverpassClient` | bbox로 OSM 벡터 페치(highway/water·waterway/landuse). User-Agent, 동시요청 제한, 백오프 | UnityWebRequest | MonoBehaviour/싱글톤 |
| `FeatureRasterizer` | 벡터 way/폴리곤 → 청크 내 칸별 `TileType[,]`. 도로=라인버퍼, 물/녹지=점-in-폴리곤, 기본=Grass | `GeoTileGrid` | static |
| `ChunkCache` | 청크(32×32타일 ≈ 96m) 단위 페치→래스터화→**디스크 캐시**. 주변 로드/원거리 언로드 | `OverpassClient`, `FeatureRasterizer` | MonoBehaviour/싱글톤 |
| `TilemapRenderer` | `TileType` 그리드 → 픽셀 스프라이트 배치. **오토타일링**(이웃 비트마스크). 화면영역+여유분만 활성, 풀 반환 | `ChunkCache`, 타일셋 | MonoBehaviour |
| `PlayerAvatar` (기존) | 화면 중앙 고정 마커 (idle+ring+shadow) | — | 유지 |
| `GpsCheck` (기존, 수정) | `Input.location` → 플레이어 좌표. `mapView` 참조를 `TilemapRenderer`로 재배선 | `TilemapRenderer` | 수정 |

### 3.2 청크 모델
- 청크 = 32×32 타일 ≈ 96m × 96m. 청크 ID = floor(타일좌표 / 32).
- 디스크 캐시 경로: `Application.persistentDataPath/earthtiles/{chunkX}_{chunkY}.bin` (분류 그리드를 compact 직렬화 — 칸당 1바이트 TileType).
- 주변 3×3 청크 로드 유지, 그 밖은 언로드. Overpass 쿼리는 미캐시 청크의 합쳐진 bbox로 1회.

---

## 4. 데이터 흐름

```
GPS fix → 플레이어 Mercator 좌표 → GeoTileGrid → 현재/주변 청크 ID
  → ChunkCache:
       캐시 hit?  → 디스크에서 TileType 그리드 로드
       miss?     → OverpassClient 페치 → FeatureRasterizer → 디스크 캐시 저장
  → TilemapRenderer: 화면 주변 타일 오토타일링 렌더
  → 아바타 화면 중앙 고정, 맵은 플레이어 소수점 타일위치만큼 오프셋 패닝
```

### 4.1 오토타일링
- 각 타일은 같은 타입 **4-이웃 비트마스크(16-타일 Wang 방식)**로 가장자리 스프라이트 선택. v1은 16-타일로 단순화, 필요 시 8-이웃(47-타일)로 확장.
- 길(Path): 풀밭 위 흙길 → 가장자리/모서리 전이.
- 물(Water): 물가(shoreline) 전이.
- 숲(Forest): 풀밭 위 나무 밀도/가장자리.
- 타일셋은 비트마스크 인덱싱 가능한 형태로 PixelLab 제작.

---

## 5. 에러 처리 (검은 화면·크래시 금지 — 기존 TileCache 정책 계승)

| 상황 | 처리 |
|---|---|
| Overpass 실패/타임아웃/429 | 캐시 청크 있으면 사용 → 없으면 **전부 Grass placeholder** 렌더. 지수 백오프 재시도 |
| GPS 없음/권한 거부 | 폴백 좌표(서울시청 — 기존 `PlanetIntroScene` 패턴)로 맵 표시 |
| 오프라인 | 디스크 캐시 청크만, 미캐시는 Grass placeholder |
| Overpass 레이트리밋 | 동시요청 1~2개 제한 + 미캐시 청크 bbox 합쳐 쿼리 수 최소화(OSM 매너) |
| 파싱 실패(부분 데이터) | 파싱 가능한 피처만 적용, 나머지 Grass. 청크는 캐시하되 재시도 플래그 |

OSM 매너 준수: User-Agent `terra-poc/0.2 (gusxodnjs@gmail.com)` 필수, Overpass 공개 인스턴스 레이트리밋 존중.

---

## 6. 테스트

- **순수함수 EditMode 단위테스트**:
  - `GeoTileGrid`: lat/lon→타일 결정론성, 청크 ID 경계.
  - `FeatureRasterizer`: 알려진 도로 라인 → Path 칸, 물 폴리곤 → Water 칸, 빈 영역 → Grass.
  - 오토타일 비트마스크 인덱싱.
- ⚠️ **빌드 안정성 제약**: 과거 `fix/remove-tests-poc-stage`로 Tests 디렉토리가 PoC iOS 빌드 막힘 때문에 제거됨. 테스트는 **asmdef 분리 + 빌드 타깃 제외(Editor 전용)** 형태로만 추가하거나, 빌드에 걸리면 생략. **빌드 안정성 > 테스트 커버리지.**
- **수동 검증**: Editor에서 디버그 강제 좌표(실제 OSM 데이터가 풍부한 좌표 — 예: 한강/공원 인접)로 시각 확인. batchmode 셋업 로그(`TilemapRenderer wired`, 청크 로드 카운트).

---

## 7. 씬 통합

- 지구 레이어 = 현재 HelloScene. 래스터 `MapView`/`TileCache` → 신규 타일맵 시스템으로 교체.
- `GpsCheck.mapView`(SerializeField) → 신규 `TilemapRenderer` 참조로 재배선.
- `PocBuildPipeline.SetupHelloScene` 갱신: 코드드리븐 컴포넌트(`OverpassClient`/`ChunkCache`/`TilemapRenderer`) wiring 추가, 구 `MapView` 제거.
- `PlayerAvatar`·`DiscoveryDetection`·빌드 씬 순서(Splash→PlanetIntro→Hello)는 유지.

### 7.1 파일 정리
- **보존:** `Assets/Scripts/GeoCoord.cs` (Mercator 변환 — 재활용)
- **삭제:** `Assets/Scripts/MapView.cs`, `Assets/Scripts/TileCache.cs` (래스터 — 지구 레이어에서 대체됨) + 각 `.cs.meta`

---

## 8. 단계 분리 (각 별도 PR · 이슈)

- **v1 (이슈 #52 본체)**: §3 컴포넌트(도로·물·녹지) + 코드드리븐 렌더러+오토타일 + 청크 캐시 + PixelLab 베이스 타일셋 + HelloScene 배선 교체. 아바타+GPS 패닝 동작.
- **v2 (후속 이슈)**: 건물(`Building` 타입 + OSM building 폴리곤 + 픽셀 건물 타일).
- **v3 (후속 이슈)**: 나무 밀도·표지판·장식, 발견 생물 맵 표시(현재 텍스트 알림 → 맵 위 스프라이트).

---

## 9. 범위 밖 (Out of scope, v1)

- 나의 행성(발전) 시스템 전체 — 생물 전송/안치/행성 성장.
- 건물·POI 렌더 (v2).
- 발견 생물의 맵 표시 (v3) — v1은 기존 `DiscoveryDetection` 텍스트 알림 유지.
- 줌 레벨 동적 변경 — 고정 줌(3m/타일).
- 멀티플레이/타 플레이어 표시.
