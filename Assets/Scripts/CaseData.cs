using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public class CaseTell { public string phrase; public string type; public string explanation; }
[Serializable] public class CaseData
{
    public string id, product, category, adText, verdictText, imageUrl, adImageUrl;
    public bool isSus;
    public int difficulty = 1;
    public CaseTell[] tells;
    [NonSerialized] public Texture2D productImage, adImage;
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(product) || string.IsNullOrWhiteSpace(adText) || adText.Length > 650 || string.IsNullOrWhiteSpace(verdictText) || tells == null || tells.Length > 3) return false;
        if (isSus != (tells.Length > 0)) return false;
        var phrases = new HashSet<string>();
        foreach (var tell in tells)
        {
            if (tell == null || string.IsNullOrWhiteSpace(tell.phrase) || !adText.Contains(tell.phrase) || !phrases.Add(tell.phrase)) return false;
            if (Array.IndexOf(new[] { "vague", "fake_label", "no_proof", "tiny_truth", "wrong_comparison" }, tell.type) < 0) return false;
        }
        return true;
    }
}
[Serializable] public class CaseBatch { public CaseData[] ads; }
public static class CaseScoring
{
    public static string Normalize(string text) => text.Trim().Trim('.', ',', '!', '?', ':', ';', '"').ToLowerInvariant();
    public static int Score(CaseData data, bool verdict, List<string> highlights, int streak, float seconds, out int found, out int falseEvidence)
    {
        found = falseEvidence = 0;
        var matched = new HashSet<int>();
        foreach (string selected in highlights)
        {
            int index = Array.FindIndex(data.tells, t => Normalize(t.phrase) == Normalize(selected));
            if (index >= 0 && matched.Add(index)) found++; else falseEvidence++;
        }
        if (verdict != data.isSus) return 0;
        return Mathf.Max(0, 100 + found * 50 - falseEvidence * 25 + Mathf.Min(streak * 10, 100) + (seconds < 15 ? 25 : 0));
    }
    public static int Maximum(CaseData data, int streak) => 125 + data.tells.Length * 50 + Mathf.Min(streak * 10, 100);
}
