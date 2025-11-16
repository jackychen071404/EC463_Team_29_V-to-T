using System;
using System.Linq;
using System.Text;
using System.IO;

namespace ScoringEngineTest
{
    public class SimilarityScorer
    {
        private static bool EnableDebugLogging = false;
        // Weights for different similarity components (tune these based on testing); all of them need to add 1.0
        private const float LEVENSHTEIN_WEIGHT = 0.3f;
        private const float JARO_WINKLER_WEIGHT = 0.3f;
        private const float PHONETIC_WEIGHT = 0.2f; // twice the weight
        private const float LENGTH_PENALTY_WEIGHT = 0.2f;

        /// <summary>
        /// Calculate overall similarity score between transcribed text and target word
        /// Returns score from 0-100
        /// </summary>
        public static float CalculateSimilarity(string? transcribed, string? target)
        {
            transcribed = transcribed ?? "";
            target = target ?? "";
            if (string.IsNullOrEmpty(transcribed) || string.IsNullOrEmpty(target))
                return 0f;

            // Normalize inputs
            transcribed = transcribed.ToLower().Trim();
            target = target.ToLower().Trim();

            // Handle exact match
            if (transcribed == target)
                return 100f;

            // Calculate different similarity metrics
            float levenshteinSim = LevenshteinSimilarity(transcribed, target);
            float jaroWinklerSim = JaroWinklerSimilarity(transcribed, target);
            float phoneticSim = PhoneticSimilarity(transcribed, target);
            float lengthPenalty = CalculateLengthPenalty(transcribed, target);

            // Use for debugging/tuning; uses column formatting for neatness
            if (EnableDebugLogging)
            {
                using (System.IO.StreamWriter writer = new System.IO.StreamWriter("Logs/SimilarityDebugLog.txt", true))
                {
                    writer.WriteLine(
                        "[INFO {6}]: {0,-10} vs {1,-10} → Lev ={2,6:F2} Jaro ={3,6:F2}  Phonetic ={4,6:F2}  LenPen ={5,6:F2}",
                        transcribed,
                        target,
                        levenshteinSim,
                        jaroWinklerSim,
                        phoneticSim,
                        lengthPenalty,

                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    );
                }
            }
            // Weighted combination
            float score = (levenshteinSim * LEVENSHTEIN_WEIGHT +
                          jaroWinklerSim * JARO_WINKLER_WEIGHT +
                          phoneticSim * PHONETIC_WEIGHT +
                          lengthPenalty * LENGTH_PENALTY_WEIGHT) * 100f;

            return Math.Min(100f, (float)Math.Ceiling(Math.Max(0f, score)));
        }

        #region Levenshtein Distance (Edit Distance)

        /// <summary>
        /// Calculates similarity based on minimum edit operations needed
        /// Good for: typos, missing letters, extra letters
        /// </summary>
        private static float LevenshteinSimilarity(string s1, string s2)
        {
            int distance = LevenshteinDistance(s1, s2);
            int maxLen = Math.Max(s1.Length, s2.Length);

            if (maxLen == 0)
                return 1f;

            return 1f - ((float)distance / maxLen);
        }

        private static int LevenshteinDistance(string s1, string s2)
        {
            int[,] d = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++)
                d[0, j] = j;

            for (int j = 1; j <= s2.Length; j++)
            {
                for (int i = 1; i <= s1.Length; i++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(
                        d[i - 1, j] + 1,      // deletion
                        d[i, j - 1] + 1),     // insertion
                        d[i - 1, j - 1] + cost); // substitution
                }
            }

