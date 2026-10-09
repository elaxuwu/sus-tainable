using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class AdaptiveAgentTests
{
    static readonly string[] Categories = { "drinks", "cosmetics", "fashion", "snacks", "tech" };
    static readonly List<string> testKeys = new List<string>();

    [MenuItem("Sus-tainable/Verify Adaptive Agent")]
    static void RunAll()
    {
        testKeys.Clear();
        try
        {
            NewPlayersAreBalanced();
            MistakesRaiseCategoryPriority();
            CorrectAnswersLowerCategoryPriority();
            CategoryWeightsMoveGradually();
            EveryCategoryRemainsSelectable();
            CasesAreNotRepeatedWhenAlternativesExist();
            CaseAvoidanceRelaxesWhenRequired();
            MemoryPersistsAndInvalidHistoryIsSafe();
            LiveCasesKeepTheirGeneratedImages();
            DiagnosticsExplainCategoryPriority();
            DiagnosticsDistinguishNoAttemptsFromNoCorrectAnswers();
            ReadyLiveCasesCannotStarve();
            EvidenceMasteryDrivesLearning();
            WeakTellTypesReceivePractice();
            Debug.Log("PASS: adaptive category/evidence mastery, persistence, quota-safe live priority, and case reuse.");
        }
        catch (Exception exception)
        {
            Debug.LogError("Adaptive Agent verification failed: " + exception);
            throw;
        }
        finally
        {
            foreach (string key in testKeys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    static void NewPlayersAreBalanced()
    {
        var agent = CreateAgent("balanced", 7341, repeatPenalty: 1f);
        var counts = DrawCategories(agent, 25000);
        foreach (string category in Categories)
        {
            Require(agent.GetWeight(category) == 1f, "New-player weights should start at one.");
            Require(counts[category] > 4500 && counts[category] < 5500, "New-player distribution was not balanced for " + category + ".");
        }
    }

    static void MistakesRaiseCategoryPriority()
    {
        var agent = CreateAgent("mistakes", 191, repeatPenalty: 1f);
        for (int i = 0; i < 8; i++) agent.RecordAnswer("drinks", false);

        Require(agent.GetWeight("drinks") > agent.GetWeight("fashion"), "Repeated mistakes should increase category weight.");
        var counts = DrawCategories(agent, 20000);
        int otherAverage = (counts["cosmetics"] + counts["fashion"] + counts["snacks"] + counts["tech"]) / 4;
        Require(counts["drinks"] > otherAverage * 1.2f, "Repeated mistakes should increase category selection frequency.");
    }

    static void CorrectAnswersLowerCategoryPriority()
    {
        var agent = CreateAgent("correct", 519, repeatPenalty: 1f);
        for (int i = 0; i < 8; i++) agent.RecordAnswer("drinks", true);

        Require(agent.GetWeight("drinks") < agent.GetWeight("fashion"), "Repeated correct answers should lower category weight.");
        var counts = DrawCategories(agent, 20000);
        int otherAverage = (counts["cosmetics"] + counts["fashion"] + counts["snacks"] + counts["tech"]) / 4;
        Require(counts["drinks"] < otherAverage * .85f, "Repeated correct answers should lower category selection frequency.");
    }

    static void CategoryWeightsMoveGradually()
    {
        var mistakes = CreateAgent("gradual-mistakes", 720);
        float previous = mistakes.GetWeight("drinks");
        for (int i = 0; i < 8; i++)
        {
            mistakes.RecordAnswer("drinks", false);
            float current = mistakes.GetWeight("drinks");
            Require(current >= previous && current <= 2f, "Mistakes should raise weight monotonically within its configured bounds.");
            previous = current;
        }

        var improvement = CreateAgent("gradual-improvement", 721);
        previous = improvement.GetWeight("drinks");
        for (int i = 0; i < 8; i++)
        {
            improvement.RecordAnswer("drinks", true);
            float current = improvement.GetWeight("drinks");
            Require(current <= previous && current >= .5f, "Correct answers should lower weight monotonically within its configured bounds.");
            previous = current;
        }
    }

    static void EveryCategoryRemainsSelectable()
    {
        var agent = CreateAgent("nonzero", 9923, repeatPenalty: .35f);
        for (int i = 0; i < 30; i++) agent.RecordAnswer("drinks", false);
        for (int i = 0; i < 30; i++) agent.RecordAnswer("fashion", true);

        var counts = DrawCategories(agent, 15000);
        foreach (string category in Categories)
        {
            Require(agent.GetWeight(category) > 0f, "Category weight must stay positive for " + category + ".");
            Require(counts[category] > 0, "Category must remain selectable: " + category + ".");
        }
    }

    static void CasesAreNotRepeatedWhenAlternativesExist()
    {
        var agent = CreateAgent("case-reuse", 61, repeatPenalty: .35f, recentCases: 20);
        var cases = MakeCases();
        var used = new HashSet<string>();
        int susRemaining = 6, legitRemaining = 4;
        for (int i = 0; i < 10; i++)
        {
            CaseData selected = agent.SelectCase(Array.Empty<CaseData>(), cases, used, susRemaining, legitRemaining);
            Require(selected != null, "The first shift should have a case for every slot.");
            Require(used.Add(selected.id), "A case repeated within one shift: " + selected.id + ".");
            if (selected.isSus) susRemaining--; else legitRemaining--;
        }
        Require(susRemaining == 0 && legitRemaining == 0, "The first shift should preserve the 6 SUS / 4 LEGIT composition.");

        used.Clear();
        susRemaining = 6;
        legitRemaining = 4;
        for (int i = 0; i < 10; i++)
        {
            var freshIds = new HashSet<string>(cases.Where(item => !agent.WasCaseRecentlySeen(item.id)).Select(item => item.id));
            CaseData selected = agent.SelectCase(Array.Empty<CaseData>(), cases, used, susRemaining, legitRemaining);
            Require(selected != null, "The second shift should have enough non-recent cases.");
            Require(used.Add(selected.id), "A case repeated within the second shift: " + selected.id + ".");
            Require(freshIds.Contains(selected.id), "A recent case repeated despite sufficient alternatives: " + selected.id + ".");
            if (selected.isSus) susRemaining--; else legitRemaining--;
        }
        Require(susRemaining == 0 && legitRemaining == 0, "The second shift should preserve the 6 SUS / 4 LEGIT composition.");
    }

    static void CaseAvoidanceRelaxesWhenRequired()
    {
        var agent = CreateAgent("case-avoidance-relax", 93, recentCases: 20);
        var onlyCase = new CaseData
        {
            id = "only-available-case",
            product = "Test",
            category = "drinks",
            adText = "Claim",
            verdictText = "Evidence",
            tells = new CaseTell[0],
            isSus = true
        };
        agent.RecordCaseSeen(onlyCase.id);
        CaseData selected = agent.SelectCase(Array.Empty<CaseData>(), new[] { onlyCase }, new HashSet<string>(), 1, 0);
        Require(ReferenceEquals(selected, onlyCase), "Recent-case avoidance should relax instead of returning an empty pool when repetition is unavoidable.");
    }

    static void MemoryPersistsAndInvalidHistoryIsSafe()
    {
        string key = NewKey("persist");
        var first = new AdaptiveCategoryAgent(Categories, Settings(), key, 88);
        first.RecordAnswer("drinks", false);
        first.RecordAnswer("drinks", true);
        first.RecordCaseSeen("local_00");
        float expectedWeight = first.GetWeight("drinks");

        var restored = new AdaptiveCategoryAgent(Categories, Settings(), key, 88);
        Require(restored.GetAttempts("drinks") == 2 && restored.GetCorrect("drinks") == 1 && restored.GetIncorrect("drinks") == 1,
            "Category totals should survive reconstruction.");
        Require(restored.WasCaseRecentlySeen("local_00"), "Recently seen case ids should survive reconstruction.");
        Require(Mathf.Approximately(restored.GetWeight("drinks"), expectedWeight), "The adaptive weight should survive reconstruction.");

        string invalidKey = NewKey("invalid-history");
        PlayerPrefs.SetString(invalidKey, "{\"categories\":[{\"category\":\"drinks\",\"attempts\":-9,\"correct\":100,\"recentResults\":null}],\"recentCaseIds\":[\"\",\"seen\",\"seen\"],\"lastCategory\":\"unknown\"}");
        var recovered = new AdaptiveCategoryAgent(Categories, Settings(), invalidKey, 7);
        Require(recovered.GetAttempts("drinks") == 0 && recovered.GetCorrect("drinks") == 0 && recovered.GetIncorrect("drinks") == 0,
            "Invalid totals should be reset safely.");
        Require(recovered.GetWeight("drinks") == 1f, "Invalid history should fall back to balanced weight.");
        Require(recovered.WasCaseRecentlySeen("seen"), "Valid recent ids should survive invalid neighboring entries.");

        string malformedKey = NewKey("malformed-history");
        PlayerPrefs.SetString(malformedKey, "not json");
        var malformed = new AdaptiveCategoryAgent(Categories, Settings(), malformedKey, 5);
        Require(malformed.GetWeight("drinks") == 1f && malformed.GetAttempts("drinks") == 0,
            "Malformed persisted JSON should start with safe defaults.");
    }

    static void LiveCasesKeepTheirGeneratedImages()
    {
        var agent = CreateAgent("live", 17);
        var generated = new CaseData { id = "generated-1", product = "Demo", category = "drinks", adText = "Claim", verdictText = "Evidence", tells = new CaseTell[0], isSus = true };
        var productImage = new Texture2D(1, 1);
        var adImage = new Texture2D(1, 1);
        generated.productImage = productImage;
        generated.adImage = adImage;
        try
        {
            CaseData selected = agent.SelectCase(new[] { generated }, Array.Empty<CaseData>(), new HashSet<string>(), 1, 0);
            Require(ReferenceEquals(selected, generated), "Selection should return the existing generated JSON object.");
            Require(selected.productImage == productImage && selected.adImage == adImage, "Adaptive selection must preserve generated image textures.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(productImage);
            UnityEngine.Object.DestroyImmediate(adImage);
        }
    }

    static void DiagnosticsExplainCategoryPriority()
    {
        var agent = CreateAgent("diagnostics", 47);
        agent.RecordAnswer("drinks", false);
        var missedCase = new CaseData
        {
            id = "diagnostic-case",
            product = "Demo",
            category = "drinks",
            adText = "Claim",
            verdictText = "Evidence",
            tells = new CaseTell[0],
            isSus = false
        };
        agent.SelectCase(Array.Empty<CaseData>(), new[] { missedCase }, new HashSet<string>(), 0, 1);
        string diagnostics = agent.GetDiagnostics();
        Require(diagnostics.Contains("drinks: attempts=1, correct=0, incorrect=1"), "Diagnostics should expose category accuracy totals.");
        Require(diagnostics.Contains("Most frequently missed: drinks (1)"), "Diagnostics should identify the most frequently missed category.");
        Require(diagnostics.Contains("Last selected: drinks") && diagnostics.Contains("missed"), "Diagnostics should explain the last category's priority.");
    }

    static void DiagnosticsDistinguishNoAttemptsFromNoCorrectAnswers()
    {
        var agent = CreateAgent("empty-recent", 302);
        Require(agent.GetDiagnostics().Contains("drinks: attempts=0, correct=0, incorrect=0, accuracy=--, recent=--"),
            "Categories with no answers should report recent accuracy as unavailable.");

        agent.RecordAnswer("drinks", false);
        Require(agent.GetDiagnostics().Contains("drinks: attempts=1, correct=0, incorrect=1, accuracy=0%, recent=0%"),
            "A recorded run of incorrect answers should report zero recent accuracy, not unavailable.");
    }

    static Dictionary<string, int> DrawCategories(AdaptiveCategoryAgent agent, int count)
    {
        var counts = new Dictionary<string, int>();
        foreach (string category in Categories) counts[category] = 0;
        for (int i = 0; i < count; i++) counts[agent.SelectCategory(Categories).category]++;
        return counts;
    }

    static void ReadyLiveCasesCannotStarve()
    {
        var cases = MakeCases();
        var image = new Texture2D(1, 1);
        try
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var agent = CreateAgent("live-priority", seed);
                for (int i = 0; i < 20; i++) { agent.RecordAnswer("drinks", true); agent.RecordAnswer("fashion", false); }
                var live = Enumerable.Range(0, 5).Select(i => new CaseData { id = "live-" + i, category = "drinks", isSus = true, tells = new CaseTell[0], productImage = image, adImage = image }).ToList();
                agent.RecordCaseSeen(live[0].id);
                var used = new HashSet<string>(); int sus = 6, legit = 4;
                for (int i = 0; i < 10; i++)
                {
                    var selected = agent.SelectCase(live, cases, used, sus, legit);
                    Require(selected != null && used.Add(selected.id), "A full shift must remain available without duplicates.");
                    if (i < 5) Require(live.Contains(selected), "Ready quota-safe live cases must precede fallback.");
                    live.Remove(selected); if (selected.isSus) sus--; else legit--;
                }
                Require(sus == 0 && legit == 0 && live.Count == 0, "Live priority must preserve the 6/4 quotas.");
                var blocked = new CaseData { id = "extra-sus", category = "drinks", isSus = true, tells = new CaseTell[0], productImage = image, adImage = image };
                Require(!agent.SelectCase(new[] { blocked }, cases, new HashSet<string>(), 0, 1).isSus, "Live SUS cannot displace a required LEGIT slot.");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(image); }
    }

    static CaseData LearningCase(string type = "vague") => new CaseData {
        id = "learning", category = "drinks", product = "Demo", adText = "Green claim", isSus = true,
        tells = new[] { new CaseTell { phrase = "Green", type = type } }, verdictText = "Reason"
    };

    static void EvidenceMasteryDrivesLearning()
    {
        string key = NewKey("evidence-persist");
        var agent = new AdaptiveCategoryAgent(Categories, Settings(), key, 42);
        var data = LearningCase();
        agent.RecordAnswer(data, true, new List<string>());
        Require(agent.GetCorrect("drinks") == 0 && agent.GetTellWeight("vague") > 1, "Correct verdict without evidence is not mastery.");
        agent.RecordAnswer(data, true, new[] { "Green", "claim" });
        Require(agent.GetCorrect("drinks") == 0, "False evidence cannot count as mastery.");
        agent.RecordAnswer(data, true, new[] { "Green" });
        Require(agent.GetCorrect("drinks") == 1 && agent.GetTellAttempts("vague") == 3, "Complete correct evidence records mastery.");
        agent.RecordAnswer(data, false, new[] { "Green" });
        Require(agent.GetCorrect("drinks") == 1, "Wrong verdict cannot record mastery.");
        var restored = new AdaptiveCategoryAgent(Categories, Settings(), key, 42);
        Require(restored.GetTellAttempts("vague") == 4 && Mathf.Approximately(restored.GetTellWeight("vague"), agent.GetTellWeight("vague")), "Tell memory must persist.");
        var legit = new CaseData { category = "drinks", tells = new CaseTell[0], isSus = false };
        agent.RecordAnswer(legit, true, new[] { "false alarm" });
        Require(agent.GetCorrect("drinks") == 1, "False highlights on LEGIT are not mastery.");
        agent.RecordAnswer(legit, true, Array.Empty<string>());
        Require(agent.GetCorrect("drinks") == 2, "Clean LEGIT verdict counts as mastery.");
    }

    static void WeakTellTypesReceivePractice()
    {
        var agent = CreateAgent("tell-practice", 81, repeatPenalty: 1);
        for (int i = 0; i < 20; i++) {
            agent.RecordAnswer(LearningCase("vague"), true, Array.Empty<string>());
            agent.RecordAnswer(LearningCase("no_proof"), true, new[] { "Green" });
        }
        Require(agent.GetTellWeight("vague") > agent.GetTellWeight("no_proof"), "Missed tells should have higher priority than mastered tells.");
        var weak = LearningCase("vague"); weak.id = "weak";
        var mastered = LearningCase("no_proof"); mastered.id = "mastered";
        int weakCount = 0;
        for (int i = 0; i < 3000; i++) if (agent.SelectCase(Array.Empty<CaseData>(), new[] { weak, mastered }, new HashSet<string>(), 1, 0) == weak) weakCount++;
        Require(weakCount > 1800, "Selection should provide more practice for the missed tell type.");
    }

    static List<CaseData> MakeCases()
    {
        var cases = new List<CaseData>();
        for (int i = 0; i < 30; i++)
        {
            cases.Add(new CaseData
            {
                id = "test_" + i,
                product = "Test " + i,
                category = Categories[i % Categories.Length],
                adText = "Claim",
                verdictText = "Evidence",
                tells = new CaseTell[0],
                isSus = i < 20
            });
        }
        return cases;
    }

    static AdaptiveCategoryAgent CreateAgent(string suffix, int seed, float repeatPenalty = .35f, int recentCases = 20)
    {
        return new AdaptiveCategoryAgent(Categories, Settings(repeatPenalty, recentCases), NewKey(suffix), seed);
    }

    static AdaptiveAgentSettings Settings(float repeatPenalty = .35f, int recentCases = 20)
    {
        return new AdaptiveAgentSettings
        {
            minimumWeight = .5f,
            maximumWeight = 2f,
            weightSensitivity = 2f,
            recentWindow = 5,
            recentCaseMemory = recentCases,
            repeatCategoryFactor = repeatPenalty
        };
    }

    static string NewKey(string suffix)
    {
        string key = "SusTainable.AdaptiveAgent.Test." + suffix + "." + Guid.NewGuid().ToString("N");
        testKeys.Add(key);
        PlayerPrefs.DeleteKey(key);
        return key;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
