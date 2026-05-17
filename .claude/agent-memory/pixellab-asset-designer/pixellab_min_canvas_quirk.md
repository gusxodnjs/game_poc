---
name: pixellab-min-canvas-quirk
description: PixelLab pixflux는 32x32 미만 정확히 생성 안 됨 — 32x32로 생성 후 PIL nearest로 다운스케일
metadata:
  type: project
---

PixelLab pixflux 엔드포인트의 **최소 권장 캔버스는 32×32**. 그보다 작은 사이즈 (예: 16×16) 또는 비정사각형 작은 사이즈 (예: 32×16)를 직접 요청하면 결과가 깨지거나 인식 불가.

**해결책:** 32×32 이상으로 생성 → PIL `Image.resize(target, Image.Resampling.NEAREST)` 다운스케일 또는 crop.

**Why:** `assets/ui/light_spot_16x16.png` 케이스 + PM 가이드. `scripts/gen_player_markers.py`의 shadow 잡 (32×32 → 32×16 NEAREST 다운스케일)에서 검증.

**How to apply:**
- 타겟 사이즈 < 32 또는 비정사각: 32×32 (혹은 가까운 32 배수)로 생성 → PIL 후처리
- 거의 단색/평탄한 자산 (그림자, 라이트 스폿)은 NEAREST 다운샘플로도 깨짐 없음
- 디테일이 많은 자산을 작게 만들 때는 LANCZOS 고려하되 픽셀아트 톤을 깨지 않게 주의

```python
from PIL import Image
from io import BytesIO
img = Image.open(BytesIO(raw_png)).convert("RGBA")
resized = img.resize((final_w, final_h), Image.Resampling.NEAREST)
buf = BytesIO()
resized.save(buf, format="PNG", optimize=True)
final_bytes = buf.getvalue()
```

관련: [[pixellab-generation-pattern]] [[asset-verification-checklist]]
