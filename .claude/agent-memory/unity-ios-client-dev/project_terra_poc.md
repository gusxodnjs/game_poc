---
name: project-terra-poc
description: TERRA × BIOSPHERE PoC 의 Unity 6 + iOS 구성·아키텍처·빌드 파이프라인 핵심 사실
metadata:
  type: project
---

TERRA × BIOSPHERE PoC — 산책 → 발견 → 안치 (walk → discover → enshrine) 루프 검증용 한국어 위치 기반 픽셀아트 모바일 게임.

**Why:** 4인 1주 schedule, 외부 SDK 도입 없이 작은 코드베이스로 핵심 가치 검증. iOS 우선, Android 보조.

**How to apply:** 변경/제안 시 다음 사실 기반으로 판단.

- Unity 6 LTS `6000.0.75f1`. Test framework 번들 버전 1.6.0
- Bundle ID `com.gusxodnjs.terrapoc`, display name `작은정복자들` (PocPostProcessBuild 가 Info.plist 패치)
- iOS 13+, iPhone-only, IL2CPP, ARM64, Personal Team 7일 한도
- 빌드 진입점: `TERRA PoC/` 메뉴 (`assets/Editor/PocBuildPipeline.cs`). 1=Setup Hello, 1b=Setup Splash, 2=iOS Player Settings, 3=Build iOS, 9=Do All
- 씬 순서: Splash → Hello. SplashScene 은 8프레임 행성 진화 애니, HelloScene 이 본 게임 셸
- UI 규약: **IMGUI 전부**, Canvas/UI Toolkit/Prefab 없음 — `OnGUI` 로만 그린다
- 폴더 casing: 디스크에는 `assets/` (소문자), Unity 는 `Assets/` 로 본다. macOS case-insensitive 라 동작 OK. CI 추가 시 `Assets/` 로 정규화 필요
- 모든 .cs 옆에 .meta 짝 필요. Unity 미실행 상태로 .cs 만 추가하면 Unity 가 자동 생성하지만, git tracking 일관성 위해 같이 작성하는 게 안전
- 자산은 `scripts/gen_*.py` 가 PixelLab API 호출해 만든다. 32×32 미만은 32×32 생성 후 nearest-neighbor 축소

관련: [[map-shell-architecture]] [[ios-build-pipeline]]
