# 지구 타일맵 리얼리티 개선 — 타입 추가 + 오토타일 가장자리 전이

- **이슈:** #52 (v1 배포 피드백 기반 개선; 머지 전 `feat/52-earth-tilemap-pikmin` 브랜치/PR #53에 이어서)
- **작성일:** 2026-05-30
- **선행:** v1 (base 타일 4종, 하드 블록 경계) — `docs/superpowers/specs/2026-05-30-earth-tilemap-pikmin-design.md`

---

## 1. 문제 & 목표

v1 배포 결과 맵이 "실제 지도처럼 보이지 않음": (1) 타입 경계가 하드 블록, (2) 종류가 4종뿐. 본 개선은:
- **오토타일 가장자리 전이** — 타입 경계를 부드럽게(Pokémon/Stardew 룩).
- **타일 종류 추가** — 포장도로(차도) vs 흙길 분리 + 건물.

승인된 접근: **A. Grass-base + 피처별 16-타일 오토타일 오버레이**, 16-타일로 시작(추후 47 확장 여지). 타일 변형·장식물은 범위 밖.

---

## 2. 타일 타입 확장

`TileType` (byte, 값 고정·추가만):
```
Grass = 0, Path = 1, Water = 2, Forest = 3, Road = 4, Building = 5
```
- **Path(1)** = 흙길/오솔길/인도/보행로 (비포장·보행자).
- **Road(4)** = 포장 차도 (아스팔트 회색).
- **Building(5)** = 건물 풋프린트.

### 2.1 OSM 분류 갱신 (`OverpassParser.Classify`)
| OSM 태그 | TileType | Geom |
|---|---|---|
| `highway` = motorway/trunk/primary/secondary/tertiary/unclassified/residential/service/living_street | **Road** | Polyline(buffer 1, 큰길은 2) |
| `highway` = footway/path/pedestrian/track/cycleway/steps/bridleway | **Path** | Polyline(buffer 1) |
| `building`=* | **Building** | Polygon |
| (기존) natural=water/water/waterway=riverbank | Water | Polygon |
| (기존) waterway=river/stream | Water | Polyline(buffer 1) |
| (기존) landuse=forest/natural=wood/leisure=park | Forest | Polygon |

`OverpassClient` 쿼리에 `way["building"]` 추가. highway는 이미 수집 중 — 분류만 세분.

### 2.2 우선순위 (래스터 Paint + 렌더 draw-order)
겹칠 때 우선순위(높을수록 위/덮음): **Building > Water > Road > Path > Forest > Grass**.
- 래스터라이저 `Priority()`에 Road=건물/물 아래, Path 위; Building 최상.
- 건물이 도로/물 위 풋프린트로 그려지는 실제 지도 직관과 일치.

---

## 3. 오토타일 (16-타일 Wang, grass-base 오버레이)

### 3.1 모델
Grass = 만능 배경. 비-grass 피처(Path/Road/Water/Forest/Building)는 각자 **grass로 번지는 16-타일 가장자리 세트**로 grass 위에 오버레이. 두 비-grass 피처 인접 시 §2.2 우선순위 draw-order로 처리(낮은 것 먼저, 높은 것 위에).

### 3.2 인덱싱 — `AutoTile.NeighborMask` 재활용
비트: **N=1, E=2, S=4, W=8** (같은 피처 이웃이면 set). 0~15. 비트 unset = 그 방향이 grass(또는 다른 피처) → 가장자리.
- 기존 `AutoTile.NeighborMask(TileType[,], x, y)` 그대로 사용(같은 타입 이웃 비트).
- 청크 경계 이웃: 렌더러는 인접 청크 데이터를 참조해 경계에서도 올바른 mask 산출(§4.2).

### 3.3 디자이너 ↔ 개발자 에셋 계약
- 디자이너는 피처 타입별(Path/Road/Water/Forest/Building) **16-타일 오토타일 시트**를 제작. grass 배경으로 자연스럽게 번지는 가장자리.
  - PixelLab **`create_topdown_tileset`** MCP 도구가 오토타일 전이 타일 생성에 적합(Task 2에서 확인: 16~23 전이타일 반환). 디자이너가 도구/방식 선택.
