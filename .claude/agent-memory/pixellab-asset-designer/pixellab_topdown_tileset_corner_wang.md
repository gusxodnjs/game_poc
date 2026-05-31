---
name: pixellab-topdown-tileset-corner-wang
description: create_topdown_tileset는 4-EDGE가 아니라 4-CORNER Wang 타일을 만든다 — edge-mask 렌더러와 1:1 매핑 불가
metadata:
  type: project
---

PixelLab MCP `create_topdown_tileset`는 **4-CORNER Wang** 타일셋을 반환한다 (NW/NE/SW/SE 각 코너가 upper=feature / lower=base). 16 코너 조합 = 16 타일 (transition_size<1.0; =1.0이면 23타일).

**4-EDGE 비트마스크(N=1,E=2,S=4,W=8) 렌더러와는 스킴이 달라 1:1 변환 불가.**
- edge-mask: "feature가 N/E/S/W 변을 넘어가는가" → inner-corner 표현 불가
- corner-Wang: "코너 NW/NE/SW/SE가 feature인가" → inner/outer corner 모두 표현, 무손실
- edge→corner를 "양쪽 코너 모두 feature일 때 변이 덮임" 규칙으로 강제하면 mask 0/1/2/4/5/8/10(단일 변·대각 반대변·고립)이 전부 all-grass 타일(cornerIndex 0)로 collapse됨 → 7개 셀이 깨짐. union 규칙은 반대로 cornerIndex 15로 collapse.

**결론(권장):** corner-Wang 16타일을 **cornerIndex 순서**(`col=cidx%4, row=cidx//4`, `cidx=NW*8+NE*4+SW*2+SE*1`)로 그대로 시트화하고 렌더러를 **4-corner mask**로 구동. 이게 유일한 무손실 통합. edge-mask 유지가 필수면 lossy 폴백표를 문서화.

**메타데이터 구조:** `tileset_data.tiles[]` 각 항목에 `corners{NW,NE,SW,SE}` + `bounding_box{x,y,width,height}`. bbox로 크롭해서 재배치.

**다운로드:** `https://api.pixellab.ai/mcp/tilesets/{id}/image` (302 → `curl -L` 필수), `/metadata` (JSON). Bearer = PIXELLAB_API_KEY.

**연결된 타일셋:** `lower_base_tile_id`로 공통 베이스 공유 시 그 베이스가 완료될 때까지 후속 타일셋은 `status: waiting`. base 먼저 끝나야 나머지가 병렬 진행. 5개 직렬화되면 총 생성시간 김(각 100~440s, ETA 추정치 노이즈 큼).

#52 Task1 적용 사례: path/road/water/forest/building 5종, 공통 grass 베이스 `14da0cce…`. road는 asphalt 프롬프트인데 gray cobble/brick로 나옴(수용), water는 transition_size 0.5로 넓은 shoreline.

관련: [[pixellab-generation-pattern]] [[pixellab-uniform-terrain-quirk]] [[asset-verification-checklist]] [[asset-directory-conventions]] [[git-case-mismatch-assets-staging]]
