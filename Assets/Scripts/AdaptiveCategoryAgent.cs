using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class AdaptiveAgentSettings
{
    [Min(.05f)] public float minimumWeight = .5f;
    [Min(.05f)] public float maximumWeight = 2f;
    [Min(0f)] public float weightSensitivity = 2f;
    [Min(1)] public int recentWindow = 5;
    [Min(1)] public int recentCaseMemory = 20;
    [Range(0f, 1f)] public float repeatCategoryFactor = .35f;
}

[Serializable]
internal sealed class AdaptiveCategoryStats
{
    public string category;
    public int attempts;
    public int correct;
    public int incorrect;
    public List<bool> recentResults = new List<bool>();
    public float weight = 1f;
}

[Serializable]
internal sealed class AdaptiveAgentMemory
{
    public List<AdaptiveCategoryStats> categories = new List<AdaptiveCategoryStats>();
    public List<string> recentCaseIds = new List<string>();
    public string lastCategory;
    public string lastSelection;
}

public struct AdaptiveCategorySelection
{
    public string category;
    public float weight;
    public string reason;

    public AdaptiveCategorySelection(string category, float weight, string reason)
    {
        this.category = category;
        this.weight = weight;
        this.reason = reason;
    }
}

/// <summary>Small, deterministic-per-session category selector with local aggregate memory.</summary>
public sealed class AdaptiveCategoryAgent
{
    public const string DefaultPrefsKey = "SusTainable.AdaptiveAgent.Memory.v1";

    readonly List<AdaptiveCategoryStats> categories = new List<AdaptiveCategoryStats>();
    readonly Dictionary<string, AdaptiveCategoryStats> byCategory = new Dictionary<string, AdaptiveCategoryStats>(StringComparer.Ordinal);
    readonly List<string> recentCaseIds = new List<string>();
    readonly HashSet<string> recentCaseSet = new HashSet<string>(StringComparer.Ordinal);
    readonly AdaptiveAgentSettings settings;
    readonly string prefsKey;
    readonly System.Random random;
    string lastCategory;
    string lastSelection = "No category selected yet.";

    public AdaptiveCategoryAgent(IEnumerable<string> availableCategories, AdaptiveAgentSettings settings = null, string playerPrefsKey = DefaultPrefsKey)
        : this(availableCategories, settings, playerPrefsKey, Environment.TickCount ^ Guid.NewGuid().GetHashCode())
    {
    }

    public AdaptiveCategoryAgent(IEnumerable<string> availableCategories, AdaptiveAgentSettings settings, string playerPrefsKey, int randomSeed)
    {
        this.settings = NormalizeSettings(settings);
        prefsKey = string.IsNullOrWhiteSpace(playerPrefsKey) ? DefaultPrefsKey : playerPrefsKey;
        random = new System.Random(randomSeed);

        if (availableCategories != null)
        {
            foreach (string rawCategory in availableCategories)
            {
                string category = (rawCategory ?? string.Empty).Trim();
                if (category.Length == 0 || byCategory.ContainsKey(category)) continue;
                var stats = new AdaptiveCategoryStats { category = category };
                categories.Add(stats);
                byCategory.Add(category, stats);
            }
        }

        LoadMemory();
        RecalculateWeights();
    }

    public void RecordAnswer(string category, bool correct)
    {
        if (string.IsNullOrWhiteSpace(category) || !byCategory.TryGetValue(category.Trim(), out var stats)) return;

        stats.attempts++;
        if (correct) stats.correct++;
        else stats.incorrect++;
        stats.recentResults.Add(correct);
        while (stats.recentResults.Count > settings.recentWindow) stats.recentResults.RemoveAt(0);
        RecalculateWeights();
        SaveMemory();
    }

    public float GetWeight(string category)
    {
        return byCategory.TryGetValue(category ?? string.Empty, out var stats) ? stats.weight : 0f;
    }

    public int GetAttempts(string category) => byCategory.TryGetValue(category ?? string.Empty, out var stats) ? stats.attempts : 0;
    public int GetCorrect(string category) => byCategory.TryGetValue(category ?? string.Empty, out var stats) ? stats.correct : 0;
    public int GetIncorrect(string category) => byCategory.TryGetValue(category ?? string.Empty, out var stats) ? stats.incorrect : 0;
    public bool WasCaseRecentlySeen(string caseId) => !string.IsNullOrWhiteSpace(caseId) && recentCaseSet.Contains(caseId.Trim());

