---
name: env-unity-editor-lock
description: batchmode가 "another Unity instance is running"으로 죽을 수 있음 — Editor 종료 금지, 코드만 커밋 후 DONE_WITH_CONCERNS
metadata:
  type: project
---

batchmode Unity 실행이 "It looks like another Unity instance is running with this project open"(exit 134)으로 죽는 경우가 있다. 사용자가 Editor를 열어둔 채 작업 중일 수 있음.

**Why:** Unity는 한 프로젝트를 동시에 두 인스턴스가 열 수 없음. 사용자 Editor를 강제 종료하면 미저장 작업이 날아갈 위험.

**How to apply:** 이 에러가 나면 Editor를 절대 kill 하지 말 것. 코드 편집 자체는 완료/커밋/푸시 가능하므로 소스 파일만 커밋하고, 씬 재생성(SetupHelloScene)·iOS 빌드처럼 batchmode가 필요한 산출물은 보류로 보고(DONE_WITH_CONCERNS). 씬 재생성이 막히면 grass 변형 .meta(Unity가 import 시 생성)와 HelloScene.unity 재생성본도 함께 보류되므로 stale 상태를 커밋하지 말 것. 디스크-풀 제약은 [[env_disk_full_unity_batchmode]] 참고.
