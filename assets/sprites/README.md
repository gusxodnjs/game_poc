# Sprites — TERRA × BIOSPHERE PoC

PixelLab API (`/v1/generate-image-pixflux`)로 생성한 6종 픽셀 아트 스프라이트.
16×16은 PixelLab `/resize` 엔드포인트가 없어 PIL LANCZOS로 다운샘플 (PoC 한정 폴백 — 본 제작 시 64×64 init image로 재생성 또는 수동 정리 권장).

## 매트릭스

| species_id | display_name | 64×64 | 16×16 |
|---|---|---|---|
| `dandelion` | 민들레 | ![dandelion 64](https://raw.githubusercontent.com/gusxodnjs/game_poc/main/assets/sprites/dandelion_64x64.png) | ![dandelion 16](https://raw.githubusercontent.com/gusxodnjs/game_poc/main/assets/sprites/dandelion_16x16.png) |
| `foxtail_grass` | 강아지풀 | ![foxtail 64](https://raw.githubusercontent.com/gusxodnjs/game_poc/main/assets/sprites/foxtail_grass_64x64.png) | ![foxtail 16](https://raw.githubusercontent.com/gusxodnjs/game_poc/main/assets/sprites/foxtail_grass_16x16.png) |
| `white_clover` | 토끼풀 | ![clover 64](https://raw.githubusercontent.com/gusxodnjs/game_poc/main/assets/sprites/white_clover_64x64.png) | ![clover 16](https://raw.githubusercontent.com/gusxodnjs/game_poc/main/assets/sprites/white_clover_16x16.png) |
| `cherry_blossom` | 벚꽃 | ![cherry 64](https://raw.githubusercontent.com/gusxodnjs/game_poc/main/assets/sprites/cherry_blossom_64x64.png) | ![cherry 16](https://raw.githubusercontent.com/gusxodnjs/game_poc/main/assets/sprites/cherry_blossom_16x16.png) |
| `ladybug` | 무당벌레 | ![ladybug 64](https://raw.githubusercontent.com/gusxodnjs/game_poc/main/assets/sprites/ladybug_64x64.png) | ![ladybug 16](https://raw.githubusercontent.com/gusxodnjs/game_poc/main/assets/sprites/ladybug_16x16.png) |
| `honeybee` | 꿀벌 | ![bee 64](https://raw.githubusercontent.com/gusxodnjs/game_poc/main/assets/sprites/honeybee_64x64.png) | ![bee 16](https://raw.githubusercontent.com/gusxodnjs/game_poc/main/assets/sprites/honeybee_16x16.png) |

## 프롬프트

모든 호출은 `no_background=true`, `image_size={width:64,height:64}`.

| species_id | prompt |
|---|---|
| `dandelion` | (이전 에이전트 산출물 — `feat(art): add PixelLab-generated dandelion sprite (PoC verification) (#6)`) |
| `foxtail_grass` | `green foxtail grass plant, fluffy seed spike, side view, pixel art, transparent background` |
| `white_clover` | `white clover plant with three green leaves, small white flower, top-down view, pixel art, transparent background` |
| `cherry_blossom` | `pink cherry blossom flower with branch, spring, side view, pixel art, transparent background` |
| `ladybug` | `red ladybug with black spots, top-down view, pixel art, transparent background` |
| `honeybee` | `yellow and black honeybee with wings, side view, pixel art, transparent background` |

## 생성 메타데이터

- **생성일**: 2026-05-14
- **API**: `POST https://api.pixellab.ai/v1/generate-image-pixflux`
- **이번 배치 API 호출**: 5회 (재시도 0회)
- **이번 배치 총 비용**: $0.0 (응답 `usage.usd` 합계 — 크레딧/무료 티어로 추정)
- **응답 시간 평균**: ~30.5초/이미지 (21.6s ~ 47.4s)
- **다운샘플 방식**: PIL `Image.LANCZOS` (64→16, 4× 축소)
- **생성 스크립트**: `scripts/generate_sprites.py`
- **상세 로그**: `scripts/generate_sprites_result.json`

## 검증

전체 12개 PNG 모두:
- PNG 시그니처 (`89 50 4E 47 0D 0A 1A 0A`) 정상
- 8-bit RGBA, non-interlaced
- `data/species.json`의 `sprite` 필드와 파일명 일치 (이번 배치에서 일부 갱신: `foxtail_*`, `clover_*`, `cherry_*`, `bee_*` → 실제 `species_id` 기반 명명으로 통일)
