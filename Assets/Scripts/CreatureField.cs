using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지구 산책 레이어의 "움직이는 샘플 생물" — 화면 안 잔디 위를 배회하는 무당벌레/꿀벌.
/// 탭하면 잡아서 "내 행성으로" 날아가는 연출 + GameSession 컬렉션에 기록(카운터).
///
/// 좌표계: 생물 SpriteRenderer 를 TilemapRenderer.MapRoot 하위에 두고 LocalForTileFrac 로 배치 →
///         타일과 동일 변환이라 팬/줌/GPS 추적에 맵과 함께 움직인다(오쏘 카메라가 줌 자동 처리).
/// 결정론 불요(앰비언트) — System.Random 사용. 화면 밖으로 나가면 despawn + 가장자리 리스폰해 ~N마리 유지.
/// 범위 밖: 행성 위 실제 배치/발전 시각화, 식물 종, 미니게임(설계 §4).
/// </summary>
public class CreatureField : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private TilemapRenderer tilemap;
    [SerializeField] private Camera mapCamera;

    [Header("생물 애니 프레임 (각 4프레임, 64x64)")]
    [SerializeField] private Texture2D[] ladybugFrames;
    [SerializeField] private Texture2D[] honeybeeFrames;

    [Header("튜닝")]
    [SerializeField] private int maxCreatures = 5;
    [SerializeField] private float animFps = 8f;
    [SerializeField] private float spriteScale = 0.55f;   // 월드 유닛(타일) 기준 크기
    [SerializeField] private float catchRadiusWorld = 0.7f;
    [SerializeField] private float flyDuration = 0.55f;

    private enum Kind { Ladybug, Honeybee }

    private class Creature
    {
        public Kind kind;
        public string speciesId;
        public Sprite[] frames;
        public double gxf, gyf;     // 전역 타일-분수좌표
        public float heading;        // rad
        public float speed;          // tiles/sec
        public float turnTimer;      // 다음 방향전환까지
        public float animOffset;     // 프레임 위상 분산
        public SpriteRenderer sr;
        public bool flying;
        public float flyT;
        public Vector3 flyStartLocal;
    }

    private readonly List<Creature> _creatures = new();
    private Sprite[] _ladybugSprites, _honeybeeSprites;
    private Transform _root;
    private System.Random _rng = new System.Random();

    // 입력(탭 판정)
    private bool _pointerDown;
    private Vector2 _downPos;
    private float _downTime;

    // UI
    private GUIStyle _counterStyle, _toastStyle, _toastShadow;
    private string _toastMsg = "";
    private float _toastTime = -10f;
    private int _caughtTotal;

    private static readonly Dictionary<string, string> DisplayName = new()
    {
        { "ladybug", "무당벌레" }, { "honeybee", "꿀벌" },
    };

    private void Start()
    {
        if (tilemap == null) { enabled = false; return; }
        if (mapCamera == null) mapCamera = Camera.main;
        _root = tilemap.MapRoot;
        if (_root == null) { enabled = false; return; }

        _ladybugSprites = BuildSprites(ladybugFrames);
        _honeybeeSprites = BuildSprites(honeybeeFrames);
        _caughtTotal = (GameSession.Instance != null) ? GameSession.Instance.CaughtTotal : 0;
    }

    private Sprite[] BuildSprites(Texture2D[] texs)
    {
        if (texs == null || texs.Length == 0) return null;
        var sprites = new List<Sprite>(texs.Length);
        foreach (var t in texs)
        {
            if (t == null) continue;
            t.filterMode = FilterMode.Point;
            // 하단-중앙 pivot, PPU 64 → 64px 스프라이트가 1유닛, localScale 로 축소.
            sprites.Add(Sprite.Create(t, new Rect(0, 0, t.width, t.height),
                new Vector2(0.5f, 0.2f), 64, 0, SpriteMeshType.FullRect));
        }
        return sprites.Count > 0 ? sprites.ToArray() : null;
    }

    private bool HasAnySprites => (_ladybugSprites != null) || (_honeybeeSprites != null);

    private void Update()
    {
        if (tilemap == null) return;
        if (mapCamera == null) mapCamera = Camera.main;
        // 도메인 리로드(Play 중 재컴파일) 자가복구: 비직렬화 상태 유실 감지 → 재초기화.
        // 옛 생물 SpriteRenderer 는 TilemapRenderer 의 옛 _root 와 함께 파괴되므로 리스트도 비운다.
        if (_root == null || (_ladybugSprites == null && _honeybeeSprites == null))
        {
            _root = tilemap.MapRoot;
            _ladybugSprites = BuildSprites(ladybugFrames);
            _honeybeeSprites = BuildSprites(honeybeeFrames);
            _creatures.Clear();
        }
        if (_root == null || mapCamera == null || !HasAnySprites) return;

        // 목표 마릿수 유지(프레임당 최대 2마리 스폰 — 스폰 실패 시 무한루프 방지)
        for (int s = 0; s < 2 && _creatures.Count < maxCreatures; s++) SpawnCreature();

        float viewR = ViewRadiusTiles();
        tilemap.CenterTileFrac(out double cTxF, out double cTyF);
        float dt = Time.deltaTime;
        float t = Time.time;
        var billboard = Quaternion.Euler(0f, 0f, -tilemap.HeadingDeg); // 맵 회전해도 생물은 수직 유지

        for (int i = _creatures.Count - 1; i >= 0; i--)
        {
            var c = _creatures[i];

            if (c.flying)
            {
                c.flyT += dt / Mathf.Max(0.01f, flyDuration);
                float u = Mathf.Clamp01(c.flyT);
                var p = c.flyStartLocal + Vector3.up * (u * 3.2f);          // 위로 솟구침
                c.sr.transform.localPosition = p;
                float s = spriteScale * (1f - u) * (1f + u * 0.4f);          // 살짝 커졌다 사라짐
                c.sr.transform.localScale = new Vector3(s, s, 1f);
                c.sr.transform.localRotation = billboard;
                var col = c.sr.color; col.a = 1f - u; c.sr.color = col;
                if (c.flyT >= 1f)
                {
                    if (GameSession.Instance != null) _caughtTotal = GameSession.Instance.AddCaught(c.speciesId);
                    Destroy(c.sr.gameObject);
                    _creatures.RemoveAt(i);
                }
                continue;
            }

            // 배회: 방향 전환 타이머
            c.turnTimer -= dt;
            if (c.turnTimer <= 0f)
            {
                float spread = (c.kind == Kind.Honeybee) ? 1.4f : 0.7f;     // 꿀벌이 더 자주/크게 꺾음
                c.heading += ((float)_rng.NextDouble() - 0.5f) * 2f * spread;
                c.turnTimer = (c.kind == Kind.Honeybee) ? 0.4f + (float)_rng.NextDouble() * 0.5f
                                                        : 0.8f + (float)_rng.NextDouble() * 1.2f;
            }

            // 전진(물/건물 회피: 다음 칸이 못 가는 타입이면 반전)
            double nx = c.gxf + Mathf.Cos(c.heading) * c.speed * dt;
            double ny = c.gyf + Mathf.Sin(c.heading) * c.speed * dt;
            if (!tilemap.IsWalkable((long)System.Math.Floor(nx), (long)System.Math.Floor(ny)))
            {
                c.heading += Mathf.PI;                                       // 막히면 되돌아감
                c.turnTimer = 0.3f;
            }
            else { c.gxf = nx; c.gyf = ny; }

            // 화면 밖으로 멀어지면 despawn(다음 루프에서 리스폰)
            double ddx = c.gxf - cTxF, ddy = c.gyf - cTyF;
            if (ddx * ddx + ddy * ddy > (viewR + 3f) * (viewR + 3f))
            {
                Destroy(c.sr.gameObject);
                _creatures.RemoveAt(i);
                continue;
            }

            // 렌더
            var local = tilemap.LocalForTileFrac(c.gxf, c.gyf);
            c.sr.transform.localPosition = local;
            c.sr.transform.localScale = new Vector3(spriteScale, spriteScale, 1f);
            c.sr.transform.localRotation = billboard;
            var frames = c.frames;
            if (frames != null && frames.Length > 0)
            {
                int idx = Mathf.FloorToInt((t + c.animOffset) * animFps) % frames.Length;
                c.sr.sprite = frames[idx];
            }
            c.sr.flipX = Mathf.Cos(c.heading) < 0f;                          // 왼쪽 이동 시 뒤집기
        }

        HandleTap();
    }

    private float ViewRadiusTiles()
    {
        float halfH = mapCamera.orthographicSize;
        float halfW = halfH * mapCamera.aspect;
        return Mathf.Sqrt(halfH * halfH + halfW * halfW); // 반대각선(타일=유닛)
    }

    private void SpawnCreature()
    {
        tilemap.CenterTileFrac(out double cTxF, out double cTyF);
        float viewR = ViewRadiusTiles();

        // 화면 가장자리 근처에 스폰(걸을 수 있는 칸). 최대 12회 시도.
        double gx = cTxF, gy = cTyF;
        bool ok = false;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float ang = (float)_rng.NextDouble() * Mathf.PI * 2f;
            float dist = viewR * (0.6f + (float)_rng.NextDouble() * 0.5f);  // 화면 안~가장자리
            gx = cTxF + Mathf.Cos(ang) * dist;
            gy = cTyF + Mathf.Sin(ang) * dist;
            if (tilemap.IsWalkable((long)System.Math.Floor(gx), (long)System.Math.Floor(gy))) { ok = true; break; }
        }
        if (!ok) return;

        bool bee = (_honeybeeSprites != null) && ((_ladybugSprites == null) || _rng.Next(2) == 0);
        var c = new Creature
        {
            kind = bee ? Kind.Honeybee : Kind.Ladybug,
            speciesId = bee ? "honeybee" : "ladybug",
            frames = bee ? _honeybeeSprites : _ladybugSprites,
            gxf = gx, gyf = gy,
            heading = (float)_rng.NextDouble() * Mathf.PI * 2f,
            speed = bee ? 0.7f : 0.35f,
            turnTimer = (float)_rng.NextDouble(),
            animOffset = (float)_rng.NextDouble() * 4f,
        };

        var go = new GameObject(bee ? "Creature_Honeybee" : "Creature_Ladybug");
        go.transform.SetParent(_root, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 1000;                                             // 지형/오브젝트 위
        if (c.frames != null && c.frames.Length > 0) sr.sprite = c.frames[0];
        c.sr = sr;
        _creatures.Add(c);
    }

    // ---- 탭 잡기 (맵 드래그/핀치와 충돌 회피: 짧고 안 움직인 단일 터치만) ----
    private void HandleTap()
    {
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)) { _pointerDown = true; _downPos = Input.mousePosition; _downTime = Time.time; }
        else if (Input.GetMouseButtonUp(0) && _pointerDown)
        {
            _pointerDown = false;
            TryCatchAt(Input.mousePosition, Time.time - _downTime, ((Vector2)Input.mousePosition - _downPos).magnitude);
        }
