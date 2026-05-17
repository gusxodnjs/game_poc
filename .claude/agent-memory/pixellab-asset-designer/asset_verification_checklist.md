---
name: asset-verification-checklist
description: PixelLab 생성 후 자산 검증 단계 (file/sips/시각 확인) — 매번 실행 필수
metadata:
  type: feedback
---

PixelLab 생성 후 자산이 정상인지 매번 다음 단계로 검증해야 한다.

**Why:** [[pixellab-polling-quirk]] 사고 이후 PM이 매 작업마다 검증 단계를 명시함. 자산이 깨졌는데 게임에 들어가서 발견되면 추적/롤백 비용이 큼.

**How to apply (생성 직후 즉시):**

1. **PNG 매직바이트 + 사이즈 (one-shot):**
   ```bash
   file <path1> <path2> ...
   # 기대 출력: "PNG image data, W x H, 8-bit/color RGBA, non-interlaced"
   ```

2. **정확한 픽셀 사이즈 확인 (macOS):**
   ```bash
   sips -g pixelWidth -g pixelHeight <path1> <path2>
   ```

3. **파일 크기 체크:**
   ```bash
   ls -la <path>
   ```
   - 일반 캐릭터/타일: > 1KB 기대
   - 단색/평탄 자산 (그림자, 라이트 스폿): 수백 바이트도 OK — 단, file/sips로 PNG 정상인지 확인
   - **70B 정도면 [[pixellab-polling-quirk]] 의심**

4. **Read 툴로 시각 확인:** 픽셀아트는 사이즈가 작아서 Read 툴로 미리보기 가능. walker 스프라이트 같은 앵커 자산과 같이 띄워 톤 일관성 확인.

5. **요약 JSON 확인:** `scripts/gen_*_result.json`의 `total_usd`, `attempts`, `success` 필드 점검.

**보고 시 항상 포함:**
- 생성 비용 (`usage.usd` 합계)
- 검증 결과 (file/sips 출력 요약)
- 시각 일관성 메모 (앵커 자산 대비)

관련: [[pixellab-polling-quirk]] [[pixellab-generation-pattern]]
