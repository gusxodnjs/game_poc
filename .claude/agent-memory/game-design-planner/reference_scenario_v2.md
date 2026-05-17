---
name: reference-scenario-v2
description: 시나리오 v2 — GPS 시드 시스템 도입판. v1 base 자산 100% 승계 + 자동 이름 풀(24×8) + PlanetIntroScene 명세.
metadata:
  type: reference
---

위치: `/Users/hyun/projects/game_poc/docs/scenario_v2.md`

v1 ([[reference-scenario-v1]]) 의 §1 (행성 3종) / §3 (진입 한 컷) 를 그대로 승계하고, GPS 시드 도입에 따라 다음을 변경/신규:
- §1 신규: GPS 시드 컨셉 도입 카피 ("지금 당신이 서 있는 자리에서, ...")
- §4 변경: 인트로 카피에서 "선택" 의례 제거
- §5 신규: 자동 이름 풀 (형용사 24개 × type별 명사 8개 × 블랙리스트 19개)
- §6 변경: 시작 버튼 카피 "여기로 첫 걸음" → **"여기서 첫 걸음"** (조사 의미 정합)
- §7 변경: 영구성 — deterministic 시드 + `last_seed` 폴백
- §8 신규: PlanetIntroScene 화면 텍스트 구조표 (Unity 명세)

**적용 시점**: PlanetSelect → PlanetIntroScene 마이그레이션 작업 시 본 문서 단일 출처. v1은 base 자산 SSOT로 계속 참조.

본문 부록 A에 v1→v2 변경 매트릭스 포함.

관련: [[project-planets-v1]], [[project-auto-name-pool-v2]], [[reference-scenario-v1]], [[feedback-tone-brand]].
