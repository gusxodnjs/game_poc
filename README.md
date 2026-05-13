# game_poc — TERRA × BIOSPHERE

> **PoC가 답하는 단 하나의 질문**
> "이 컨셉을 Unity + 픽셀랩으로 만들 수 있는가?"
> 그리고 "산책 → 발견 → 안치" 한 사이클이 진짜 재미있는가?

위치기반 픽셀아트 모바일 게임 PoC. **D+1 ~ D+5 (4인 1주 통합)** 일정으로 가능성 검증에 집중.

## 기술 스택

| 영역 | 도구 |
|------|------|
| 엔진 | Unity 6 LTS (2D) |
| 아트 | 픽셀랩 + Aseprite (16×16 픽셀아트) |
| 지도/위치 | Mapbox Maps SDK for Unity 또는 Niantic Lightship (PoC에서 비교 검증) |
| GPS | Unity `Input.location` |
| 저장 | PlayerPrefs / JSON (PoC 한정) |
| BaaS 후보 | Firebase / NHN 게임베이스 / PlayFab (PoC에서 비교) |
| 플랫폼 | iOS / Android (PoC는 Android APK 우선) |

## 디렉토리

```
.
├── README.md
├── .gitignore           — Unity 6 LTS 표준
├── .editorconfig
├── docs/                — PoC 작업 지시서·콘텐츠 스펙
│   ├── PROJECT_GENESIS_PoC지시서_축소판.docx   (1주 4인 통합 — 최신, 권위)
│   └── TERRA_POC_v7_2주.docx                   (2주 콘텐츠 스펙)
└── data/
    └── species.json     — PoC 수록 6종 데이터
```

Unity 프로젝트 자체(Assets/, ProjectSettings/, Packages/)는 Unity Hub에서 6 LTS로 생성 후 이 폴더에 합쳐 사용.

## 시작하기

### 사전 요건
- Unity Hub + Unity 6 LTS
- iOS Build Support / Android Build Support 모듈
- Aseprite (또는 PixelLab Aseprite plugin)
- gh CLI + github.com 인증 (이슈/PR 작업용)

### 초기 셋업 (예정)
```bash
git clone https://github.com/gusxodnjs/game_poc.git
cd game_poc
# Unity Hub에서 6 LTS · 2D 템플릿 · 이 폴더 위치로 새 프로젝트 생성
# Assets/, ProjectSettings/, Packages/ 가 추가됨 — .gitignore가 처리
```

### 개인 git author (이 레포 한정)
```bash
git config user.email "gusxodnjs@gmail.com"
git config user.name "gusxodnjs"
```

## 일정 (Genesis 지시서)

| 일 | 클라이언트 | 백엔드 | 테스터 | 디자이너 |
|----|------------|--------|--------|----------|
| D+1 | Unity 세팅 + 픽셀랩 import 시도 | BaaS 3종 자료 조사 | 체크리스트 v0 | 포스터 썸네일 3종 |
| D+2 | 픽셀랩 워크플로 동작 확인 | 비교표 1차 | 체크리스트 v1 | 본 시안 선택 + 캐릭터 1종 실측 시작 |
| D+3 | GPS 또는 AR 1개 검증 | Firebase 임시 세팅 공유 | 대기 / 자료조사 | 실측 완료 + 포스터 본격 |
| D+4 | PoC 빌드 1차 → 테스터 | 권고안 작성 | 1차 검증 | 포스터 채색 |
| D+5 | PoC 빌드 최종 + 보고서 | 최종 1페이지 | 최종 검증 + 보고 | 포스터 최종 + 산정서 1p |
| **D+6** | **합동 검토 회의 (90분) — 본 제작 진입 / 1주 연장 / 컨셉 재검토** ||||

## 작업 이슈

| # | 직군 | 제목 |
|---|------|------|
| [#1](https://github.com/gusxodnjs/game_poc/issues/1) | 클라이언트 | Unity + 픽셀랩 PoC 검증 (D+1~D+5) |
| [#2](https://github.com/gusxodnjs/game_poc/issues/2) | 백엔드 | BaaS 비교 + Firebase 임시 세팅 |
| [#3](https://github.com/gusxodnjs/game_poc/issues/3) | 테스터 | PoC 빌드 검증 체크리스트 + 1·2차 검증 |
| [#4](https://github.com/gusxodnjs/game_poc/issues/4) | 디자이너 | 포스터 시안 + 캐릭터 1종 실측 |
| [#5](https://github.com/gusxodnjs/game_poc/issues/5) | 합동 | D+6 검토 회의 |

## PoC에서 의도적으로 제외

| 시스템 | 사유 |
|--------|------|
| 환경 자원 5종, 시그니처 미니게임 | 코어 검증 후 추가 |
| 도감 4단 해금, 어드밴티지/희귀도 | 6종에서 무의미 |
| 온보딩 컷신, 동물 AI 상태머신, 공생 애니메이션 | PoC 범위 외 |
| 사운드/음악, 소셜/친구 | 효과음 2~3개로 대체 |
| 다국어, 정교한 저장, UI 다듬기 | PoC 범위 외 |

자세한 내용: [`docs/`](./docs)
