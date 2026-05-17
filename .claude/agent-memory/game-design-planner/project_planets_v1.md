---
name: project-planets-v1
description: 작은정복자들 PoC v7.2 — 행성 3종(volcano/ice/desert, 원시 환경) 확정안과 시작 종 풀 매핑. PlanetSelect 화면 데이터의 원본.
metadata:
  type: project
---

PoC v7.2에서 게임 시작 직후 PlanetSelect 화면을 통과한다. 행성 3종은 **모두 원시 단계의 서로 다른 형태**(불/얼음/모래)이며, 산책으로 점진적 회복해 가는 메타포의 출발점.

**Why:** 사용자 피드백(2026-05-17) — "초기 행성 모습은 원시행성 형태여야 해. 해변이 있고 도시가 있고 이러면 안 돼." 발달된 환경(도시/해변/풍성한 숲)이 시작점이면 회복할 게 없어 게임 핵심 메타포(스플래시 *황폐 → 푸른 지구*와 일관)가 무너짐. 직전 forest/city/coast 안은 **폐기**.

**How to apply:** PlanetSelect 관련 후속 작업(pixellab 카드 시안 / Unity 데이터 구조 / Shader Tint)은 아래 표를 단일 출처(SSOT)로 참조. 변경 시 [[scenario-v1-source]] 본문 §1 동기화 필수. 카드 시안 작업 시 **발달된 환경 금지** 가드: 도시 건물 / 모래사장 야자수 / 풍성한 숲 모두 NO. 회색·검은·흰·적갈·황토의 황량함이 기본 톤.

| planet_id | 표시 이름 | tint_hex | 시작 종 풀 (4) | 보너스 종 | 시그니처 분위기 |
|-----------|----------|---------|---------------|----------|---------------|
| `volcano` | 잠든 화산 | `#5C3B33` | dandelion, foxtail_grass, white_clover, ladybug | white_clover | 검은 바위, 흰 김, 잔열, 정적 |
| `ice` | 얼어붙은 평원 | `#6E8AA0` | dandelion, foxtail_grass, honeybee, ladybug | honeybee | 얼음, 정적, 회청, 긴 바람 |
| `desert` | 메마른 들녘 | `#B89968` | dandelion, foxtail_grass, cherry_blossom, ladybug | cherry_blossom | 황토, 갈라진 땅, 햇빛, 마른 바람 |

**공통 등장 종**: 민들레 + 강아지풀 + 무당벌레 (3행성 모두, 원시 환경 첫 정착자 안전망).
**행성 시그니처**: 토끼풀(화산) / 꿀벌(얼음) / 벚꽃(사막).
**메타포 분리 원칙**: 시작 종 풀은 "산책지에서 발견할 종"의 슬롯이지 "행성 거주 생물 목록"이 아님. lore에 종 이름 미등장.

표시 이름은 "행성" 어휘를 제거 (SF 잔향 회피, 자연 명사 + 형용사 조합).

본문/카피 원본: [[scenario-v1-source]].
