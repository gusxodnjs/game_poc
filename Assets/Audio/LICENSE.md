# Assets/Audio — License

이 디렉토리의 모든 오디오 자산은 **CC0 1.0 Universal (Public Domain Dedication)** 으로 배포됩니다.

> CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/

요약:
- 무제한 사용 (상업/비상업 포함)
- 수정/2차 창작 자유
- 출처 표시 의무 없음
- 보증 없음 (as-is)

---

## 트랙 목록

### `splash_bgm_v1.wav`

| 항목 | 값 |
|---|---|
| 라이선스 | CC0 1.0 Universal |
| 생성 방식 | `scripts/gen_splash_bgm.py` — Python stdlib (`wave`, `struct`, `math`, `random`) 만으로 절차적 합성 |
| 저작자 | TERRA PoC 팀 (self-generated) |
| 포맷 | 44.1kHz / 모노 / 16-bit PCM WAV |
| 길이 | 8.0초 |
| 용도 | `SplashScene.unity` 의 빅뱅 시퀀스 BGM (`docs/splash_v2_bigbang.md` 타이밍 동기) |

#### 구간 구조

| 구간 (ms) | 음향 | 시퀀스 매핑 |
|---|---|---|
| 0 – 1500 | 110Hz + 220Hz 사인 드론, 0.0 → 0.15 페이드인 | 무 → 작은 점 (f00–f01) |
| 1500 – 1700 | 220Hz → 880Hz 글리산도 sweep | 임계 광원 (f02) |
| 1700 – 2200 | 화이트 노이즈 burst + 55Hz boom | 폭발 (f03–f05) |
| 2200 – 6500 | 110/165/220Hz 화음 패드 (detune ±2Hz, LFO 0.3Hz) | 응집 → 원시 행성 (f06–f10) |
| 6500 – 8000 | 110Hz 잔향 페이드아웃 (0.1 → 0.0) | 최종 hold 위 페이드 (f11) |

#### 외부 다운로드 회피 사유

- **라이선스 안전**: 자체 생성물이므로 출처 의무·재라이선스 위험 없음.
- **타이밍 정확**: 빅뱅 시퀀스(`docs/splash_v2_bigbang.md`)와 ms 단위 동기.
- **재현 가능**: `random.seed` 고정 — 동일 입력 → 동일 출력.
- **PoC 의존성 최소화**: stdlib 만 사용, numpy 등 신규 패키지 미추가.

#### 재생성 / 변경 정책

스크립트(`scripts/gen_splash_bgm.py`)는 항상 보존한다. 음악 톤 변경 요청 시 스크립트의 파라미터(주파수/엔벨로프/페이드)만 수정 후 재실행 → 새 PR 로 트랙만 교체.