            return d[s1.Length, s2.Length];
        }

        #endregion

        #region Jaro-Winkler Similarity

        /// <summary>
        /// Calculates similarity with emphasis on matching prefixes
        /// Good for: partial attempts, words that start correctly
        /// </summary>
        private static float JaroWinklerSimilarity(string s1, string s2)
        {
            int s1Len = s1.Length;
            int s2Len = s2.Length;

            if (s1Len == 0 && s2Len == 0)
                return 1f;
            if (s1Len == 0 || s2Len == 0)
                return 0f;

            int matchDistance = Math.Max(s1Len, s2Len) / 2 - 1;
            bool[] s1Matches = new bool[s1Len];
            bool[] s2Matches = new bool[s2Len];

            int matches = 0;
            int transpositions = 0;

            // Find matches
            for (int i = 0; i < s1Len; i++)
            {
                int start = Math.Max(0, i - matchDistance);
                int end = Math.Min(i + matchDistance + 1, s2Len);

                for (int j = start; j < end; j++)
                {
                    if (s2Matches[j] || s1[i] != s2[j])
                        continue;
                    s1Matches[i] = true;
                    s2Matches[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0)
                return 0f;

            // Find transpositions
            int k = 0;
            for (int i = 0; i < s1Len; i++)
            {
                if (!s1Matches[i])
                    continue;
                while (!s2Matches[k])
                    k++;
                if (s1[i] != s2[k])
                    transpositions++;
                k++;
            }

            float jaro = ((float)matches / s1Len +
                         (float)matches / s2Len +
                         (float)(matches - transpositions / 2) / matches) / 3f;

            // Jaro-Winkler modification (boost for common prefix)
            int prefix = 0;
            for (int i = 0; i < Math.Min(Math.Min(s1Len, s2Len), 4); i++)
            {
                if (s1[i] == s2[i])
                    prefix++;
                else
                    break;
            }

            return jaro + (prefix * 0.1f * (1f - jaro));
        }

        #endregion

        #region Phonetic Similarity (Simplified Soundex)

        /// <summary>
        /// Calculates phonetic similarity - words that sound alike
        /// Good for: phonetic approximations, different spellings of similar sounds
        /// </summary>
        private static float PhoneticSimilarity(string s1, string s2)
        {
            string soundex1 = Soundex(s1);
            string soundex2 = Soundex(s2);

            if (soundex1 == soundex2)
                return 1f;

            // Calculate similarity between soundex codes
            int distance = LevenshteinDistance(soundex1, soundex2);
            return 1f - ((float)distance / 4f); // Soundex is 4 chars
        }

        /// <summary>
        /// Simplified Soundex algorithm for phonetic encoding
        /// </summary>
        private static string Soundex(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "0000";

            s = s.ToUpper();
            StringBuilder result = new StringBuilder();
            result.Append(s[0]);

            // Soundex mapping
            char[] mapping = new char[26];
            string[] codes = { "01230120022455012623010202" };
            for (int i = 0; i < 26; i++)
                mapping[i] = codes[0][i];

            char lastCode = GetSoundexCode(s[0], mapping);

            for (int i = 1; i < s.Length && result.Length < 4; i++)
            {
                char code = GetSoundexCode(s[i], mapping);
                if (code != '0' && code != lastCode)
                {
                    result.Append(code);
                    lastCode = code;
                }
                else if (code != '0')
                {
                    lastCode = code;
                }
            }

            // Pad with zeros
            while (result.Length < 4)
                result.Append('0');

            return result.ToString();
        }

        private static char GetSoundexCode(char c, char[] mapping)
        {
            if (c >= 'A' && c <= 'Z')
                return mapping[c - 'A'];
            return '0';
        }

        #endregion

        #region Length Penalty

        /// <summary>
        /// Penalizes very short or very long attempts relative to target
        /// </summary>
        private static float CalculateLengthPenalty(string transcribed, string target)
        {
            float ratio = (float)transcribed.Length / target.Length;

            // Optimal ratio is 1.0 (same length)
            // Penalize heavily for very short attempts (< 0.3) or very long (> 2.0)
            if (ratio < 0.3f)
                return 0.3f; // Heavy penalty for too short
            if (ratio > 2.0f)
                return 0.5f; // Penalty for too long

            // Smooth penalty curve
            return 1f - Math.Abs(1f - ratio) * 0.5f;
        }

        #endregion

        #region Testing and Utilities

        /// <summary>
        /// Get detailed breakdown of similarity components (for tuning/debugging)
        /// </summary>
        public static string GetSimilarityBreakdown(string transcribed, string target)
        {
            transcribed = transcribed.ToLower().Trim();
            target = target.ToLower().Trim();

            float lev = LevenshteinSimilarity(transcribed, target) * 100;
            float jaro = JaroWinklerSimilarity(transcribed, target) * 100;
            float phon = PhoneticSimilarity(transcribed, target) * 100;
            float len = CalculateLengthPenalty(transcribed, target) * 100;
            float total = CalculateSimilarity(transcribed, target);

            return $"Transcribed: '{transcribed}' → Target: '{target}'\n" +
                   $"Levenshtein: {lev:F1}% (weight: {LEVENSHTEIN_WEIGHT})\n" +
                   $"Jaro-Winkler: {jaro:F1}% (weight: {JARO_WINKLER_WEIGHT})\n" +
                   $"Phonetic: {phon:F1}% (weight: {PHONETIC_WEIGHT})\n" +
                   $"Length: {len:F1}% (weight: {LENGTH_PENALTY_WEIGHT})\n" +
                   $"TOTAL SCORE: {total:F1}/100";
        }

        #endregion
    }
}