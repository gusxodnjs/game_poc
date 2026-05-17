---
name: feedback-no-dynamic-textures
description: 동적 텍스처 생성 금지 — 디스크 PNG → LoadImage 경로만 사용
metadata:
  type: feedback
---

`new Texture2D(w, h, format)` + `SetPixels()` 같은 동적 텍스처 채우기 패턴 금지.
PNG 바이트가 있으면 디스크에 저장 후 `Texture2D.LoadImage(bytes)` 경유로만 만든다.

**Why:** 이전 PR (`fix/splash-blackscreen`) 에서 동적으로 생성한 텍스처가 iOS 빌드에서 검은 화면을 유발한 트라우마가 있다. 그 후 모든 splash/asset 파이프라인이 디스크 PNG 기반으로 정리되었고, 사용자가 명시적으로 이 정책을 유지해 달라고 요청.

**How to apply:**
- 텍스처가 필요하면 (1) 디자이너 자산을 `Assets/` 에 import 하거나 (2) 런타임 다운로드를 `Application.persistentDataPath` 에 PNG 로 저장한 뒤 `LoadImage` 로 디코딩한다
- `UnityWebRequestTexture.GetTexture` 대신 일반 `UnityWebRequest.Get` 으로 PNG 바이트를 받아 디스크 거쳐 LoadImage. (GetTexture 의 자동 디코딩이 동적 생성 분류에 가까움)
- placeholder/단색 텍스처가 필요하면 사전 인코딩한 하드코딩 PNG byte[] 를 디스크에 한 번 쓰고 LoadImage. 절대 `Color[]` + `SetPixels` 안 됨
- 예외: Sprite.Create 는 unreadable 텍스처도 받으므로 LoadImage 후 sprite 만드는 건 안전
