# Background Tiles

지도 화면 배경 타일. TERRA v7 본문 환경 (도시 인도, 잔디밭/공원, 흙길/산책로, 꽃밭) 대응. 모두 32×32, seamless tileable로 생성.

| 파일 | 사이즈 | 프롬프트 | 미리보기 |
|---|---|---|---|
| `tile_grass_32x32.png` | 32×32 | seamless tileable grass tile, top-down view, pixel art | ![grass](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-chars-tiles-ui/assets/tiles/tile_grass_32x32.png) |
| `tile_sidewalk_32x32.png` | 32×32 | seamless tileable urban sidewalk tile with concrete blocks, top-down view, pixel art | ![sidewalk](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-chars-tiles-ui/assets/tiles/tile_sidewalk_32x32.png) |
| `tile_dirt_path_32x32.png` | 32×32 | seamless tileable dirt path tile, top-down view, pixel art, natural | ![dirt](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-chars-tiles-ui/assets/tiles/tile_dirt_path_32x32.png) |
| `tile_flower_field_32x32.png` | 32×32 | seamless tileable flower field tile with small pink and yellow flowers on grass, top-down view, pixel art | ![flower](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-chars-tiles-ui/assets/tiles/tile_flower_field_32x32.png) |

- 생성일: 2026-05-14
- 생성기: PixelLab `/v1/generate-image-pixflux`
- 호출 수: 4
- 비용: $0.00 (보고된 usage.usd 기준)

## 매핑 (TERRA v7 종 ↔ 타일)

| 종 | 권장 타일 |
|---|---|
| 강아지풀 | `tile_sidewalk_32x32` (도시 인도) |
| 토끼풀 | `tile_grass_32x32` (잔디밭/공원) |
| 벚꽃 | `tile_flower_field_32x32` (꽃밭/시즌) |
| 기타 식물/곤충 | `tile_dirt_path_32x32` (흙길/산책로) |
