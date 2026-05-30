---
name: map-object-tree-recipe
description: create_map_object 으로 나무/덤불/그루터기 등 transparent 오브젝트 스프라이트 만드는 워크플로
metadata:
  type: feedback
---

`create_map_object` (basic mode, no background_image) 가 단독 데코 오브젝트(나무/덤불/그루터기)에 가장 적합. PixelLab create_topdown_tileset 의 절벽/타일 제약([[pixellab-topdown-tileset-corner-wang]])을 피해감.

**Why:** 지도가 밋밋하다는 #52 — Pokemon overworld 풍 conical pine 트리 오브젝트가 필요했고, 타일셋이 아니라 개별 transparent 오브젝트여야 렌더러가 base 를 타일에 꽂을 수 있음.

**How to apply:**
- 트리는 `view: "low top-down"` 가 원뿔형으로 잘 읽힘. 덤불/그루터기는 `high top-down`.
- `outline: single color outline`, `shading: basic shading` 가 GBA-Pokemon 톤과 forest 팔레트(canopy ~49,72,60 / grass ~140,198,60)에 맞음.
- 반환은 **이미 transparent RGBA** (corners alpha 0) — 추가 chroma-key 불필요. (no_background quirk 와 달리 map-object 는 깨끗했음.)
- get_map_object preview 는 content bbox 크기(예: 48x48)를 보고하지만 **download 엔드포인트는 요청한 full canvas(48x64)** 를 반환함. download 를 신뢰할 것.
- 후처리 `scripts/make_tree_objects.py`: alpha<16 클리핑 + bottom-center 재배치(트렁크가 캔버스 하단에 닿게). canvas 보다 크면만 NEAREST 다운스케일, 업스케일 금지.
- ETA 편차 큼: 같은 배치에서 트리 2개는 ~60s, 32x32 2개는 ~460s 걸렸음. 폴링 루프 필수.
- **dirt-mound quirk**: map-object 는 오브젝트 밑에 작은 갈색 흙더미 base 를 자주 붙임. 트리/그루터기엔 자연스럽지만 꽃/풀에는 grass 위 흙점으로 거슬림. PIL 휴리스틱(r>g>=b, r-b>25, r<200, g<=110)으로 갈색만 골라 녹색 stem(60,120,40)으로 recolor — `scripts/make_grass_detail.py` degreen_dirt. 풀 tuft 는 어두운 녹색이 갈색으로 오검출될 수 있어 끄고, 꽃에만 적용.
- **작은 데코는 16px 로 다운스케일 + 의도적으로 작게**: 32px 생성 → content bbox crop → target_h(9~12px)로 NEAREST 축소 → bottom-anchor. dense scatter 용은 alpha-0 fraction 0.6~0.8 유지(타일 가득 X). 검증 composite: grass tile 타일링 후 ~20px step jitter scatter, 3x NEAREST 업스케일로 eyeball.
- **prompt 팁**: tuft_c 1차에서 "minimal/sparse" 만 주면 회색-청록(#2c3b3d) 으로 빠짐. "vivid leaf green, bright green blades, no flowers" 명시하면 깨끗한 녹색(#a8d129) 나옴.
- 산출물: `Assets/world/objects/` (신규 dir). 스테이징은 [[git-case-mismatch-assets-staging]] 따라 git 이 보고하는 lowercase 경로로 add.
