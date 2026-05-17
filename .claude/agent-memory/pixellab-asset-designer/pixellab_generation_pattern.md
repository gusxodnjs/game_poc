---
name: pixellab-generation-pattern
description: 이 프로젝트는 PixelLab MCP가 아니라 pixflux REST 엔드포인트 직접 호출 Python 스크립트로 자산을 생성한다
metadata:
  type: project
---

이 프로젝트는 PixelLab MCP 툴이 아니라 **Python 스크립트로 PixelLab REST API를 직접 호출**한다.

**엔드포인트:** `POST https://api.pixellab.ai/v1/generate-image-pixflux`
- 정적 이미지: `/v1/generate-image-pixflux`
- 애니메이션: `/v1/animate-with-text`

**스크립트 패턴 (`scripts/gen_*.py`):**
1. `.env.local`에서 `PIXELLAB_API_KEY=` 읽기 (helper: `load_api_key()`)
2. urllib.request로 POST. payload: `{"description": prompt, "image_size": {"width": W, "height": H}, "no_background": True, "text_guidance_scale": 10.0}`
3. 응답 JSON의 `image.base64`를 base64 디코드 → PNG bytes
4. **항상 PNG 시그니처 검증**: `bytes[:8] == b"\x89PNG\r\n\x1a\n"` ([[pixellab-polling-quirk]] 참조)
5. 결과를 `scripts/gen_<name>_result.json`에 요약 저장 (호출 수, total_usd, attempts 로그)
6. 1회 실패 시 2회 재시도 (강화 프롬프트 사용 가능)

**참고 스크립트:**
- `scripts/gen_human_walker.py` (정적 캐릭터, retry pattern)
- `scripts/gen_human_walker_anim.py` (애니메이션)
- `scripts/gen_player_markers.py` (마커, 다운스케일 포함)
- `scripts/gen_assets.py` (배치 잡 리스트)

**Why:** 직접 REST 호출이 MCP보다 안정적이고 ([[pixellab-polling-quirk]] 회피), `usage.usd` 비용 추적과 attempt 로깅을 통제할 수 있음.

**How to apply:** 새 자산 생성 요청이 오면 기존 `gen_*.py` 중 가장 가까운 케이스를 복사해서 시작. MCP `mcp__pixellab__*` 툴은 이 프로젝트에서 사용하지 않음.

관련: [[pixellab-min-canvas-quirk]] [[asset-verification-checklist]]
