// ChunkCache.cs — 청크 메모리/디스크 캐시 + Overpass 페치→래스터화. 싱글톤.
// 검은화면 금지: 미로드 청크는 null → 렌더러가 Grass placeholder.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ChunkCache : MonoBehaviour
{
    private static ChunkCache _instance;
    public static ChunkCache Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("ChunkCache");
                _instance = go.AddComponent<ChunkCache>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private readonly Dictionary<(long, long), ChunkData> _mem = new();
    private readonly HashSet<(long, long)> _loading = new();
    private string _dir;

    private void Awake()
    {
        _dir = Path.Combine(Application.persistentDataPath, "earthtiles");
        Directory.CreateDirectory(_dir);
    }

    public ChunkData TryGet(long cx, long cy)
    {
        var key = (cx, cy);
        if (_mem.TryGetValue(key, out var cd)) return cd;
        if (!_loading.Contains(key)) StartCoroutine(Load(cx, cy));
        return null;
    }

    private string PathFor(long cx, long cy) => Path.Combine(_dir, $"{cx}_{cy}.bin");

    private IEnumerator Load(long cx, long cy)
    {
        var key = (cx, cy);
        _loading.Add(key);

        string p = PathFor(cx, cy);
        if (File.Exists(p))
        {
            byte[] bytes = null;
            try { bytes = File.ReadAllBytes(p); } catch { }
            if (bytes != null)
            {
                _mem[key] = ChunkData.Deserialize(cx, cy, bytes);
                _loading.Remove(key);
                yield break;
            }
        }

        var (otx, oty) = GeoTileGrid.ChunkOriginTile(cx, cy);
        int n = GeoTileGrid.ChunkTiles;
        var (latNW, lonNW) = GeoTileGrid.TileCenterLatLon(otx - 1, oty - 1);
        var (latSE, lonSE) = GeoTileGrid.TileCenterLatLon(otx + n, oty + n);
        double south = System.Math.Min(latNW, latSE);
        double north = System.Math.Max(latNW, latSE);
        double west = System.Math.Min(lonNW, lonSE);
        double east = System.Math.Max(lonNW, lonSE);

        string json = null;
        yield return OverpassClient.Instance.Fetch(south, west, north, east, r => json = r);

        ChunkData cd;
        if (!string.IsNullOrEmpty(json))
        {
            var feats = OverpassParser.Parse(json);
            cd = FeatureRasterizer.Rasterize(cx, cy, feats);
            try { File.WriteAllBytes(p, cd.Serialize()); } catch { }
        }
        else
        {
            cd = new ChunkData(cx, cy);
        }
        _mem[key] = cd;
        _loading.Remove(key);
    }

    public void TrimFar(long centerCx, long centerCy, int radius)
    {
        List<(long, long)> rm = null;
        foreach (var k in _mem.Keys)
            if (System.Math.Abs(k.Item1 - centerCx) > radius || System.Math.Abs(k.Item2 - centerCy) > radius)
                (rm ??= new()).Add(k);
        if (rm != null) foreach (var k in rm) _mem.Remove(k);
    }
}
