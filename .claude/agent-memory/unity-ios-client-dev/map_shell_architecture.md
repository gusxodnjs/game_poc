---
name: map-shell-architecture
description: M1 지도 셸 — 자체 OSM raster tile 방식, 외부 지도 SDK 0
metadata:
  type: project
---

피크민 블룸 류의 "지도 위 캐릭터" 경험을 위해 외부 지도 SDK (Mapbox/Lightship) 미사용, 자체 OSM raster tile 다운로드 방식으로 결정.

**Why:** 외부 SDK 의존성 축소 (PoC 단계), 라이센스/비용 회피, iOS 빌드 용이성. OSM 무료 + 매너만 지키면 됨.

**How to apply:**
- OSM tile URL: `https://tile.openstreetmap.org/{z}/{x}/{y}.png`
- **User-Agent 필수**: `terra-poc/0.1 (gusxodnjs@gmail.com)` — OSM 약관 위반 시 IP 차단
- 동시 다운로드 ≤4, rate limit 의식
- 줌 17 고정 (≈1.2m/px @ 서울 위도)
- 메모리 LRU 50엔트리, 디스크 캐시 `Application.persistentDataPath/tiles/{z}_{x}_{y}.png`
- 좌표 변환은 `GeoCoord` (Web Mercator EPSG:3857 표준)
- 1 타일 = 1 Unity unit (SpriteRenderer PixelsPerUnit=256)
- 카메라 Orthographic, ortho size 1.5 → 화면 ~3타일
- 동적 텍스처 생성 금지 정책 ([[feedback-no-dynamic-textures]]) — 모든 타일은 디스크 PNG → LoadImage 경유
- M1 범위: 지도만 표시. M2 (다음 라운드): PlayerAvatar, CameraFollow, GPS 추적, walker 애니메이션
- MapView 외부 API: `SetCenter(lat, lon, zoom)`, `Zoom { get; set; }` — 이걸로만 갱신

관련: [[project-terra-poc]] [[feedback-no-dynamic-textures]]
