---
name: scenario-v1-source
description: PlanetSelect 시나리오/카피 단일 출처 문서 위치. 모든 후속 라운드는 이 파일을 SSOT로 참조.
metadata:
  type: reference
---

PlanetSelect 화면의 모든 텍스트 자산(인트로, 행성별 진입 단락, 버튼 카피)과 행성 3종 확정안의 원본 문서:

`/Users/hyun/projects/game_poc/docs/scenario_v1.md`

**How to apply:**
- Unity 클라이언트 라운드: §1 표 → `data/planets.json` 매핑, §2/§3 텍스트 → IMGUI Label 리터럴, §4 → 버튼 텍스트.
- pixellab 아트 라운드: §1의 색조 톤 + 분위기 키워드를 카드 시안 프롬프트에 그대로 주입.
- 카피 수정 요청이 들어오면 본 문서를 갱신한 뒤 [[project-planets-v1]] 동기화 여부를 점검.

관련: [[project-planets-v1]], [[feedback-tone-brand]], [[project-poc-scope]].
