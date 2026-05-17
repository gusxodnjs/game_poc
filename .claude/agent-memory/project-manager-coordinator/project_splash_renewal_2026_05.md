---
name: project-splash-renewal-2026-05
description: 2026-05-17 스플래시 화면 리뉴얼 — A안(황폐→푸른 지구 진화) 8프레임 + 명시적 시작 버튼, GD 카피 결정사항 포함
metadata:
  type: project
---

스플래시 화면 리뉴얼 작업 (2026-05-17). 컨셉: A안 "황폐 행성에서 푸른 지구로의 진화" 8프레임(6fps, Loop) + 자동 전환 제거 + 명시적 시작 버튼.

**Why:** 사용자가 직전 PM(aa71952efb29883b9)에게 위임한 작업 중 PM 교체. 환경 회복·산책 기반 종 발견 게임의 첫인상으로 정복/지배보다 회복/돌봄 톤을 강조하기 위함. 직전 커밋(ed85fae, 19c08ee)에서 검은 화면 트라우마 있음 → 동적 텍스처 생성 금지가 안전 가이드라인.

**How to apply:**
- 자산 경로: `assets/AppIcon/splash_anim_v1_planet_evolve_256_f00.png ~ f07.png` (256×256, .meta는 Unity Editor 진입 시 자동 생성)
- 핵심 스크립트: `assets/Scripts/SplashScreen.cs` (IMGUI, Canvas 비사용), 빌드 파이프라인 `assets/Editor/PocBuildPipeline.cs`의 `SetupSplashScene` 메뉴(`TERRA PoC/1b. Setup Splash Scene`)가 8프레임을 자동 로드.
- 카피 결정사항 (GD 검토 결과, 2026-05-17):
  - 타이틀: `작은정복자들` (브랜드명, 띄어쓰기 없음 유지)
  - 서브카피: `걸음마다, 자라나는 세계` (직전 임시값 "당신의 한 걸음이 행성을 푸르게"에서 변경 — 더 간결, 산책 메커닉과 직결, 운율 강함)
  - 시작 버튼: `첫 걸음 떼기` (서브카피와 운율 통일)
- 폰트: 1차는 기본 폰트 유지 (Pretendard 도입 보류)
- 버전: 우하단 safe area에 `v <Application.version>` 표시 (자동 페이드 효과 포함)
- 미해결: f01.png가 손상(JSON 에러 본문)된 상태로 커밋되었고, Claude 자동 모드 정책상 덮어쓰기가 거부되어 PM이 사용자에게 명시 승인을 요청해야 함. 정상 PNG는 `/tmp/splash_redl/0bf1b4d2-eaed-4dfb-9f5d-de5c158089ff.png`에 준비됨. 관련: [[feedback-pixellab-quirks]]