#else
        if (Input.touchCount == 1)
        {
            var tch = Input.GetTouch(0);
            if (tch.phase == TouchPhase.Began) { _pointerDown = true; _downPos = tch.position; _downTime = Time.time; }
            else if (tch.phase == TouchPhase.Ended && _pointerDown)
            {
                _pointerDown = false;
                TryCatchAt(tch.position, Time.time - _downTime, (tch.position - _downPos).magnitude);
            }
        }
        else if (Input.touchCount >= 2) { _pointerDown = false; } // 핀치 중엔 잡기 취소
#endif
    }

    private void TryCatchAt(Vector2 screenPos, float duration, float moved)
    {
        if (duration > 0.35f || moved > 16f) return; // 드래그/롱프레스는 잡기 아님
        Vector3 world = mapCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -mapCamera.transform.position.z));
        Creature best = null; float bestD = catchRadiusWorld * catchRadiusWorld;
        foreach (var c in _creatures)
        {
            if (c.flying) continue;
            Vector3 wp = _root.TransformPoint(c.sr.transform.localPosition);
            float d = (wp.x - world.x) * (wp.x - world.x) + (wp.y - world.y) * (wp.y - world.y);
            if (d < bestD) { bestD = d; best = c; }
        }
        if (best == null) return;
        best.flying = true;
        best.flyT = 0f;
        best.flyStartLocal = best.sr.transform.localPosition;
        DisplayName.TryGetValue(best.speciesId, out string nm);
        _toastMsg = (nm ?? best.speciesId) + " → 내 행성으로!";
        _toastTime = Time.time;
    }

    private void OnGUI()
    {
        EnsureStyles();
        Rect safe = Screen.safeArea;

        // 누적 카운터(좌상단 safe)
        string counter = "🌱 모은 생물 " + _caughtTotal;
        var cRect = new Rect(safe.x + 16f, safe.y + 12f, Screen.width * 0.6f, _counterStyle.fontSize * 1.6f);
        var prevC = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(cRect.x + 2f, cRect.y + 2f, cRect.width, cRect.height), counter, _counterStyle);
        GUI.color = prevC;
        GUI.Label(cRect, counter, _counterStyle);

        // 토스트(중상단, 2s 페이드)
        float elapsed = Time.time - _toastTime;
        if (!string.IsNullOrEmpty(_toastMsg) && elapsed < 2f)
        {
            float a = elapsed < 0.3f ? elapsed / 0.3f : (elapsed > 1.7f ? (2f - elapsed) / 0.3f : 1f);
            var r = new Rect(0f, Screen.height * 0.2f, Screen.width, _toastStyle.fontSize * 2f);
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), _toastMsg, _toastShadow);
            GUI.Label(r, _toastMsg, _toastStyle);
            GUI.color = prev;
        }
    }

    private void EnsureStyles()
    {
        if (_counterStyle != null) return;
        int big = Mathf.Max(18, Screen.height / 28);
        _counterStyle = new GUIStyle(GUI.skin.label)
        { fontSize = big, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
        _counterStyle.normal.textColor = new Color(1f, 1f, 1f, 1f);
        _toastStyle = new GUIStyle(GUI.skin.label)
        { fontSize = Mathf.Max(20, Screen.height / 22), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        _toastStyle.normal.textColor = new Color(1f, 0.95f, 0.6f, 1f);
        _toastShadow = new GUIStyle(_toastStyle) { alignment = TextAnchor.MiddleCenter };
        _toastShadow.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
    }
}
