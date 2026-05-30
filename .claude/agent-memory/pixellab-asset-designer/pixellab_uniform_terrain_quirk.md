---
name: pixellab-uniform-terrain-quirk
description: pixflux는 32x32 base 지형 타일에 장식(덤불/풀경계/대각선 길/잎사귀)을 멋대로 추가함 — uniform 타일 받기 어려움
metadata:
  type: project
---

PixelLab pixflux는 32×32 **단일 균일 지형(base terrain) 타일**을 만들 때 프롬프트와 무관하게 장식 요소를 끼워 넣는 경향이 강하다.

관찰된 사례 (#52 Earth tilemap base 타일셋, grass/path/water/forest):
- `"dirt path"` → 풀밭을 가로지르는 **대각선 흙길 + 녹색 풀 경계**가 생김 (장면이지 타일이 아님).
- `"bare dry dirt, NO grass, NO green"` (guidance 11~13) → 그래도 **녹색 잎/덤불 클럼프**가 박힘. guidance를 13까지 올리면 오히려 잎 클럼프가 더 커졌다.
- `"grass"` → 풀 대신 **덤불/관목 덩어리**가 생김.

**효과가 있었던 것:**
- 프롬프트에 `"single uniform terrain type only"`, `"completely filling the canvas edge to edge"`, 명시적 부정어(`no bushes, no shrubs, no flowers, no rocks`) 추가.
- grass/path는 guidance_scale 11.0이 9.0보다 균일했음. 단 13.0은 과해서 역효과(잎 확대).
- water/forest는 첫 시도(guidance 9.0)에서 바로 양호 — 물결/캐노피는 원래 패턴 텍스처라 잘 나온다.

**Why:** v1 타일맵 렌더러는 각 셀을 솔리드 블록으로 칠하므로 타일 안의 장식이 고정 위치에서 반복돼 격자 무늬처럼 보인다. autotile edge transition은 v3 예정이라 base 타일은 진짜 균일해야 한다.

**How to apply:** dirt/흙/모래 같은 "맨바닥" 타일은 1~2회 재생성을 예산에 넣고, 여러 결과 중 장식이 가장 적은 것을 고른다 (백업 cp 후 비교). 완벽한 bare dirt는 32×32에서 어려우니 sparse한 specks 정도는 수용. 필요하면 후처리로 잎 픽셀을 흙색으로 페인트하는 방법 고려. tileable 검증은 항상 2×2 타일 프리뷰를 만들어 seam/반복 패턴을 눈으로 확인.

관련: [[pixellab-generation-pattern]] [[pixellab-min-canvas-quirk]] [[asset-verification-checklist]] [[project-style-guide]]
