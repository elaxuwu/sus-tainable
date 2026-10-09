using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AdProvider : MonoBehaviour
{
    [Tooltip("Leave empty to use local cases in the Editor. WebGL defaults to /api/cases.")]
    public string endpoint = "";
    public bool offlineOnly;
    public int difficulty = 1;
    public AdaptiveAgentSettings adaptiveSettings = new AdaptiveAgentSettings();
    public string Status { get; private set; } = "LOCAL CASE ARCHIVE";
    public int ReadyCount => queue.Count;
    public bool IsFetching => fetching;
    public List<CaseData> Fallback { get; private set; } = new List<CaseData>();
    readonly Queue<CaseData> queue = new Queue<CaseData>();
    readonly HashSet<string> used = new HashSet<string>();
    readonly List<Texture2D> textures = new List<Texture2D>();
    AdaptiveCategoryAgent adaptiveAgent;
    int susRemaining = 6;
    int legitRemaining = 4;
    int shiftTaken;
    bool fetching;
    float nextRetry;
    void Awake()
    {
        var source = Resources.Load<TextAsset>("fallback_ads");
        if (source != null)
        {
            try{var batch = JsonUtility.FromJson<CaseBatch>(source.text);
            if(batch?.ads!=null)foreach (var item in batch.ads) if (item!=null&&item.IsValid()) Fallback.Add(item);}catch(Exception){Debug.LogWarning("Local case archive is invalid.");}
        }
        var categories = new List<string>();
        foreach (var item in Fallback) if (item != null && !string.IsNullOrWhiteSpace(item.category) && !categories.Contains(item.category)) categories.Add(item.category);
        adaptiveAgent = new AdaptiveCategoryAgent(categories, adaptiveSettings);
        if (Application.platform == RuntimePlatform.WebGLPlayer && string.IsNullOrEmpty(endpoint)) endpoint = "/api/cases";
    }
    void Update()
    {
        if (!offlineOnly && !string.IsNullOrEmpty(endpoint) && queue.Count < 5 && !fetching && Time.unscaledTime >= nextRetry) StartCoroutine(Fetch());
    }
    public void ResetShift(string practiceId=null)
    {
        used.Clear();if(!string.IsNullOrEmpty(practiceId))used.Add(practiceId);
        susRemaining = 6; legitRemaining = 4; shiftTaken = 0;
    }
    public CaseData Take()
    {
        if (adaptiveAgent == null || shiftTaken >= 10) return null;
        RemoveAlreadyUsedLiveCases();
        var selected = adaptiveAgent.SelectCase(queue.ToArray(), Fallback, used, susRemaining, legitRemaining);
        if (selected == null) return null;
        RemoveQueuedCase(selected);
        used.Add(selected.id);
        if (selected.isSus) susRemaining = Mathf.Max(0, susRemaining - 1);
        else legitRemaining = Mathf.Max(0, legitRemaining - 1);
        shiftTaken++;
        return selected;
    }

    public void RecordAnswer(string category, bool correct) { if (adaptiveAgent != null) adaptiveAgent.RecordAnswer(category, correct); }

    [ContextMenu("Log Adaptive Agent Diagnostics")]
    public void LogAdaptiveAgentDiagnostics()
    {
        if (adaptiveAgent != null) Debug.Log(adaptiveAgent.GetDiagnostics(), this);
    }

    void RemoveAlreadyUsedLiveCases()
    {
        int count = queue.Count;
        for (int i = 0; i < count; i++)
        {
            var item = queue.Dequeue();
            if (item != null && used.Contains(item.id)) Release(item);
            else queue.Enqueue(item);
        }
    }

    void RemoveQueuedCase(CaseData selected)
    {
        int count = queue.Count;
        bool removed = false;
        for (int i = 0; i < count; i++)
        {
            var item = queue.Dequeue();
            if (!removed && ReferenceEquals(item, selected)) removed = true;
            else queue.Enqueue(item);
        }
    }
    public void Release(CaseData item)
    {
        if (item == null) return;
        foreach (var texture in new[] { item.productImage, item.adImage })
            if (texture != null && textures.Remove(texture)) Destroy(texture);
        item.productImage = item.adImage = null;
    }
    IEnumerator Fetch()
    {
        fetching = true; Status = "SUS FACTORY: PREPARING CASES";
        using (var request = new UnityWebRequest(endpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes("{\"difficulty\":" + Mathf.Clamp(difficulty, 1, 5) + ",\"count\":1}"));
            request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 120;
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            CaseBatch batch = null;
            if (request.result == UnityWebRequest.Result.Success)
            {
                try { batch = JsonUtility.FromJson<CaseBatch>(request.downloadHandler.text); } catch (Exception) { }
            }
            if (batch != null && batch.ads != null)
            {
                foreach (var item in batch.ads)
                {
                    if (queue.Count >= 5) break;
                    if (item == null || !item.IsValid() || used.Contains(item.id)) continue;
                    yield return LoadImage(item.imageUrl, t => item.productImage = t);
                    yield return LoadImage(item.adImageUrl, t => item.adImage = t);
                    // Only fully downloaded live cases count towards the ready buffer.
                    if (item.productImage != null && item.adImage != null) queue.Enqueue(item);
                    else Release(item);
                }
            }
        }
        fetching = false; nextRetry = Time.unscaledTime + (queue.Count == 0 ? 30f : 2f);
        Status = queue.Count > 0 ? "SUS FACTORY: " + queue.Count + " READY" : "LOCAL ARCHIVE ACTIVE - FACTORY UNAVAILABLE";
    }
    IEnumerator LoadImage(string url, Action<Texture2D> accept)
    {
        if (string.IsNullOrEmpty(url)) yield break;
        if (url.StartsWith("data:image/"))
        {
            if(url.Length>12000000)yield break;
            Texture2D texture=null;
            try
            {
                texture = new Texture2D(2, 2);
                if (texture.LoadImage(Convert.FromBase64String(url.Substring(url.IndexOf(',') + 1)))) { textures.Add(texture); accept(texture); }
                else Destroy(texture);
            } catch (Exception) { if(texture!=null)Destroy(texture); }
            yield break;
        }
        if (!url.StartsWith("https://") && !url.StartsWith("/")) yield break;
        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = 20; yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success) { var texture = DownloadHandlerTexture.GetContent(request); textures.Add(texture); accept(texture); }
        }
    }
    void OnDestroy() { foreach (var texture in textures) if (texture != null) Destroy(texture); }
}
