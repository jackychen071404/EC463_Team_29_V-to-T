using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public static class PhonemeScoringEngine
{
    private const string LogSource = "ScoringEngineTest.cs";
    // For vowel detection ("ah", "eh", "ee", "ay", etc.)
    // Must match your vocab mapping.
    private static readonly HashSet<string> Vowels = new(BackendConfig.Scoring.Vowels);

    // MAIN ENTRY FUNCTION
    public static float CalculateSimilarity(string spokenPhonemes, string targetPhonemes)
    {
        var spokenTokens = Tokenize(spokenPhonemes);
        var targetTokens = Tokenize(targetPhonemes);

        if (spokenTokens.Length == 0 || targetTokens.Length == 0)
        {
            BackendLogger.Warn(LogSource, "SimilaritySkipped", $"reason=empty_tokens, spokenTokenCount={spokenTokens.Length}, targetTokenCount={targetTokens.Length}");
            return 0f;
        }

        float editSim  = PhonemeEditSimilarity(spokenTokens, targetTokens);     // 0–1
        float vowelSim = VowelSimilarity(spokenTokens, targetTokens);           // 0–1
        float lenPen   = LengthPenalty(spokenTokens, targetTokens);             // 0–1

        float score =
            editSim  * BackendConfig.Scoring.MainWeight +
            vowelSim * BackendConfig.Scoring.VowelWeight +
            lenPen   * BackendConfig.Scoring.LengthWeight;

        BackendLogger.Info(
            LogSource,
            "SimilarityComputed",
            $"spokenTokens={spokenTokens.Length}, targetTokens={targetTokens.Length}, editSim={editSim:F3}, vowelSim={vowelSim:F3}, lengthPenalty={lenPen:F3}, finalScore={score * BackendConfig.Scoring.ScoreScale:F3}, scoreScale={BackendConfig.Scoring.ScoreScale:F1}"
        );

        return score * BackendConfig.Scoring.ScoreScale; // Already in range 0–100
    }

    // ---------------------------------------------------------
    // TOKENIZATION
    // ---------------------------------------------------------
    private static string[] Tokenize(string phonemeString)
    {
        if (string.IsNullOrWhiteSpace(phonemeString))
            return Array.Empty<string>();

        return phonemeString
            .Replace("|", "")
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.ToLower())
            .ToArray();
    }

    // ---------------------------------------------------------
    // PHONEME LEVENSHTEIN SIMILARITY
    // ---------------------------------------------------------
    private static float PhonemeEditSimilarity(string[] s, string[] t)
    {
        int dist = TokenLevenshtein(s, t);
        int maxLen = Math.Max(s.Length, t.Length);

        if (maxLen == 0)
            return 1f;

        return 1f - (float)dist / maxLen;
    }

    private static int TokenLevenshtein(string[] s, string[] t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++)
            d[i, 0] = i;
        for (int j = 0; j <= m; j++)
            d[0, j] = j;

        for (int i = 1; i < n + 1; i++)
        {
            for (int j = 1; j < m + 1; j++)
            {
                int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost
                );
            }
        }

        return d[n, m];
    }

    // ---------------------------------------------------------
    // VOWEL SIMILARITY (OPTIONAL BUT VERY USEFUL)
    // ---------------------------------------------------------
    private static float VowelSimilarity(string[] s, string[] t)
    {
        var sVowels = s.Where(p => Vowels.Contains(p)).ToList();
        var tVowels = t.Where(p => Vowels.Contains(p)).ToList();

        if (sVowels.Count == 0 || tVowels.Count == 0)
            return 0f;

        int matchCount = 0;
        int min = Math.Min(sVowels.Count, tVowels.Count);

        for (int i = 0; i < min; i++)
            if (sVowels[i] == tVowels[i])
                matchCount++;

        return (float)matchCount / min;
    }

    // ---------------------------------------------------------
    // LENGTH PENALTY BASED ON PHONEME COUNT
    // ---------------------------------------------------------
    private static float LengthPenalty(string[] s, string[] t)
    {
        float ratio = (float)s.Length / t.Length;

        if (ratio < BackendConfig.Scoring.MinLengthRatio)
            return BackendConfig.Scoring.OutOfRangeLengthPenalty;
        if (ratio > BackendConfig.Scoring.MaxLengthRatio)
            return BackendConfig.Scoring.OutOfRangeLengthPenalty;

        return 1f - Math.Abs(1f - ratio) * BackendConfig.Scoring.RatioPenaltyFactor;
    }
}
