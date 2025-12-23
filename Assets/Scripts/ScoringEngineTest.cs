using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public static class PhonemeScoringEngine
{
    // Weight configuration (0–1, sum does NOT need to equal 1)
    private const float MAIN_WEIGHT       = 0.70f;  // phoneme Levenshtein
    private const float VOWEL_WEIGHT      = 0.15f;  // vowel similarity
    private const float LENGTH_WEIGHT     = 0.15f;  // phoneme length penalty

    // For vowel detection ("ah", "eh", "ee", "ay", etc.)
    // Must match your vocab mapping.
    private static readonly HashSet<string> Vowels = new()
    {
        "a","aw","ay","e","ee","i","o","oau","oh","oi","oo","or","u","uoh"
    };

    // MAIN ENTRY FUNCTION
    public static float CalculateSimilarity(string spokenPhonemes, string targetPhonemes)
    {
        var spokenTokens = Tokenize(spokenPhonemes);
        var targetTokens = Tokenize(targetPhonemes);

        if (spokenTokens.Length == 0 || targetTokens.Length == 0)
            return 0f;

        float editSim  = PhonemeEditSimilarity(spokenTokens, targetTokens);     // 0–1
        float vowelSim = VowelSimilarity(spokenTokens, targetTokens);           // 0–1
        float lenPen   = LengthPenalty(spokenTokens, targetTokens);             // 0–1

        float score =
            editSim  * MAIN_WEIGHT +
            vowelSim * VOWEL_WEIGHT +
            lenPen   * LENGTH_WEIGHT;

        Debug.Log($"Phoneme Edit Similarity: {editSim:F3}");
        Debug.Log($"Vowel Similarity: {vowelSim:F3}");
        Debug.Log($"Length Penalty: {lenPen:F3}");
        Debug.Log($"Final Score (0–100): {score * 100f:F3}");

        return score * 100f; // Already in range 0–100
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

        if (ratio < 0.5f)
            return 0.4f;
        if (ratio > 1.8f)
            return 0.4f;

        return 1f - Math.Abs(1f - ratio) * 0.5f;
    }
}
