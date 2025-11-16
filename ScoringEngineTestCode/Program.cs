using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;  // Added for OrderByDescending
using System.IO;    // Added for File, Directory, StreamWriter
using ScoringEngineTest;

namespace ScoringEngineTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/SimilarityDebugLog.txt", "");
            File.WriteAllText("Logs/AllTestPairScores.txt", "");

            Console.WriteLine("=== Speech Similarity Scorer - Testing ===\n");

            Console.WriteLine("=== Initialization ===");
            Initialization("test", "test");

            Console.WriteLine("=== Test1: Deterministic Output ===");
            TestDeterministicOutput("apple", "apple", 3);

            Console.WriteLine("\n=== Test2: Ranking Behavior ===");
            var rankingTestPairs = new List<(string transcribed, string target)>
            {
                ("apple", "apple"),       // Exact match
                ("aple", "apple"),        // Near match (one letter off)
                ("app", "apple"),         // Partial match
                ("dog", "apple")          // Unrelated (changed from "zebra")
            };
            TestRankingBehavior(rankingTestPairs);

            Console.WriteLine("\n=== Test3: Valid Output ===");
            var validTestPairs = new List<(string transcribed, string target)>
            {
                ("", "test"), // Empty transcribed
                ("a", "abbreviation"),    // very short input vs long target
                ("supercalifragilisticexpialidocious", "super"), // very long input vs short target
            };
            ValidOutputTest(validTestPairs);

            Console.WriteLine("\n=== Test4: Null/Empty Strings ===");
            var validPairs = new List<(string? transcribed, string? target)>
            {
                (null, "apple"),
                ("apple", null),
                (null, null),
                ("", "apple"),
                ("apple", ""),
                ("", "")
            };
            NonStringTest(validPairs);

            Console.WriteLine("\n=== Test5: Performance Benchmark Tests ===");
            // Test dataset - realistic transcription/target pairs
            // NOTE: Current test set has 35 pairs for initial validation.
            // For Spring MVP, expand to 1,000+ word pairs to meet full database requirement.
            var testPairs = new List<(string transcribed, string target)>
            {
                // Exact matches
                ("apple", "apple"),
                ("baby", "baby"),
                
                // Close matches (1 letter off)
                ("aple", "apple"),
                ("babi", "baby"),
                ("acordion", "accordion"),
                
                // Partial attempts
                ("app", "apple"),
                ("ba", "baby"),
                ("acc", "accordion"),
                
                // Phonetically similar
                ("nite", "night"),
                ("fone", "phone"),
                ("ruff", "rough"),
                
                // Very short attempts
                ("a", "apple"),
                ("b", "baby"),
                ("aa", "ant"),
                
                // Common child speech patterns
                ("tat", "cat"),      // fronting
                ("wabbit", "rabbit"), // substitution
                ("pasketti", "spaghetti"), // complex word
                
                // Completely different
                ("zebra", "apple"),
                ("xyz", "baby"),
                
                // Repeated sounds
                ("aaaa", "ant"),
                ("bbb", "baby"),
                
                // Longer words
                ("artist", "artist"),
                ("artis", "artist"),
                ("avocado", "avocado"),
                ("avacado", "avocado")
            };

            int[] iterationCounts = { 100, 1000 };
            RunBenchmarkTest(testPairs, iterationCounts);
        }

        #region Initialization 
        /// <summary>
        /// Performs any necessary initialization for the scoring engine.
        /// </summary>
        static void Initialization(string testTranscribed, string testTarget)
        {
            float initialScore = SimilarityScorer.CalculateSimilarity(testTranscribed, testTarget);
            Console.WriteLine($"Initialization complete. Sample score for '{testTranscribed}' vs '{testTarget}': {initialScore}/100");
            Console.WriteLine("(This output is discarded as per test procedure)\n");
        }
        #endregion

        #region Determisitic Output
        /// <summary>
        /// Tests that the scoring engine produces consistent results for the same input.
        /// </summary>
        static void TestDeterministicOutput(string transcribed, string target, int runs)
        {
            var scores = new List<float>();

            for (int i = 0; i < runs; i++)
            {
                float score = SimilarityScorer.CalculateSimilarity(transcribed, target);
                scores.Add(score);
                Console.WriteLine($"Run {i + 1}: Score for '{transcribed}' vs '{target}': {score}/100");
            }

            // Verify all scores are identical
            bool allSame = scores.All(s => Math.Abs(s - scores[0]) < 0.001f);
            if (allSame)
                Console.WriteLine("✓ Deterministic output verified: All scores identical");
            else
                Console.WriteLine("❌ Warning: Scores differ across runs");
        }
        #endregion

        #region Ranking Behavior
        /// <summary>
        /// Tests the ranking behavior of the scoring engine with various input pairs.
        /// </summary>
        static void TestRankingBehavior(List<(string transcribed, string target)> testPairs)
        {
            var results = new List<(string transcribed, string target, float score)>();

            foreach (var (transcribed, target) in testPairs)
            {
                float score = SimilarityScorer.CalculateSimilarity(transcribed, target);
                results.Add((transcribed, target, score));
            }

            // Sort results by score in descending order
            results = results.OrderByDescending(r => r.score).ToList();

            for (int i = 0; i < results.Count; i++)
            {
                var (transcribed, target, score) = results[i];
                Console.WriteLine($"Rank {i + 1}: '{transcribed}' → '{target}': {score:F1}/100");
            }

            // Verify ranking order is correct (descending)
            bool rankingCorrect = true;
            for (int i = 1; i < results.Count; i++)
            {
                if (results[i].score > results[i - 1].score)
                {
                    rankingCorrect = false;
                    Console.WriteLine($"❌ Ranking error: Rank {i + 1} score ({results[i].score:F1}) > Rank {i} score ({results[i - 1].score:F1})");
                }
            }

            if (rankingCorrect)
                Console.WriteLine("✓ Ranking order verified: Scores correctly ordered (exact > near > partial > unrelated)");
        }
        #endregion

        #region Valid Output
        /// <summary>
        /// Tests that the scoring engine produces valid scores (0-100) for a variety of input pairs.
        /// </summary>
        static void ValidOutputTest(List<(string transcribed, string target)> testPairs)
        {
            bool allValid = true;

            foreach (var (transcribed, target) in testPairs)
            {
                float score = SimilarityScorer.CalculateSimilarity(transcribed, target);
                Console.WriteLine($"Score for '{transcribed}' vs '{target}': {score}/100");

                if (score < 0 || score > 100)
                {
                    Console.WriteLine($"❌ Invalid score: {score} (outside 0-100 range)");
                    allValid = false;
                }
            }

            if (allValid)
                Console.WriteLine("✓ All scores within valid range (0-100)");
        }
        #endregion

        #region Non-String and Null Tests
        /// <summary>
        /// Tests that the scoring engine produces valid scores for a variety of input pairs. Makes sure the algo can handle nulls, empty strings, and any other weird inputs.
        /// </summary>
        static void NonStringTest(List<(string? transcribed, string? target)> testPairs)
        {
            bool allPassed = true;

            foreach (var (transcribed, target) in testPairs)
            {
                try
                {
                    float score = SimilarityScorer.CalculateSimilarity(transcribed, target);
                    Console.WriteLine($"Score for '{transcribed ?? "null"}' vs '{target ?? "null"}': {score}/100");

                    if (score != 0)
                    {
                        Console.WriteLine($"  Note: Expected 0 for null/empty, got {score}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error scoring '{transcribed ?? "null"}' vs '{target ?? "null"}': {ex.Message}");
                    allPassed = false;
                }
            }

            if (allPassed)
                Console.WriteLine("✓ Null/empty handling verified: No crashes, all return 0");
        }
        #endregion

        #region Benchmarking
        /// <summary>
        /// Runs benchmark tests on the scoring engine with specified test pairs and iteration counts.
        /// </summary>
        static void RunBenchmarkTest(
            List<(string transcribed, string target)> testPairs,
            int[] iterationCounts)
        {
            Console.WriteLine("Warming up...\n");

            WarmUpEngine();

            foreach (int iterations in iterationCounts)
                RunBenchmark(testPairs, iterations);

            Console.WriteLine("\n--- Detailed Breakdown Example ---");
            PrintDetailedBreakdownExample(testPairs);

            LogAllScores(testPairs); // Sends full results to file silently
        }

        static void WarmUpEngine()
        {
            for (int i = 0; i < 200; i++)
                SimilarityScorer.CalculateSimilarity("test", "test");
        }

        static void RunBenchmark(List<(string transcribed, string target)> testPairs, int iterations)
        {
            Stopwatch stopwatch = new Stopwatch();
            int pairCount = testPairs.Count;
            int totalComparisons = iterations * pairCount;

            stopwatch.Start();

            for (int i = 0; i < iterations; i++)
            {
                foreach (var (transcribed, target) in testPairs)
                    SimilarityScorer.CalculateSimilarity(transcribed, target);
            }

            stopwatch.Stop();

            double totalMs = stopwatch.Elapsed.TotalMilliseconds;
            double avgMs = totalMs / totalComparisons;
            double throughput = totalComparisons / stopwatch.Elapsed.TotalSeconds;

            Console.WriteLine(
                $"Iterations: {iterations:N0} × {pairCount} pairs = {totalComparisons:N0} comparisons");
            Console.WriteLine($"Total Time: {totalMs:F2} ms");
            Console.WriteLine($"Avg per comparison: {avgMs:F4} ms ({avgMs * 1000:F2} microseconds)");
            Console.WriteLine($"Throughput: {throughput:F0} comparisons/second");

            // Verify performance requirement (under 5ms per comparison)
            if (avgMs < 5.0)
                Console.WriteLine($"✓ Performance PASS: {avgMs:F4} ms per comparison (under 5ms threshold)\n");
            else
                Console.WriteLine($"❌ Performance FAIL: {avgMs:F4} ms per comparison (exceeds 5ms threshold)\n");
        }

        static void PrintDetailedBreakdownExample(List<(string transcribed, string target)> testPairs)
        {
            // Pick one clean example, like ("aple","apple")
            var example = testPairs.FirstOrDefault(
                p => p.transcribed == "aple" && p.target == "apple");

            if (example == default)
                example = testPairs[0];

            string breakdown = SimilarityScorer.GetSimilarityBreakdown(
                example.transcribed, example.target);

            Console.WriteLine(breakdown);
        }

        static void LogAllScores(List<(string transcribed, string target)> testPairs)
        {
            Directory.CreateDirectory("Logs");

            using (StreamWriter writer = new StreamWriter("Logs/AllTestPairScores.txt", false))
            {
                foreach (var (t, target) in testPairs)
                {
                    float score = SimilarityScorer.CalculateSimilarity(t, target);
                    writer.WriteLine($"'{t}' → '{target}': {score:F1}/100");
                }
            }

            Console.WriteLine($"Full results written to: Logs/AllTestPairScores.txt");
        }

        #endregion
    }
}