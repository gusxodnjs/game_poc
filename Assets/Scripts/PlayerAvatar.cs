using UnityEngine;

/// <summary>
/// 화면 정중앙 고정 플레이어 마커 (Pikmin Bloom 패턴).
/// 지도가 패닝되어도 캐릭터는 항상 화면 정중앙에 머문다. 월드 좌표 이동 X, 스크린 좌표 직접 그리기.
///
/// 표시 순서 (아래 → 위):
///   1) accuracy ring (펄스, alpha 0.4 ~ 0.8, 주기 1.5s)
///   2) shadow         (정적, 캐릭터 발 아래, alpha 0.55)
///   3) idle walker    (4프레임 6fps loop)
///
/// IMGUI 전용 — 프로젝트 컨벤션상 Canvas/Sprite GameObject 사용 안 함.
/// OnGUI 는 Repaint 이벤트만 그려서 mouse/layout 이벤트의 중복 호출을 회피.
/// </summary>
public class PlayerAvatar : MonoBehaviour
{
    [Header("Assets")]
    [Tooltip("Idle 4프레임 (frame0 ~ frame3)")]
    public Texture2D[] idleFrames;
    [Tooltip("GPS 정확도 표시용 링 (64x64)")]
    public Texture2D accuracyRingTex;
    [Tooltip("캐릭터 그림자 (32x16)")]
    public Texture2D shadowTex;

    [Header("타이밍")]
    [Tooltip("Idle 애니메이션 FPS")]
    public float idleFps = 8f;
    [Tooltip("정확도 링 펄스 주기 (초)")]
    public float ringPulsePeriodSec = 1.5f;

    [Header("크기 (논리 픽셀 스케일)")]
    [Tooltip("캐릭터 스케일 — 64*3.2 = 205px (모바일 가시성 확보)")]
    public float characterScale = 3.2f;
    [Tooltip("정확도 링 스케일 — 캐릭터보다 살짝 큰 범위")]
    public float ringScale = 3.4f;
    [Tooltip("그림자 스케일")]
    public float shadowScale = 2.6f;

    [Header("지도 연동")]
    [Tooltip("패닝 시 아바타를 GPS 실제 위치에 고정하기 위한 화면 오프셋 소스")]
    [SerializeField] private TilemapRenderer tilemap;

    private float _startTime;

    private void Start()
    {
        _startTime = Time.time;
    }

    private void OnGUI()
    {
        // OnGUI 는 한 프레임당 여러 번 호출(Layout/Repaint/Mouse...).
        // Repaint 만 그려서 동일 텍스처를 중복 draw 하지 않도록 가드.
        if (Event.current.type != EventType.Repaint) return;

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        // 핀치줌 연동: 맵이 줌인/아웃되면 캐릭터·링·그림자도 함께 크기 변동(월드와 일치).
        float z = (tilemap != null) ? tilemap.ZoomFactor : 1f;
        if (tilemap != null) { var o = tilemap.PlayerGuiOffset; cx += o.x; cy += o.y; }

        // 1) accuracy ring — sin 파형 펄스로 alpha 0.4 ~ 0.8 왕복
        if (accuracyRingTex != null)
        {
            float t = ((Time.time - _startTime) % ringPulsePeriodSec) / ringPulsePeriodSec;
            float pulse = Mathf.Lerp(0.4f, 0.8f, (Mathf.Sin(t * Mathf.PI * 2f) + 1f) * 0.5f);
            float rw = accuracyRingTex.width * ringScale * z;
            float rh = accuracyRingTex.height * ringScale * z;
            GUI.color = new Color(1f, 1f, 1f, pulse);
            GUI.DrawTexture(new Rect(cx - rw * 0.5f, cy - rh * 0.5f, rw, rh), accuracyRingTex, ScaleMode.ScaleToFit);
        }

        // 2) shadow — 캐릭터 발 위치 근사값에 그림자.
        //    캐릭터 sprite 64px 의 발이 대략 중심 +35% 아래에 위치.
        if (shadowTex != null)
        {
            float sw = shadowTex.width * shadowScale * z;
            float sh = shadowTex.height * shadowScale * z;
            float footY = cy + (64f * characterScale * z * 0.35f);
            GUI.color = new Color(1f, 1f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(cx - sw * 0.5f, footY - sh * 0.5f, sw, sh), shadowTex, ScaleMode.ScaleToFit);
        }

        // 3) idle walker — 6fps loop (Time.time 기반, deltaTime 누적 불요)
        if (idleFrames != null && idleFrames.Length > 0)
        {
            int idx = Mathf.FloorToInt((Time.time - _startTime) * idleFps) % idleFrames.Length;
            var tex = idleFrames[idx];
            if (tex != null)
            {
                float w = tex.width * characterScale * z;
                float h = tex.height * characterScale * z;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h), tex, ScaleMode.ScaleToFit);
            }
        }

        // OnGUI 후속 호출자가 GUI.color 잔재 영향 받지 않도록 복구.
        GUI.color = Color.white;
    }
}
