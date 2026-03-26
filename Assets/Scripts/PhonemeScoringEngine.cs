using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public static class PhonemeScoringEngine
{
    private const string LogSource = "PhonemeScoringEngine.cs";

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

        float editSim  = PhonemeEditSimilarity(spokenTokens, targetTokens);
        float vowelSim = VowelSimilarity(spokenTokens, targetTokens);
        float lenPen   = LengthPenalty(spokenTokens, targetTokens);

        float score =
            editSim  * BackendConfig.Scoring.MainWeight +
            vowelSim * BackendConfig.Scoring.VowelWeight +
            lenPen   * BackendConfig.Scoring.LengthWeight;


        float finalScore = score * BackendConfig.Scoring.ScoreScale;
        finalScore = Mathf.Clamp(finalScore, 0f,100f);

        BackendLogger.Info(
            LogSource,
            "SimilarityComputed",
            $"spokenTokens={spokenTokens.Length}, targetTokens={targetTokens.Length}, editSim={editSim:F3}, vowelSim={vowelSim:F3}, lengthPenalty={lenPen:F3}, score={score:F3}, finalScore={finalScore:F3}, scoreScale={BackendConfig.Scoring.ScoreScale:F1}"
        );

        return finalScore;
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
        float dist = TokenLevenshtein(s, t);
        int maxLen = Math.Max(s.Length, t.Length);

        if (maxLen == 0)
            return 1f;

        return 1f - dist / maxLen;
    }

    private static float TokenLevenshtein(string[] s, string[] t)
    {
        int n = s.Length;
        int m = t.Length;
        float[,] d = new float[n + 1, m + 1];

        for (int i = 0; i <= n; i++)
            d[i, 0] = i;
        for (int j = 0; j <= m; j++)
            d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                float cost = GetSubstitutionCost(s[i - 1], t[j - 1]);
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 0.8f, d[i, j - 1] + 0.8f),
                    d[i - 1, j - 1] + cost
                );
            }
        }

        return d[n, m];
    }

    // ---------------------------------------------------------
    // SUBSTITUTION COST
    // ---------------------------------------------------------
    private static float GetSubstitutionCost(string a, string b)
    {
        if (a == b) return 0f;

        bool aIsVowel = Vowels.Contains(a);
        bool bIsVowel = Vowels.Contains(b);

        // Cross vowel/consonant: max penalty
        if (aIsVowel != bIsVowel) return 1f;

        if (aIsVowel)
            return AreSimilarVowels(a, b) ? 0.3f : 0.7f;

        return AreSimilarConsonants(a, b) ? 0.3f : 0.6f;
    }

    private static bool AreSimilarVowels(string a, string b)
    {
        string[][] groups = {
            new[] { "ay", "ee", "i" },
            new[] { "oh", "oo", "aw" },
            new[] { "a", "e", "u" },
            new[] { "or", "oau", "oi" },
            new[] { "uoh", "u" },
        };

        foreach (var group in groups)
            if (group.Contains(a) && group.Contains(b)) return true;

        return false;
    }

    private static bool AreSimilarConsonants(string a, string b)
    {
        string[][] groups = {
            new[] { "b", "p" },
            new[] { "d", "t" },
            new[] { "g", "k" },
            new[] { "v", "f" },
            new[] { "z", "s" },
            new[] { "th", "s", "z" },
            new[] { "sh", "ch" },
            new[] { "j", "ch" },
            new[] { "m", "n", "ng" },
            new[] { "w", "v" },
            new[] { "r", "l" },
            new[] { "h", "f" },
        };

        foreach (var group in groups)
            if (group.Contains(a) && group.Contains(b)) return true;

        return false;
    }

    // ---------------------------------------------------------
    // VOWEL SIMILARITY
    // ---------------------------------------------------------
    private static float VowelSimilarity(string[] s, string[] t)
    {
        var sVowels = s.Where(p => Vowels.Contains(p)).ToList();
        var tVowels = t.Where(p => Vowels.Contains(p)).ToList();

        if (sVowels.Count == 0 && tVowels.Count == 0)
            return 1f;

        if (sVowels.Count == 0 || tVowels.Count == 0)
            return 0f;

        int matchCount = 0;
        int min = Math.Min(sVowels.Count, tVowels.Count);

        for (int i = 0; i < min; i++)
            if (sVowels[i] == tVowels[i])
                matchCount++;

        // Penalize length mismatch in vowel sequences too
        int max = Math.Max(sVowels.Count, tVowels.Count);
        return (float)matchCount / max;
    }

    // ---------------------------------------------------------
    // LENGTH PENALTY
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