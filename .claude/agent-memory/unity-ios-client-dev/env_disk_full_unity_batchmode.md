---
name: env-disk-full-unity-batchmode
description: 이 머신의 디스크가 거의 가득 참 — Unity batchmode가 disk-full IOException으로 실패함
metadata:
  type: project
---

이 개발 머신(/dev/disk3s1s1, 460Gi)은 거의 가득 차 있다. 2026-05-30 기준 container free space ~150MB.

**Why:** 점유의 대부분은 레포가 아니라 `/Users/hyun/Library`(155G), `/Users/hyun/cowork`(40G). game_poc 레포 자체는 ~167M.

**증상:** `Unity -batchmode -executeMethod ...` 실행 시 스크립트 컴파일/IL-post-processing 단계에서 `IOException: Disk full. Path .../Library/Bee/...-inputdata.json` 으로 실패. exit code는 0으로 나올 수 있으나 `[POC]` 로그가 중간에 끊기고 산출물이 불완전/신뢰불가.

**How to apply:** Unity batchmode 셋업/빌드를 실행하기 전에 반드시 `df -h /` 로 free space 확인. 수 GB 미만이면 실행 전 사용자에게 디스크 확보 요청. regenerable `build/ios/`(gitignored, 490M) 삭제만으로는 부족 — OS-update APFS 스냅샷이 즉시 흡수해 free가 회복 안 됨. `tmutil deletelocalsnapshots /` 또는 Library/cowork/Downloads 정리는 파괴적이라 사용자 판단 필요. 절대 손으로 .unity YAML 만들어 우회하지 말 것 — 안 되면 멈추고 보고.

관련: [[project_terra_poc]] (PocBuildPipeline 메뉴 흐름)
