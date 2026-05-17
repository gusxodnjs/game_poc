---
name: asset-directory-conventions
description: assets/ 폴더 구조와 Unity .meta 파일 처리 규칙
metadata:
  type: project
---

**폴더 구조:**
- `assets/characters/` — 플레이어 캐릭터 스프라이트, 마커 (그림자, 정확도 링)
- `assets/tiles/` — 타일맵 타일
- `assets/world/` — 행성, 월드 오브젝트
- `assets/ui/` — UI 아이콘, 마커
- `assets/AppIcon/` — iOS 앱 아이콘, 스플래시
- `assets/Scenes/` — Unity 씬
- `assets/Scripts/` — C# 스크립트
- `assets/Editor/` — Unity 에디터 확장

**파일명 규칙:**
- `<name>_<W>x<H>.png` (예: `walker_front_64x64.png`, `player_shadow_32x16.png`)
- 애니메이션: `<name>_frame{N}_<W>x<H>.png` + `<name>_sheet_<W>x<H>.png` (가로 시트)

**.meta 파일:**
- Unity는 모든 자산에 짝지어진 `.meta` 파일을 생성한다 (GUID 관리용).
- 새 PNG를 추가하면 Unity 에디터가 자동으로 `.meta`를 만든다 — 스크립트에서 수동 생성 불필요.
- 단, **Unity 에디터를 열어보지 않으면 .meta가 안 생긴다**. PR에서 자산만 추가하고 .meta가 빠지면 Unity가 GUID를 재발급해 참조가 깨질 수 있음.
- PR 머지 전 .meta 동반 여부 확인 필수.

**README:**
- 각 자산 폴더에 `README.md` (예: `assets/characters/README.md`)에 자산 목록, 프롬프트, 생성 메타데이터 기록.
- 새 자산 추가 시 README 업데이트 필수.

관련: [[project-style-guide]] [[pixellab-generation-pattern]]
