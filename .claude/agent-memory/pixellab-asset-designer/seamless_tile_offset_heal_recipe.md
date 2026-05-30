---
name: seamless-tile-offset-heal-recipe
description: PixelLab 타일을 이음새 없게 만드는 offset-and-heal 후처리 레시피 (numpy 없이 PIL)
metadata:
  type: feedback
---

PixelLab text→image 타일은 wrap-edge가 안 맞아 반복 시 격자/줄무늬가 보인다. 후처리로 강제 seamless 화.

**Why:** issue #52에서 `grass_32.png`가 left↔right edge diff ~141(seamless는 ~0)이라 타일맵에 세로 이음새 격자가 그대로 노출됨. PixelLab은 seamless를 보장하지 않음.

**How to apply (offset-and-heal):**
1. PixelLab `create_topdown_tileset`로 톤/팔레트만 확보 → 128x128(4x4) 중 **std가 가장 낮은 cell**(=완전 채움 grass, transition arc 없음)을 32x32로 crop해 look-source로 사용. (이번엔 std~3.7 cell이 luminance range 21로 이미 calm)
2. 대비 압축: 모든 픽셀을 평균쪽으로 lerp + dark/teal 픽셀은 BASE green으로 강하게 pull → high-contrast blob 제거.
3. `numpy.roll` 대신 PIL로 (W/2,H/2) wrap roll → 원래 이음새가 중앙 +자 십자로 이동. 그 십자 band(±3px)를 주변 clean grass의 median으로 heal. roll 후 **새 바깥 edge = 옛 내부 픽셀**이라 자동으로 wrap-match.
4. 변형(blade/flower) 편집은 **re-seam보다 먼저** 적용. 안 그러면 edge에 이음새 재유입.

**검증:** spec의 seam(path) 함수로 H/V edge diff < 20 요구. 결과 v0~v3 모두 H 3~13, V 9 → 통과. 스크립트: `scripts/make_grass_variants.py` (pure PIL, repo는 numpy 미설치 + externally-managed라 pip 안 됨).

**Scatter 주의:** 4변형의 hue/밝기 nudge가 크면 무작위 배치 시 타일별 밝기 블록이 보임. nudge를 ±2 이하로 작게 유지하고 차이는 blade/flower 배치로만 → 한 덩어리 field처럼 읽힘. (pairwise RGB delta 5~10이 적정: 반복 깨지되 동일 biome.)
