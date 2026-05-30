// OverpassClient.cs — Overpass API bbox 페치. 싱글톤. OSM 매너: User-Agent, 동시요청 1, 백오프.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class OverpassClient : MonoBehaviour
{
    private const string Endpoint = "https://overpass-api.de/api/interpreter";
    private const string UserAgent = "terra-poc/0.2 (gusxodnjs@gmail.com)";

    private static OverpassClient _instance;
    public static OverpassClient Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("OverpassClient");
                _instance = go.AddComponent<OverpassClient>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private bool _busy;

    public IEnumerator Fetch(double south, double west, double north, double east, Action<string> onDone)
    {
        while (_busy) yield return null;
        _busy = true;
        string query =
            $"[out:json][timeout:25];(" +
            $"way[\"highway\"]({south},{west},{north},{east});" +
            $"way[\"natural\"=\"water\"]({south},{west},{north},{east});" +
            $"way[\"water\"]({south},{west},{north},{east});" +
            $"way[\"waterway\"]({south},{west},{north},{east});" +
            $"way[\"landuse\"=\"forest\"]({south},{west},{north},{east});" +
            $"way[\"natural\"=\"wood\"]({south},{west},{north},{east});" +
            $"way[\"leisure\"=\"park\"]({south},{west},{north},{east});" +
            $");out geom;";

        string result = null;
        int attempt = 0;
        while (attempt < 2 && result == null)
        {
            using (var req = UnityWebRequest.Post(Endpoint, "data=" + UnityWebRequest.EscapeURL(query)))
            {
                req.SetRequestHeader("User-Agent", UserAgent);
                req.timeout = 30;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                    result = req.downloadHandler.text;
                else
                {
                    Debug.LogWarning($"[Earth] Overpass fail (attempt {attempt}): {req.error}");
                    yield return new WaitForSeconds(2f * (attempt + 1));
                }
            }
            attempt++;
        }
        _busy = false;
        onDone?.Invoke(result);
    }
}