    public void RecordCaseSeen(string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseId)) return;
        caseId = caseId.Trim();
        if (recentCaseSet.Remove(caseId)) recentCaseIds.Remove(caseId);
        recentCaseIds.Add(caseId);
        recentCaseSet.Add(caseId);
        while (recentCaseIds.Count > settings.recentCaseMemory)
        {
            recentCaseSet.Remove(recentCaseIds[0]);
            recentCaseIds.RemoveAt(0);
        }
        SaveMemory();
    }

    public AdaptiveCategorySelection SelectCategory(IEnumerable<string> availableCategories)
    {
        var candidates = new List<AdaptiveCategoryStats>();
        if (availableCategories != null)
        {
            foreach (string rawCategory in availableCategories)
            {
                if (!byCategory.TryGetValue((rawCategory ?? string.Empty).Trim(), out var stats) || candidates.Contains(stats)) continue;
                candidates.Add(stats);
            }
        }

        if (candidates.Count == 0) return new AdaptiveCategorySelection(string.Empty, 0f, "No valid category is available.");

        bool repeatCanBeDamped = candidates.Count > 1 && !string.IsNullOrEmpty(lastCategory);
        double totalWeight = 0d;
        foreach (var stats in candidates) totalWeight += EffectiveWeight(stats, repeatCanBeDamped);
        double roll = random.NextDouble() * totalWeight;
        AdaptiveCategoryStats selected = candidates[candidates.Count - 1];
        foreach (var stats in candidates)
        {
            roll -= EffectiveWeight(stats, repeatCanBeDamped);
            if (roll < 0d) { selected = stats; break; }
        }

        float effectiveWeight = EffectiveWeight(selected, repeatCanBeDamped);
        string reason = BuildPriorityReason(selected, selected.category == lastCategory && repeatCanBeDamped);
        lastCategory = selected.category;
        lastSelection = selected.category + " (priority " + effectiveWeight.ToString("0.00") + "): " + reason;
        return new AdaptiveCategorySelection(selected.category, effectiveWeight, reason);
    }

    /// <summary>Selects an existing case while preserving SUS/LEGIT shift quotas where possible.</summary>
    public CaseData SelectCase(IEnumerable<CaseData> liveCases, IEnumerable<CaseData> fallbackCases, ISet<string> usedCaseIds, int susRemaining, int legitRemaining)
    {
        List<CaseData> available = BuildCandidates(liveCases, fallbackCases, usedCaseIds);
        if (available.Count == 0) return null;

        var nonRecent = available.FindAll(item => !WasCaseRecentlySeen(item.id));
        if (CanFill(nonRecent, susRemaining, legitRemaining)) available = nonRecent;

        List<CaseData> eligible = FindQuotaSafeCandidates(available, Math.Max(0, susRemaining), Math.Max(0, legitRemaining));
        if (eligible.Count == 0) eligible = available;
        if (eligible.Count == 0) return null;

        List<string> candidateCategories = UniqueCategories(eligible);
        AdaptiveCategorySelection selection = SelectCategory(candidateCategories);
        List<CaseData> sameCategory = eligible.FindAll(item => item.category.Trim() == selection.category);
        if (sameCategory.Count == 0) return null;

        List<CaseData> liveInCategory = sameCategory.FindAll(item => item.productImage != null && item.adImage != null);
        List<CaseData> pool = liveInCategory.Count > 0 ? liveInCategory : sameCategory;
        CaseData selectedCase = pool[random.Next(pool.Count)];
        lastSelection = selection.category + " (priority " + selection.weight.ToString("0.00") + "): " + selection.reason;
        RecordCaseSeen(selectedCase.id);
        return selectedCase;
    }

    public string GetDiagnostics()
    {
        var output = new StringBuilder("Adaptive category memory (local aggregate statistics)");
        foreach (var stats in categories)
        {
            output.Append("\n").Append(stats.category)
                .Append(": attempts=").Append(stats.attempts)
                .Append(", correct=").Append(stats.correct)
                .Append(", incorrect=").Append(stats.incorrect)
                .Append(", accuracy=").Append(stats.attempts == 0 ? "--" : (100f * stats.correct / stats.attempts).ToString("0") + "%")
                .Append(", recent=").Append(stats.recentResults.Count == 0 ? "--" : RecentAccuracy(stats).ToString("0%"))
                .Append(", weight=").Append(stats.weight.ToString("0.00"));
        }

        AdaptiveCategoryStats mostMissed = null;
        foreach (var stats in categories)
            if (stats.incorrect > 0 && (mostMissed == null || stats.incorrect > mostMissed.incorrect)) mostMissed = stats;

        output.Append("\nMost frequently missed: ").Append(mostMissed == null ? "none yet" : mostMissed.category + " (" + mostMissed.incorrect + ")")
            .Append("\nLast selected: ").Append(lastSelection);
        return output.ToString();
    }

    List<CaseData> BuildCandidates(IEnumerable<CaseData> liveCases, IEnumerable<CaseData> fallbackCases, ISet<string> usedCaseIds)
    {
        var candidates = new List<CaseData>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        AddCandidates(liveCases, candidates, ids, usedCaseIds);
        AddCandidates(fallbackCases, candidates, ids, usedCaseIds);
        return candidates;
    }

    void AddCandidates(IEnumerable<CaseData> source, List<CaseData> candidates, HashSet<string> ids, ISet<string> usedCaseIds)
    {
        if (source == null) return;
        foreach (CaseData item in source)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id) || string.IsNullOrWhiteSpace(item.category)) continue;
            string id = item.id.Trim();
            if (!byCategory.ContainsKey(item.category.Trim()) || ids.Contains(id) || (usedCaseIds != null && (usedCaseIds.Contains(id) || usedCaseIds.Contains(item.id)))) continue;
            ids.Add(id);
            candidates.Add(item);
        }
    }

    List<CaseData> FindQuotaSafeCandidates(List<CaseData> available, int susRemaining, int legitRemaining)
    {
        if (susRemaining + legitRemaining == 0) return available;
        var quotaMatches = available.FindAll(item => item.isSus ? susRemaining > 0 : legitRemaining > 0);
        if (quotaMatches.Count == 0) return available;

        if (!CanFill(available, susRemaining, legitRemaining)) return quotaMatches;
        var safe = new List<CaseData>();
        foreach (CaseData item in quotaMatches)
        {
            int nextSus = susRemaining - (item.isSus ? 1 : 0);
            int nextLegit = legitRemaining - (item.isSus ? 0 : 1);
            var remaining = available.FindAll(candidate => !ReferenceEquals(candidate, item));
            if (CanFill(remaining, nextSus, nextLegit)) safe.Add(item);
        }
        return safe.Count > 0 ? safe : quotaMatches;
    }

    static bool CanFill(List<CaseData> cases, int susRemaining, int legitRemaining)
    {
        if (cases == null) return false;
        int sus = 0, legit = 0;
        foreach (CaseData item in cases) if (item.isSus) sus++; else legit++;
        return sus >= Math.Max(0, susRemaining) && legit >= Math.Max(0, legitRemaining);
    }

    List<string> UniqueCategories(List<CaseData> cases)
    {
        var result = new List<string>();
        foreach (CaseData item in cases)
        {
            string category = item.category.Trim();
            if (!result.Contains(category)) result.Add(category);
        }
        return result;
    }

    float EffectiveWeight(AdaptiveCategoryStats stats, bool repeatCanBeDamped)
    {
        float weight = stats.weight;
        if (repeatCanBeDamped && stats.category == lastCategory)
            weight *= Mathf.Clamp(settings.repeatCategoryFactor, 0f, 1f);
        return Mathf.Max(settings.minimumWeight * .05f, weight);
    }

    string BuildPriorityReason(AdaptiveCategoryStats stats, bool dampedForRepeat)
    {
        string reason;
        if (stats.attempts == 0) reason = "balanced starting weight";
        else if (stats.weight > 1.001f) reason = stats.incorrect + " missed of " + stats.attempts + " attempts";
        else if (stats.weight < .999f) reason = "correct answers lowered priority (" + stats.correct + " of " + stats.attempts + ")";
        else reason = "performance is near the balanced baseline";
        if (stats.attempts > 0)
            reason += "; recent accuracy " + RecentAccuracy(stats).ToString("P0") + " across " + stats.recentResults.Count + " recent answers";
        if (dampedForRepeat) reason += "; repeat-category dampening applied";
        return reason;
    }

    float RecentAccuracy(AdaptiveCategoryStats stats)
    {
        if (stats.recentResults.Count == 0) return 0f;
        int correct = 0;
        foreach (bool result in stats.recentResults) if (result) correct++;
        return (float)correct / stats.recentResults.Count;
    }

    void RecalculateWeights()
    {
        foreach (AdaptiveCategoryStats stats in categories)
        {
            if (stats.attempts == 0) { stats.weight = 1f; continue; }
            float historicalAccuracy = (stats.correct + 2f) / (stats.attempts + 4f);
            int recentCorrect = 0;
            foreach (bool result in stats.recentResults) if (result) recentCorrect++;
            float recentAccuracy = (recentCorrect + 2f) / (stats.recentResults.Count + 4f);
            float blendedAccuracy = (historicalAccuracy + recentAccuracy) * .5f;
            stats.weight = Mathf.Clamp(1f + settings.weightSensitivity * (.5f - blendedAccuracy), settings.minimumWeight, settings.maximumWeight);
            stats.weight = Mathf.Max(.0001f, stats.weight);
        }
    }

    void LoadMemory()
    {
        if (!PlayerPrefs.HasKey(prefsKey)) return;
        AdaptiveAgentMemory memory = null;
        try { memory = JsonUtility.FromJson<AdaptiveAgentMemory>(PlayerPrefs.GetString(prefsKey, string.Empty)); }
        catch (Exception) { }
        if (memory == null) return;

        var loadedCategories = new HashSet<string>(StringComparer.Ordinal);
        if (memory.categories != null)
        {
            foreach (AdaptiveCategoryStats saved in memory.categories)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.category)) continue;
                string category = saved.category.Trim();
                if (loadedCategories.Contains(category) || !byCategory.TryGetValue(category, out var stats)) continue;
                if (saved.attempts < 0 || saved.correct < 0 || saved.incorrect < 0 || saved.correct > saved.attempts || saved.incorrect != saved.attempts - saved.correct)
                    continue;
                loadedCategories.Add(category);
                stats.attempts = saved.attempts;
                stats.correct = saved.correct;
                stats.incorrect = saved.incorrect;
                stats.recentResults = SanitizeRecentResults(saved.recentResults, stats.attempts);
            }
        }

        if (memory.recentCaseIds != null)
        {
            foreach (string rawId in memory.recentCaseIds)
            {
                if (string.IsNullOrWhiteSpace(rawId)) continue;
                string id = rawId.Trim();
                if (!recentCaseSet.Add(id)) continue;
                recentCaseIds.Add(id);
            }
        }
        while (recentCaseIds.Count > settings.recentCaseMemory)
        {
            recentCaseSet.Remove(recentCaseIds[0]);
            recentCaseIds.RemoveAt(0);
        }
        if (!string.IsNullOrWhiteSpace(memory.lastCategory) && byCategory.ContainsKey(memory.lastCategory.Trim())) lastCategory = memory.lastCategory.Trim();
        if (!string.IsNullOrWhiteSpace(memory.lastSelection) && memory.lastSelection.Length <= 256) lastSelection = memory.lastSelection;
    }

    List<bool> SanitizeRecentResults(List<bool> saved, int attempts)
    {
        var results = saved == null ? new List<bool>() : new List<bool>(saved);
        if (results.Count > attempts) results.RemoveRange(0, results.Count - attempts);
        if (results.Count > settings.recentWindow) results.RemoveRange(0, results.Count - settings.recentWindow);
        return results;
    }

    void SaveMemory()
    {
        var memory = new AdaptiveAgentMemory
        {
            categories = categories,
            recentCaseIds = recentCaseIds,
            lastCategory = lastCategory,
            lastSelection = lastSelection
        };
        PlayerPrefs.SetString(prefsKey, JsonUtility.ToJson(memory));
        PlayerPrefs.Save();
    }

    static AdaptiveAgentSettings NormalizeSettings(AdaptiveAgentSettings source)
    {
        var value = source ?? new AdaptiveAgentSettings();
        float minimum = IsFinite(value.minimumWeight) && value.minimumWeight > 0f ? value.minimumWeight : .5f;
        float maximum = IsFinite(value.maximumWeight) && value.maximumWeight >= minimum ? value.maximumWeight : Mathf.Max(2f, minimum);
        return new AdaptiveAgentSettings
        {
            minimumWeight = Mathf.Clamp(minimum, .01f, 100f),
            maximumWeight = Mathf.Clamp(maximum, minimum, 100f),
            weightSensitivity = IsFinite(value.weightSensitivity) && value.weightSensitivity >= 0f ? Mathf.Min(value.weightSensitivity, 100f) : 2f,
            recentWindow = Mathf.Clamp(value.recentWindow, 1, 100),
            recentCaseMemory = Mathf.Clamp(value.recentCaseMemory, 1, 500),
            repeatCategoryFactor = IsFinite(value.repeatCategoryFactor) ? Mathf.Clamp01(value.repeatCategoryFactor) : .35f
        };
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
