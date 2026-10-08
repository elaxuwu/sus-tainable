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
    public string Status { get; private set; } = "LOCAL CASE ARCHIVE";
    public int ReadyCount => queue.Count;
    public bool IsFetching => fetching;
    public List<CaseData> Fallback { get; private set; } = new List<CaseData>();
    readonly Queue<CaseData> queue = new Queue<CaseData>();
    readonly HashSet<string> used = new HashSet<string>();
    readonly Queue<CaseData> localShift=new Queue<CaseData>();
    readonly List<Texture2D> textures = new List<Texture2D>();
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
        if (Application.platform == RuntimePlatform.WebGLPlayer && string.IsNullOrEmpty(endpoint)) endpoint = "/api/cases";
    }
    void Update()
    {
        if (!offlineOnly && !string.IsNullOrEmpty(endpoint) && queue.Count < 5 && !fetching && Time.unscaledTime >= nextRetry) StartCoroutine(Fetch());
    }
    public void ResetShift(string practiceId=null)
    {
        used.Clear();localShift.Clear();if(!string.IsNullOrEmpty(practiceId))used.Add(practiceId);
        var sus=Fallback.FindAll(c=>c.isSus&&!used.Contains(c.id));var legit=Fallback.FindAll(c=>!c.isSus&&!used.Contains(c.id));
        Shuffle(sus);Shuffle(legit);var deck=new List<CaseData>();deck.AddRange(sus.GetRange(0,Mathf.Min(6,sus.Count)));deck.AddRange(legit.GetRange(0,Mathf.Min(4,legit.Count)));Shuffle(deck);
        foreach(var item in deck)localShift.Enqueue(item);
    }
    static void Shuffle(List<CaseData> items){for(int i=items.Count-1;i>0;i--){int j=UnityEngine.Random.Range(0,i+1);var swap=items[i];items[i]=items[j];items[j]=swap;}}
    public CaseData Take()
    {
        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            if (used.Add(item.id)) return item;
            Release(item);
        }
        while(localShift.Count>0){var item=localShift.Dequeue();if(used.Add(item.id))return item;}
        var candidates=Fallback.FindAll(c=>!used.Contains(c.id));
        if (candidates.Count == 0) return null;
        var fallback = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        used.Add(fallback.id);
        return fallback;
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
