# Assets — PixelLab 생성 픽셀아트 인덱스

TERRA × BIOSPHERE PoC의 픽셀아트 에셋. PixelLab API (`/v1/generate-image-pixflux`) 기반 생성.

## 카테고리

| 디렉토리 | 내용 | README |
|---|---|---|
| [`sprites/`](./sprites/) | 6종 동/식물 스프라이트 (16×16 / 64×64) | [sprites/README.md](./sprites/README.md) |
| [`characters/`](./characters/) | 플레이어 캐릭터 (front/side) | [characters/README.md](./characters/README.md) |
| [`tiles/`](./tiles/) | 배경 타일 4종 (32×32) | [tiles/README.md](./tiles/README.md) |
| [`world/`](./world/) | 행성 (회색/녹색 상태, 128×128) | [world/README.md](./world/README.md) |
| [`ui/`](./ui/) | UI 아이콘 (망·도감·핀·빛 점) | [ui/README.md](./ui/README.md) |

> `sprites/`는 병렬 디자이너 에이전트가 별도 PR로 작업 중. 본 인덱스는 링크만 제공.

## 워크플로

1. `.env.local`의 `PIXELLAB_API_KEY` 로드 (gitignored)
2. `scripts/gen_assets.py` 실행 — 각 자산을 base64로 받아 PNG 저장, 시그니처 검증
3. 카테고리별 README에 마크다운 이미지 임베드 + 프롬프트 기록

## 제약

- PixelLab 최소 캔버스: 32×32 — 그보다 작은 결과물은 32×32에서 생성 후 nearest-neighbor 다운스케일 (`light_spot_16x16.png` 케이스).
- 응답 `usage.usd`는 모든 호출에서 0.0 (프로모션/무료 구독).

## 본 제작 권고

- **캐릭터**: 4방향 (front/back/left/right) × 걷기 2~4프레임 시트.
- **타일**: 각 환경 variant 2~3종 + 코너/에지 타일 (autotile 룰셋).
- **행성**: 회색 → 녹색 사이 N(예: 6) 단계 보간 또는 종 안치 위치 별 패치 오버레이.
- **UI**: 도감 빈/완료 상태, 채집망 휘두름 1~3프레임, 발견 핀 ping 애니메이션.
