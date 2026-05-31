---
name: flat-autotile-mask-composite-recipe
description: 절벽 없는 평지 오토타일은 create_topdown_tileset 대신 평탄 텍스처+코너마스크 합성으로 만든다
metadata:
  type: feedback
---

오토타일 transition 시트(`*_auto_128.png`)는 `create_topdown_tileset`로 만들지 말 것. 그 도구는 각 feature를 **높은 plateau + 어두운 drop-shadow/ledge**로 렌더해서, 흙길이 grass로 떨어지는 절벽(mesa)처럼 보인다. issue #52 배포 피드백에서 사용자가 "절벽 말고 포켓몬처럼 같은 평면 위 흙 패치"를 요구.

**Why:** create_topdown_tileset은 4-corner Wang이면서도 elevation/cliff를 baking함([[pixellab-topdown-tileset-corner-wang]] [[pixellab-road-asphalt-limitation]]와 같은 맥락의 한계). 마스크 합성은 구조적으로 절벽이 생길 수 없음 — grass와 feature가 같은 z 평면에 놓이고 경계만 feather됨.

**How to apply (`scripts/make_flat_autotiles.py`):**
1. feature마다 PixelLab pixflux로 **평탄 32x32 interior 텍스처** 1장 생성. 프롬프트에 "flat top-down, uniform flat lighting, no outline, no shadow, no elevation, no cliff, seamless tileable". `no_background:false`(불투명 ground), guidance 9.
2. **방어적 flatten**: 픽셀을 텍스처 평균으로 lerp. 어두운(mean_lum-55 이하) 픽셀 = baked outline/shadow는 k=0.85로 강하게 당겨 검은 ledge 제거, 나머지는 k=0.45 대비 압축. 그래도 텍스처감은 남김.
3. offset-and-heal로 seamless화([[seamless-tile-offset-heal-recipe]]).
4. **코너 마스크 합성** (Layout A 고정, 렌더러 의존): ci=NW*8+NE*4+SW*2+SE, col=ci%4 row=ci//4. 2x2 grayscale(NW,NE/SW,SE) → BILINEAR 32x32 → smoothstep LUT `0 if v<110 else 255 if v>145 else feather`. `Image.composite(feat, grass, mask)`. grass base는 기존 `Assets/world/tiles/grass_v0_32.png`(field와 일치).
5. cell(0,0)=all grass(마스크 전부 0), cell(3,3)=solid feature. 검증: cell00 평균 == grass 평균 정확히 일치해야 함.

**Montage 검증 필수:** 6x6 field 중앙 3x3 vertex를 feature로 마킹, 셀별 4코너로 ci 선택해 렌더러처럼 조립 → /tmp/montage_*.png. "절벽 사라졌나?"를 사람 눈으로 확인. path/forest/road/water 모두 둥근 soft border, 같은 평면 dirt/grass 경계 = 통과.

**잔여 아티팩트(허용):** offset-heal seam이 타일 중앙에 옅은 band로, 패치 안쪽 테두리에 옅은 halo ring으로 남음. 절벽 아님, 게임 스케일에서 미미. 더 줄이려면 heal blur 약화. pure PIL, repo numpy 없음.
