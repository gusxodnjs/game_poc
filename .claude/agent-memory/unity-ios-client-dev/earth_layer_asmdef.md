---
name: earth-layer-asmdef
description: Earth 산책 레이어(#52) 스크립트는 Assembly-CSharp가 아니라 자체 Earth.asmdef를 쓴다 — Unity 6 테스트 참조 제약 때문
metadata:
  type: project
---

지구 산책 레이어(Pikmin 타일맵, issue #52) 코드는 `Assets/Scripts/Earth/`에 있고, 다른 게임 스크립트(Assembly-CSharp)와 달리 **자체 `Earth.asmdef`** 를 가진다.

**Why:** Task 1 원래 계획은 "Earth 스크립트는 asmdef 없이 Assembly-CSharp에 두고 테스트 asmdef가 Assembly-CSharp를 참조" 였으나, Unity 6에서 test asmdef는 predefined `Assembly-CSharp`를 이름으로 참조할 수 없다(컴파일 시 타입 미해결 CS0103). 그래서 Earth 스크립트에 `Earth.asmdef`(`autoReferenced: true`, `includePlatforms: []` = 전 플랫폼)를 부여하고, `EarthTests.asmdef`는 `"Earth"`를 참조하도록 바꿔 통과시켰다.

**How to apply:**
- 새 Earth 게임 스크립트는 `Assets/Scripts/Earth/` 아래 두면 자동으로 `Earth` 어셈블리에 들어간다.
- `Earth.asmdef`가 `autoReferenced: true`라서 Assembly-CSharp 쪽 코드(PocBuildPipeline, 씬 와이어링 등)는 별도 참조 없이 Earth 타입을 그대로 쓸 수 있다.
- Earth 코드가 다른 게임 스크립트(Assembly-CSharp) 타입을 역참조해야 하면, autoReferenced로는 안 되고 그 타입들도 asmdef화하거나 Earth.asmdef references에 추가해야 한다 — 단방향 의존(Earth는 자립적 순수 유틸)을 유지하는 게 좋다.
- EditMode 테스트는 `Assets/Tests/Earth/EarthTests.asmdef` (Editor 전용 + `defineConstraints: ["UNITY_INCLUDE_TESTS"]`)로, 플레이어 빌드에서 제외됨을 batchmode 컴파일로 검증했다.

관련: [[project_terra_poc]], [[map_shell_architecture]]
