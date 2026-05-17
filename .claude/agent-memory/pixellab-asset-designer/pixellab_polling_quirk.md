---
name: pixellab-polling-quirk
description: PixelLab MCP가 폴링/에러 응답 본문을 PNG 자리에 저장하는 버그 — 70바이트 PNG는 의심 신호
metadata:
  type: project
---

PixelLab MCP 툴 (`mcp__pixellab__*`)이 비동기 잡 폴링 중에 폴링 응답이나 에러 본문을 PNG 파일 자리에 저장해 버린 사고가 있었음. 직전 PR에서 `f01.png` (70바이트짜리)가 이렇게 생성되어 추적 사고가 났음.

**의심 신호:**
- 자산 PNG가 70~200B 정도로 비정상적으로 작음
- `file <path>` 결과가 `PNG image data`가 아님 (예: `ASCII text`, `JSON data`)
- PNG 시그니처 `\x89PNG\r\n\x1a\n` 가 없음

**해결책 (이 프로젝트):**
- REST API 직접 호출 ([[pixellab-generation-pattern]]) — MCP 우회
- 생성 직후 **반드시** PNG 시그니처 검증 (스크립트 내부) + `file` 명령 검증 ([[asset-verification-checklist]])
- 파일 크기 < 1KB 시 경고 (단, 그림자 같은 단색/평탄 자산은 정상적으로 작을 수 있음 — `file` 검증으로 구분)

**예시 검증 코드:**
```python
PNG_SIG = b"\x89PNG\r\n\x1a\n"
if not png_bytes.startswith(PNG_SIG):
    raise RuntimeError(f"invalid PNG signature (len={len(png_bytes)})")
```

**Why:** 한 번 사고가 나면 자산 파이프라인 전체 검토가 필요해서 비용이 큼. 검증을 매번 자동화해야 함.

**How to apply:** 모든 PixelLab 생성 잡에 PNG 시그니처 체크 + 사후 `file` 명령 검증. 작은 파일 (< 1KB)이 나오면 시각적으로 한 번 더 확인.

관련: [[pixellab-generation-pattern]] [[asset-verification-checklist]]
