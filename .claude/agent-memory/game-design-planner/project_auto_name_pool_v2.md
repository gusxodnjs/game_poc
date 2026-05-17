---
name: project-auto-name-pool-v2
description: 시나리오 v2 자동 행성 이름 풀 — 형용사 24개 × type별 명사 8개 + 블랙리스트 19조합. GPS 시드 deterministic 매핑의 SSOT.
metadata:
  type: project
---

PlanetIntroScene 표시 이름은 GPS 1km 셀 hash로 결정 — "{형용사} {type별 명사}". deterministic.

**Why:** 사용자 + PM 합의 (2026-05-17) — "GPS로 행성 자동 결정, 사용자 선택 제거". 동네별 변주를 주되 base 3종(volcano/ice/desert)의 톤·색조·종 풀은 [[project-planets-v1]] 그대로 유지. 변주는 (1) 자동 이름 + (2) hue ±15° 두 채널만 허용.

**How to apply:** PlanetIntroScene 구현 / 자동 이름 생성 코드 / QA 시연 스크린샷 작업 시 본 메모리를 SSOT로 참조. 풀 수정 시 [[reference-scenario-v2]] §5 동기화 필수.

## 알고리즘

```
seed_hash = SHA1(f"{lat_floor_001:.2f},{lon_floor_001:.2f}")
type_idx  = seed_hash[0] % 3                  → [volcano, ice, desert]
adj_idx   = int(seed_hash[1:3], 16) % 24
noun_idx  = int(seed_hash[3:5], 16) % 8
hue_off   = (int(seed_hash[5:7], 16) % 31) - 15   → -15° ~ +15°
if (adj_idx, noun_idx) ∈ BLACKLIST[type]:
    noun_idx = (noun_idx + 1) % 8
```

## 형용사 풀 (24)

잠든, 고요한, 메마른, 얼어붙은, 식어버린, 잔잔한, 흩어진, 텅 빈, 잠잠한, 굳어진, 가라앉은, 멈춰선, 낡은, 바랜, 희미한, 가물거리는, 옅은, 무거운, 느린, 깊은, 외딴, 한적한, 비어버린, 오래된

## 명사 풀 (type별 8)

| type | 명사 8 |
|------|-------|
| volcano | 화산, 용암 들녘, 잿빛 언덕, 식은 봉우리, 검은 바위 자락, 재의 평원, 분화구, 굳은 협곡 |
| ice | 평원, 얼음 늪, 회청 들녘, 빙하 자락, 서리 언덕, 얼어붙은 강, 새벽 벌판, 흰 골짜기 |
| desert | 들녘, 모래 언덕, 황토 평원, 마른 바람골, 갈라진 땅, 빈 사구, 햇빛 언덕, 메마른 협곡 |

## 블랙리스트 (19조합 — 의미 충돌)

- volcano: (잔잔한, 용암 들녘), (얼어붙은, 화산), (얼어붙은, 분화구), (얼어붙은, 용암 들녘), (얼어붙은, 식은 봉우리), (식어버린, 식은 봉우리), (메마른, 용암 들녘)
- ice: (메마른, 얼음 늪), (메마른, 얼어붙은 강), (식어버린, 빙하 자락), (식어버린, 얼음 늪), (식어버린, 얼어붙은 강), (잠든, 얼어붙은 강)
- desert: (얼어붙은, 햇빛 언덕), (얼어붙은, 마른 바람골), (얼어붙은, 모래 언덕), (얼어붙은, 빈 사구), (잔잔한, 갈라진 땅), (식어버린, 햇빛 언덕)

총 576조합 중 19개 제거 (3.3%).

## 시연 화이트리스트 (type별 5 — 톤 정합도 ★5)

- volcano: 잠든 화산 / 고요한 잿빛 언덕 / 식어버린 분화구 / 굳어진 검은 바위 자락 / 오래된 재의 평원
- ice: 고요한 평원 / 잠든 빙하 자락 / 희미한 새벽 벌판 / 깊은 흰 골짜기 / 오래된 서리 언덕
- desert: 메마른 들녘 / 바랜 황토 평원 / 한적한 모래 언덕 / 오래된 갈라진 땅 / 느린 마른 바람골

QA 시연 빌드/홍보 스크린샷에는 이 15개 조합을 우선 노출.

## 톤 가드

명사 풀에 "행성"·"세계"·"별" 어휘 없음 — SF 잔향 회피 ([[feedback-tone-brand]]).
형용사는 원시·정적·시간 어휘만. "빛나는"·"신비한"·"마법의" 등 판타지/SF 잔향 금지.

관련: [[project-planets-v1]], [[reference-scenario-v2]], [[feedback-tone-brand]].
