---
name: map-object-house-recipe
description: create_map_object 으로 OSM 건물용 코지 하우스/오두막 스프라이트 만드는 워크플로 (#52)
metadata:
  type: feedback
---

`create_map_object` (basic mode, 64x64) 가 OSM 건물 렌더링용 코지 하우스 스프라이트에도 잘 맞음. [[map-object-tree-recipe]] 의 트리 패턴과 동일 계열.

**Why:** #52 Earth 타일맵에서 OSM building 을 밋밋한 회색 타일이 아니라 Pokemon-overworld 풍 빨간 박공지붕 통나무 오두막으로 렌더하려고 함.

**How to apply:**
- `view: "low top-down"`, `outline: single color outline`, `shading: basic shading`, `detail: medium detail` 가 GBA-Pokemon 톤에 맞고 트리 오브젝트와 톤 일치.
- prompt: "cute small log cabin house, warm red gable roof, wooden plank log walls, a wooden front door, a small square window, cozy storybook RPG overworld building, Pokemon GBA overworld style". 빨강 지붕=house_a, 주황-브라운 지붕=house_b 변형.
- 64x64 요청하면 download 가 깨끗한 transparent RGBA(코너 alpha 0) 반환 — chroma-key 불필요. content bbox 는 보통 ~52-58px.
- map-object 가 하단에 작은 grass tuft / 덤불 base 를 자연스럽게 붙여줌 (트리의 dirt-mound quirk 와 동일 성향이지만 하우스엔 오히려 코지함).
- 후처리 `scripts/make_house_objects.py`: alpha<16 클리핑 → content crop → 가로 center + 하단 bottom-anchor (front wall 이 캔버스 바닥에 닿게, 지붕은 위로). canvas 초과 시만 NEAREST 다운스케일.
- 검증: 코너 alpha 0, bbox 가 y=64 까지 닿는지(bottom-anchor), grass(120,180,70) composite 6x NEAREST 업스케일로 eyeball — white box/halo 없어야 함.
- 스테이징은 [[git-case-mismatch-assets-staging]]: git 이 lowercase `assets/` 로 보고하지만 add 후 git 이 `Assets/` 로 canonicalize.
- 1486 generations 중 하우스 2개 ~60s. 폴링 루프 필수.
