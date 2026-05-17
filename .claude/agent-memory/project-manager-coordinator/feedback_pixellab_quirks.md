---
name: feedback-pixellab-quirks
description: PixelLab MCP의 알려진 함정 — 다운로드 응답에 JSON 에러 본문이 PNG 파일로 저장될 수 있고, 256px 결과물도 픽셀아트 + 모바일 업스케일 컨텍스트에서 PoC용으로 충분
metadata:
  type: feedback
---

PixelLab MCP를 사용해 자산을 생성·다운로드할 때 두 가지 함정에 주의.

**Why:** 2026-05-17 스플래시 8프레임 작업에서 f01.png(70바이트)가 PNG가 아니라 `{"detail":"Object is still being generated (90% complete). ETA: 23s."}` 라는 JSON 에러 본문이었음. 클라이언트가 polling 중 미완료 응답을 그대로 파일에 저장한 버그. Unity는 의미 있는 에러 없이 이 파일을 무시하므로 빌드 시점까지 발견 안 됨. 또한 동일 작업에서 명세는 1024×1024였으나 256×256으로 생성됐고, 픽셀아트 + iOS 자동 업스케일 컨텍스트에서 사용자가 "256으로 진행" 결정.

**How to apply:**
1. PixelLab 자산 다운로드 직후 항상 다음을 검증: (a) `file <path>`로 PNG 매직바이트 확인 (b) `sips -g pixelWidth -g pixelHeight`로 차원 확인 (c) 파일 크기가 비정상적으로 작으면(<1KB) JSON 에러 의심. 손상 파일은 같은 group의 다른 ID들에서 SHA256 매칭으로 어느 ID가 다운로드 누락인지 역추적 가능.
2. 해상도 협상: 픽셀아트 + 모바일 업스케일 + PoC 단계라면 256으로 충분. 1024 재생성은 90% 정체 이슈 + 비용으로 비용이 큼. 사용자가 픽셀아트 톤 보존을 원할 때 256 유지가 안전한 기본 선택.
3. PNG 파일을 덮어쓰는 작업은 Claude 자동 모드 정책에서 거부될 수 있음 — `cp`로 기존 자산 덮어쓰기는 사용자의 명시적 승인이 필요. 관련: [[project-splash-renewal-2026-05]]
