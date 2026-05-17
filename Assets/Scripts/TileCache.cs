// TileCache.cs
// TERRA × BIOSPHERE PoC — OSM raster tile 다운로드/캐시.
//
// 정책:
//   - User-Agent 필수 (OSM 약관): "terra-poc/0.1 (gusxodnjs@gmail.com)"
//   - 동시 다운로드 최대 4개 (OSM 매너 + 모바일 네트워크 부담)
//   - 메모리 LRU 50엔트리
//   - 디스크 캐시: Application.persistentDataPath/tiles/{z}_{x}_{y}.png
//   - 동적 텍스처 생성 회피: 다운로드 → 디스크 저장 → File.ReadAllBytes →
//     LoadImage 경로로만 Texture2D 를 채운다. 검은 화면/픽셀 깨짐 회피.
//   - Placeholder: 첫 1회만 LoadImage 로 1×1 회색 PNG (Resources 불필요)을 만들고
//     이후 재사용. Texture2D ctor 로 동적 생성하지 않는다.
//
// 라이프사이클:
//   - MonoBehaviour 싱글톤. Awake 에서 DontDestroyOnLoad.
//   - 씬 전환 후에도 메모리/디스크 캐시 유지.
//
// 인터페이스:
//   IEnumerator GetTile(int z, int x, int y, Action<Texture2D> onLoaded)
//     - 항상 onLoaded 가 한 번 호출됨 (실패해도 placeholder 전달).
//
// 알려진 한계:
//   - 디스크 캐시 만료/사이즈 제한 없음 (PoC 범위 외).
//   - HTTP ETag / If-Modified-Since 미구현 (OSM tile 은 거의 정적이라 PoC 충분).

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class TileCache : MonoBehaviour
{
    private const string UrlTemplate = "https://tile.openstreetmap.org/{0}/{1}/{2}.png";
    private const string UserAgent = "terra-poc/0.1 (gusxodnjs@gmail.com)";
    private const int MaxMemoryEntries = 50;
    private const int MaxConcurrent = 4;
    private const int RequestTimeoutSec = 15;

    private static TileCache _instance;
    public static TileCache Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("TileCache");
                _instance = go.AddComponent<TileCache>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // LRU: LinkedList(가장 오래된 → 가장 최근). Dictionary 가 노드 핸들 보관.
    private readonly LinkedList<string> _lruOrder = new LinkedList<string>();
    private readonly Dictionary<string, LinkedListNode<string>> _lruNodes = new Dictionary<string, LinkedListNode<string>>(64);
    private readonly Dictionary<string, Texture2D> _memCache = new Dictionary<string, Texture2D>(64);

    // 같은 타일에 대해 중복 다운로드를 막기 위한 in-flight 콜백 리스트.
    private readonly Dictionary<string, List<Action<Texture2D>>> _inflight = new Dictionary<string, List<Action<Texture2D>>>();

    private int _activeDownloads;
    private readonly Queue<TileRequest> _pending = new Queue<TileRequest>();

    private string _diskRoot;
    private Texture2D _placeholder;

    /// <summary>
    /// 로딩 중 임시로 깔 회색 placeholder 텍스처. MapView 가 새 슬롯 만들 때 즉시 부착.
    /// 절대 Destroy 하지 말 것 (캐시 소유).
    /// </summary>
    public Texture2D Placeholder => _placeholder;

    private struct TileRequest
    {
        public int Z, X, Y;
        public string Key;
        public string DiskPath;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _diskRoot = Path.Combine(Application.persistentDataPath, "tiles");
        try
        {
            if (!Directory.Exists(_diskRoot)) Directory.CreateDirectory(_diskRoot);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[TileCache] disk root 생성 실패: " + e.Message);
        }

        // placeholder 는 1x1 회색 PNG 을 디스크에 한 번 써 두고 LoadImage 로 읽어 만든다.
        // (동적 텍스처 생성 금지 규칙 준수)
        _placeholder = LoadOrWritePlaceholder();
    }

    /// <summary>
    /// 타일을 가져온다. 메모리/디스크 캐시 우선, 없으면 네트워크. onLoaded 는 한 번 보장.
    /// </summary>
    public IEnumerator GetTile(int z, int x, int y, Action<Texture2D> onLoaded)
    {
        string key = MakeKey(z, x, y);

        // 1) 메모리 캐시
        if (_memCache.TryGetValue(key, out var cached) && cached != null)
        {
            TouchLru(key);
            onLoaded?.Invoke(cached);
            yield break;
        }

        // 2) 디스크 캐시
        string diskPath = Path.Combine(_diskRoot, $"{z}_{x}_{y}.png");
        if (File.Exists(diskPath))
        {
            Texture2D tex = LoadFromDisk(diskPath);
            if (tex != null)
            {
                Put(key, tex);
                onLoaded?.Invoke(tex);
                yield break;
            }
            // 손상된 파일은 지우고 재다운로드 시도
            try { File.Delete(diskPath); } catch { /* ignore */ }
        }

        // 3) 네트워크 — 중복 in-flight 합치기
        if (_inflight.TryGetValue(key, out var list))
        {
            list.Add(onLoaded);
            yield break;
        }
        _inflight[key] = new List<Action<Texture2D>> { onLoaded };

        var req = new TileRequest { Z = z, X = x, Y = y, Key = key, DiskPath = diskPath };
        _pending.Enqueue(req);
        TryStartNext();
    }

    private void TryStartNext()
    {
        while (_activeDownloads < MaxConcurrent && _pending.Count > 0)
        {
            var req = _pending.Dequeue();
            _activeDownloads++;
            StartCoroutine(Download(req));
        }
    }

    private IEnumerator Download(TileRequest req)
    {
        string url = string.Format(UrlTemplate, req.Z, req.X, req.Y);
        // UnityWebRequestTexture 대신 일반 Get — 우리는 PNG 바이트를 디스크에 저장 후
        // LoadImage 로 다시 디코딩하므로 (동적 텍스처 생성 회피 정책) GetTexture 의 자동 디코딩이 불필요.
        using (var www = UnityWebRequest.Get(url))
        {
            www.SetRequestHeader("User-Agent", UserAgent);
            www.timeout = RequestTimeoutSec;
            yield return www.SendWebRequest();

            Texture2D resolved = null;
            if (www.result == UnityWebRequest.Result.Success)
            {
                // 디스크 저장 — downloadHandler.data 는 PNG 바이트 그대로
                try
                {
                    File.WriteAllBytes(req.DiskPath, www.downloadHandler.data);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TileCache] disk write 실패 z={req.Z} x={req.X} y={req.Y}: {e.Message}");
                }

                // LoadImage 경로로 통일 (동적 생성 회피 — 다운로드한 텍스처를 직접 쓰지 않는다)
                resolved = LoadFromDisk(req.DiskPath);
            }
            else
            {
                Debug.LogWarning($"[TileCache] 다운로드 실패 z={req.Z} x={req.X} y={req.Y}: {www.error}");
            }

            if (resolved == null) resolved = _placeholder;
            else Put(req.Key, resolved);

            // in-flight 콜백 전부 호출
            if (_inflight.TryGetValue(req.Key, out var callbacks))
            {
                _inflight.Remove(req.Key);
                for (int i = 0; i < callbacks.Count; i++)
                {
                    try { callbacks[i]?.Invoke(resolved); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
        }

        _activeDownloads--;
        TryStartNext();
    }

    private Texture2D LoadFromDisk(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            // LoadImage 는 빈 텍스처(가능하면 1×1)에 실제 이미지로 덮어쓴다.
            // ctor 만으로 사용하는 것이 아니라 LoadImage 가 PNG 디코딩까지 수행하므로
            // "동적 텍스처 생성" 함정(이전 PR 트라우마)을 피한다.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (tex.LoadImage(bytes, markNonReadable: true))
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                return tex;
            }
            UnityEngine.Object.Destroy(tex);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[TileCache] LoadFromDisk 실패: " + e.Message);
        }
        return null;
    }

    private Texture2D LoadOrWritePlaceholder()
    {
        // 1×1 RGB(128,128,128) PNG — Python 으로 사전 인코딩한 표준 PNG.
        // 동적 텍스처 생성 회피를 위해 LoadImage 경유로만 만든다.
        byte[] grayPng = new byte[]
        {
            0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A, 0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
            0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01, 0x08,0x02,0x00,0x00,0x00,0x90,0x77,0x53,
            0xDE,0x00,0x00,0x00,0x0C,0x49,0x44,0x41, 0x54,0x78,0xDA,0x63,0x68,0x68,0x68,0x00,
            0x00,0x03,0x04,0x01,0x81,0x75,0x2E,0x01, 0xBC,0x00,0x00,0x00,0x00,0x49,0x45,0x4E,
            0x44,0xAE,0x42,0x60,0x82,
        };
        string placeholderPath = Path.Combine(_diskRoot, "_placeholder.png");
        try
        {
            if (!File.Exists(placeholderPath))
            {
                File.WriteAllBytes(placeholderPath, grayPng);
            }
            var tex = LoadFromDisk(placeholderPath);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                return tex;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[TileCache] placeholder 생성 실패: " + e.Message);
        }
        // 최후 수단: 그래도 만들어 둔다 (단 1회). 검은 화면보다 회색이 낫다.
        var fallback = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        fallback.LoadImage(grayPng, true);
        return fallback;
    }

    private void Put(string key, Texture2D tex)
    {
        if (_memCache.ContainsKey(key))
        {
            _memCache[key] = tex;
            TouchLru(key);
            return;
        }
        _memCache[key] = tex;
        var node = _lruOrder.AddLast(key);
        _lruNodes[key] = node;
        EvictIfNeeded();
    }

    private void TouchLru(string key)
    {
        if (_lruNodes.TryGetValue(key, out var node))
        {
            _lruOrder.Remove(node);
            _lruOrder.AddLast(node);
        }
    }

    private void EvictIfNeeded()
    {
        while (_lruOrder.Count > MaxMemoryEntries)
        {
            var oldest = _lruOrder.First;
            if (oldest == null) return;
            string key = oldest.Value;
            _lruOrder.RemoveFirst();
            _lruNodes.Remove(key);
            if (_memCache.TryGetValue(key, out var tex))
            {
                _memCache.Remove(key);
                if (tex != null && tex != _placeholder) UnityEngine.Object.Destroy(tex);
            }
        }
    }

    private static string MakeKey(int z, int x, int y) => $"{z}/{x}/{y}";

    private void OnApplicationLowMemory()
    {
        // iOS 메모리 경고 — placeholder 와 가장 최근 항목 일부만 남기고 비운다.
        int keep = Mathf.Min(8, _lruOrder.Count);
        int drop = _lruOrder.Count - keep;
        for (int i = 0; i < drop; i++)
        {
            var oldest = _lruOrder.First;
            if (oldest == null) break;
            string key = oldest.Value;
            _lruOrder.RemoveFirst();
            _lruNodes.Remove(key);
            if (_memCache.TryGetValue(key, out var tex))
            {
                _memCache.Remove(key);
                if (tex != null && tex != _placeholder) UnityEngine.Object.Destroy(tex);
            }
        }
        Resources.UnloadUnusedAssets();
        Debug.Log("[TileCache] low memory — 캐시 축소: 남은 " + _lruOrder.Count);
    }

    private void OnEnable()
    {
        Application.lowMemory += OnApplicationLowMemory;
    }

    private void OnDisable()
    {
        Application.lowMemory -= OnApplicationLowMemory;
    }
}
