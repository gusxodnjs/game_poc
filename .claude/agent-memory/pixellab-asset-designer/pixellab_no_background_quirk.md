---
name: pixellab-no-background-quirk
description: PixelLab pixflux `no_background:true`가 가끔 솔리드 배경을 반환 — corner-color 알파 클리핑 후처리 필요
metadata:
  type: project
---

PixelLab pixflux 엔드포인트의 `no_background: true` 플래그는 보장되지 않는다. 모델이 가끔 (특히 둥근 자산 + 부드러운 톤일 때) 코너까지 채운 솔리드 배경 (예: pale blue, off-white)을 반환한다.

**증상:**
- 생성된 PNG의 코너 픽셀들이 `alpha=255`이고 같은 RGB
- Unity에서 SpriteRenderer로 표시했을 때 행성 주위에 사각 배경이 보임

**해결책: corner-color 알파 클리핑 후처리**

1. PNG 코너 4점에서 가장 많이 등장하는 RGB를 배경색으로 추정
2. Manhattan 거리 `<= tol` (보통 10~14) 인 픽셀을 `alpha=0` 으로 변환
3. 자산 본체 색상이 배경 추정색에 비슷하면 본체 일부도 투명해질 수 있으니, 보고 적절히 `tol` 조정

```python
def alpha_clip_background(raw_png: bytes, tol: int = 12) -> bytes:
    img = Image.open(BytesIO(raw_png)).convert("RGBA")
    w, h = img.size
    corners = [img.getpixel((0,0)), img.getpixel((w-1,0)),
               img.getpixel((0,h-1)), img.getpixel((w-1,h-1))]
    from collections import Counter
    br, bg_, bb = Counter([c[:3] for c in corners]).most_common(1)[0][0]
    new_pixels = []
    for r, g, b, a in img.getdata():
        if a == 0 or abs(r-br)+abs(g-bg_)+abs(b-bb) <= tol:
            new_pixels.append((r, g, b, 0))
        else:
            new_pixels.append((r, g, b, a))
    img.putdata(new_pixels)
    buf = BytesIO(); img.save(buf, format="PNG", optimize=True)
    return buf.getvalue()
```

**Why:** 2026-05-17 planet_card 3종 생성 시 ice 카드가 pale-blue 배경으로 채워진 채 반환됨. corner alpha 검증 단계가 없으면 Unity에 들어가 사각 배경이 보이는 사고가 남.

**How to apply:**
- 새 자산 생성 후 항상 코너 alpha 확인 (`PIL Image.getpixel((0,0))[3]`)
- alpha=255면 alpha_clip 후처리 적용
- 자산 본체가 배경 추정 색조와 매우 가까울 경우 (예: 흰 배경 + 흰 캐릭터) tol을 낮추거나 매뉴얼 카핑 고려

관련: [[pixellab-generation-pattern]] [[asset-verification-checklist]]
