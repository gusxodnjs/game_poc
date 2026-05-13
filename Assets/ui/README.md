# UI Icons

PoC 화면 구성 요소.

| 파일 | 사이즈 | 프롬프트 | 용도 | 미리보기 |
|---|---|---|---|---|
| `net_icon_32x32.png` | 32×32 | butterfly net icon, side view, simple, pixel art, transparent background | 미니게임 2 (곤충 채집망) | ![net](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-chars-tiles-ui/assets/ui/net_icon_32x32.png) |
| `book_icon_32x32.png` | 32×32 | open notebook with bookmark icon, simple, pixel art, transparent background | 도감 탭 | ![book](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-chars-tiles-ui/assets/ui/book_icon_32x32.png) |
| `map_pin_32x32.png` | 32×32 | red map pin marker, simple, pixel art, transparent background | 지도 발견 표시 | ![pin](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-chars-tiles-ui/assets/ui/map_pin_32x32.png) |
| `light_spot_16x16.png` | 16×16 | glowing yellow light dot, bright center, simple, pixel art, transparent background | 식물 펼치기 미니게임 빛 점 | ![light](https://raw.githubusercontent.com/gusxodnjs/game_poc/feat/pixellab-chars-tiles-ui/assets/ui/light_spot_16x16.png) |

## 비고

- PixelLab API 최소 캔버스는 32×32. `light_spot_16x16.png`는 32×32로 생성 후 Pillow nearest-neighbor로 16×16 다운스케일 (픽셀아트 톤 보존).
- 생성일: 2026-05-14
- 생성기: PixelLab `/v1/generate-image-pixflux`
- 호출 수: 4 (3 직접 + 1 다운스케일)
- 비용: $0.00 (보고된 usage.usd 기준)
