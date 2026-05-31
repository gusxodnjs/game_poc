---
name: git-case-mismatch-assets-staging
description: 새 에셋은 git status가 소문자 assets/로 보고 — 대문자 Assets/로 git add하면 조용히 누락됨
metadata:
  type: feedback
---

새로 만든 에셋 파일을 `git add Assets/world/tiles/foo.png`(대문자)로 스테이징하면 **조용히 누락**될 수 있다. 커밋은 성공하지만 그 파일이 안 들어감.

**Why:** 레포 기존 트래킹 경로는 대문자 `Assets/`인데, macOS 케이스-인센서티브 FS에서 새로 생성된 untracked 파일을 `git status`는 **소문자 `assets/`**로 보고한다. git pathspec 매칭은 케이스-센서티브라 대문자 인자가 소문자 untracked 엔트리와 안 맞는다. (CLAUDE.md "Folder casing caveat" 참조)

**How to apply:** 에셋 스테이징 전 `git status --short`로 실제 보고된 케이스를 확인하고 **그 케이스 그대로** `git add`. 신규 파일은 보통 소문자 `assets/...`. 스테이징 후 `git diff --cached --name-only`로 git이 저장할 경로(보통 대문자 `Assets/`로 정규화됨)와 기대 파일 수가 맞는지 확인. 커밋 직후 `git show --stat`으로 누락 없는지 재검증. 누락 시 소문자 경로로 add 후 `git commit --amend --no-edit`.

관련: [[asset-verification-checklist]] [[asset-directory-conventions]] [[pixellab-topdown-tileset-corner-wang]]