- **레이아웃 계약(필수):** 디자이너는 산출 시트의 **`mask(0~15) → 시트 내 셀 위치` 매핑을 문서화**해 넘긴다. 가능하면 표준 4×4 시트(128×128, 32px 셀, `col=mask%4, row=mask/4`, row 0=상단). PixelLab 산출이 이 순서와 다르면, 디자이너가 셀을 재배치하거나 매핑표를 명시 → 개발자가 그 매핑대로 인덱싱.
  - 참조 셀 의미: mask 0 = 사방 가장자리(고립 블롭), 15 = 내부 풀필, 1(N만) = 하/좌/우 가장자리, … (16개 전부 매핑표).
- 산출물: `Assets/world/tiles/{path,road,water,forest,building}_auto_128.png` (16-타일 시트) + grass 베이스(기존 `grass_32.png` 유지) + `tileset_layout.md`(매핑표).
- import 설정: Sprite(Multiple 또는 코드 슬라이스), Point, no-mip, PPU 32.

---

## 4. 렌더러 변경 (`TilemapRenderer`)

### 4.1 멀티패스
기존 단일 `_sprites[TileType]` → 레이어 구조:
1. **베이스 패스:** 모든 칸에 grass 스프라이트(현 방식). 기존 풀 재활용.
2. **오버레이 패스:** 칸의 TileType이 비-grass면, 그 피처의 16-타일 시트에서 `NeighborMask` 인덱스 스프라이트를 grass 위에 그림. 우선순위 정렬된 sortingOrder.
- 칸당 SpriteRenderer가 최대 2장(base + overlay) 필요 → 풀을 base/overlay 2종으로 운영하거나, overlay 전용 풀 추가.

### 4.2 청크 경계 mask
한 칸의 이웃이 옆 청크에 속할 수 있음 → `NeighborMask` 계산 시 청크 경계에서 인접 청크 `ChunkData`를 조회(없으면 grass 취급=경계 그려짐, 로드되면 다음 refresh에 갱신). 렌더러가 전역 타일좌표→(청크,로컬) 변환으로 이웃 TileType 조회하는 헬퍼 추가.

### 4.3 스프라이트 빌드
피처별 128×128 시트를 16개 32×32 Sprite로 슬라이스(`BuildAutoTileSprites`). `Sprite[feature][16]`. grass는 단일.

---

## 5. 에러/폴백 (v1 정책 계승)
- 시트 텍스처 null/슬라이스 실패 → 그 피처는 단일 베이스 스프라이트로 폴백(가장자리 없이라도 표시). 검은 화면 금지.
- 미로드 청크 → 전부 grass(기존).
- Overpass building 미응답 → 건물 없이 렌더(degrade).

---

## 6. 테스트
- `OverpassParserTests`: highway major→Road, highway foot→Path, building→Building 분류 추가 케이스.
- `FeatureRasterizerTests`: 우선순위(Building>Water>Road>Path>Forest) 케이스 추가.
- `AutoTileTests`: 기존 유지(인덱싱 변경 없음).
- 렌더러 멀티패스/슬라이스는 단위테스트 어려움 → 수동(Editor 실좌표) 검증.
- ⚠️ 빌드 안정성: 테스트 asmdef Editor 전용 유지. DoAll 빌드 깨지지 않는지 확인.

---

## 7. 단계 (PR #53에 이어서 커밋)
1. **에셋(디자이너 선행):** 5개 피처 16-타일 오토타일 시트 + 레이아웃 매핑표. PixelLab `create_topdown_tileset`.
2. TileType 확장 + OSM 분류 갱신(Road/Building) + 래스터 우선순위 + 파서 테스트.
3. 렌더러 멀티패스 + 청크경계 mask + 시트 슬라이스.
4. SetupHelloScene 타일셋 주입 갱신(시트 5종) + 씬 재생성 + DoAll 빌드 + 수동 검증.

---

## 8. 범위 밖 (후속)
- 타일 변형(반복 제거), 장식물/랜드마크(나무·바위 오브젝트) — 별도.
- 47-타일 인너코너 정밀화 — 16-타일 후 필요시.
- 모래/해변·논밭 타입 — 이번 제외.
- 나의 행성 발전 시스템.
