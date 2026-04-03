using UnityEngine;
using Unity.InferenceEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class Wav2VecONNX : IDisposable
{
    private const string LogSource = "Wav2VecONNX.cs";
    [Header("Model Settings")]
    public ModelAsset wav2vec2ModelAsset;

    [Header("Model Configuration")]
    public int expectedSampleRate = BackendConfig.Ml.DefaultExpectedSampleRate;
    public bool normalizeAudio = BackendConfig.Ml.DefaultNormalizeAudio;
    public float leadingSilencePaddingSeconds = BackendConfig.Ml.LeadingSilencePaddingSeconds;

    private Model model;
    private Worker worker;
    private readonly bool enableDebugLogs;
    private bool isDisposed;

    private readonly Dictionary<int, string> vocab = new Dictionary<int, string>
    {
        {39, "E"}, {1, "I"}, {42, "[PAD]"}, {41, "[UNK]"}, {24, "a"},
        {30, "aw"}, {6, "ay"}, {3, "b"}, {25, "bth"}, {19, "ch"},
        {4, "d"}, {32, "e"}, {9, "ee"}, {7, "f"}, {33, "g"},
        {8, "h"}, {34, "i"}, {5, "j"}, {11, "k"}, {12, "l"},
        {13, "m"}, {14, "n"}, {26, "ng"}, {27, "o"}, {28, "oau"},
        {15, "oh"}, {29, "oi"}, {20, "oo"}, {31, "or"}, {2, "ow"},
        {16, "p"}, {35, "r"}, {17, "s"}, {36, "sh"}, {18, "t"},
        {40, "th"}, {38, "u"}, {37, "uoh"}, {21, "v"}, {22, "w"},
        {10, "y"}, {23, "z"}, {0, "|"}
    };

    private const int BLANK_TOKEN_ID = BackendConfig.Ml.BlankTokenId; // [PAD] is blank token for CTC
    private const int P_TOKEN_ID = 16;
    private const int InitialFrameDiagnosticCount = 12;

    public Wav2VecONNX(ModelAsset modelAsset, bool enableDetailedLogs = false)
    {
        enableDebugLogs = enableDetailedLogs;
        wav2vec2ModelAsset = modelAsset ?? throw new ArgumentNullException(nameof(modelAsset));

        LogDebug("Loading model asset...");
        model = ModelLoader.Load(wav2vec2ModelAsset);
        LogDebug("Creating GPU worker...");
        worker = new Worker(model, BackendType.GPUCompute);
        BackendLogger.Info(LogSource, "ModelLoaded", "backend=GPUCompute");
    }

    /// <summary>
    /// Returns phonemes from an AudioClip
    /// </summary>
    public string GetPhonemesFromClip(AudioClip clip)
    {
        if (clip == null) throw new ArgumentNullException(nameof(clip));
        float[] audioData = new float[clip.samples * clip.channels];
        clip.GetData(audioData, 0);
        float[] monoData = ConvertToMono(audioData, clip.channels);

        if (clip.frequency != expectedSampleRate)
            monoData = Resample(monoData, clip.frequency, expectedSampleRate);

        if (normalizeAudio)
            monoData = NormalizeAudio(monoData);

        monoData = PrependSilence(monoData, expectedSampleRate, leadingSilencePaddingSeconds);

        return RunInference(monoData);
    }

    /// <summary>
    /// Returns phonemes from a WAV file path
    /// </summary>
    public string GetPhonemesFromWav(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"WAV file not found: {path}");

        var (audioData, sampleRate, channels) = LoadWavFile(path);
        float[] monoData = ConvertToMono(audioData, channels);

        if (sampleRate != expectedSampleRate)
            monoData = Resample(monoData, sampleRate, expectedSampleRate);

        if (normalizeAudio)
            monoData = NormalizeAudio(monoData);

        monoData = PrependSilence(monoData, expectedSampleRate, leadingSilencePaddingSeconds);

        return RunInference(monoData);
    }

    private string RunInference(float[] audioData)
    {
        if (isDisposed)
            throw new ObjectDisposedException(nameof(Wav2VecONNX));

        LogDebug($"RunInference start. Samples={audioData.Length}");
        using (var inputTensor = new Tensor<float>(new TensorShape(1, audioData.Length), audioData))
        {
            LogDebug("Input tensor created. Scheduling worker...");
            worker.Schedule(inputTensor);
            LogDebug("Worker schedule complete. Reading output...");

            using (var outputTensor = worker.PeekOutput() as Tensor<float>)
            {
                if (outputTensor == null)
                    throw new Exception("Failed to get output tensor");

                LogDebug($"Output tensor acquired. Shape=[{outputTensor.shape[0]}, {outputTensor.shape[1]}, {outputTensor.shape[2]}]");
                outputTensor.DownloadToArray();
                LogDebug("Output tensor downloaded. Decoding CTC...");
                return DecodeCTC(outputTensor);
            }
        }
    }

    public void WarmUp(int sampleCount = BackendConfig.Ml.DefaultWarmUpSampleCount)
    {
        if (isDisposed)
            throw new ObjectDisposedException(nameof(Wav2VecONNX));

        if (sampleCount < 1)
            sampleCount = BackendConfig.Ml.DefaultWarmUpSampleCount;

        LogDebug($"Warm-up start. sampleCount={sampleCount}");
        var silent = new float[sampleCount];
        var _ = RunInference(silent);
        LogDebug("Warm-up complete.");
    }

    private string DecodeCTC(Tensor<float> logits)
    {
        int batchSize = logits.shape[0];
        int timeSteps = logits.shape[1];
        int vocabSize = logits.shape[2];

        LogDebug($"DecodeCTC shape batch={batchSize}, timeSteps={timeSteps}, vocabSize={vocabSize}");

        float[] logitsData = logits.DownloadToArray();
        List<int> tokenIndices = new List<int>();
        List<float> topLogits = new List<float>(timeSteps);

        for (int t = 0; t < timeSteps; t++)
        {
            int maxIndex = 0;
            float maxValue = float.MinValue;

            for (int v = 0; v < vocabSize; v++)
            {
                int idx = t * vocabSize + v;
                if (logitsData[idx] > maxValue)
                {
                    maxValue = logitsData[idx];
                    maxIndex = v;
                }
            }

            tokenIndices.Add(maxIndex);
            topLogits.Add(maxValue);
        }

        LogInitialFrameDiagnostics(logitsData, vocabSize, tokenIndices, topLogits);

        // Remove consecutive duplicates and blanks
        List<string> phonemes = new List<string>();
        int prevToken = -1;
        foreach (int token in tokenIndices)
        {
            if (token == BLANK_TOKEN_ID)
            {
                // CTC blank resets duplicate suppression.
                prevToken = -1;
                continue;
            }

            if (!vocab.TryGetValue(token, out string ph))
                continue;

            if (ph == "[UNK]" || ph == "[PAD]" || ph == "|")
                continue;

            if (token == prevToken)
                continue;

            phonemes.Add(ph);
            prevToken = token;
        }

        return string.Join(" ", phonemes).Trim();
    }

    private void LogInitialFrameDiagnostics(float[] logitsData, int vocabSize, List<int> tokenIndices, List<float> topLogits)
    {
        if (!enableDebugLogs || tokenIndices == null || tokenIndices.Count == 0)
            return;

        int framesToInspect = Mathf.Min(InitialFrameDiagnosticCount, tokenIndices.Count);
        List<string> frameSummaries = new List<string>(framesToInspect);
        int pTop1Count = 0;

        for (int t = 0; t < framesToInspect; t++)
        {
            int bestToken = tokenIndices[t];
            float bestLogit = topLogits[t];
            float pLogit = logitsData[t * vocabSize + P_TOKEN_ID];
            int pRank = 1;

            for (int v = 0; v < vocabSize; v++)
            {
                if (logitsData[t * vocabSize + v] > pLogit)
                    pRank++;
            }

            if (bestToken == P_TOKEN_ID)
                pTop1Count++;

            frameSummaries.Add($"t{t}:best={TokenLabel(bestToken)}({bestLogit:F3}),pRank={pRank},pLogit={pLogit:F3}");
        }

        LogDebug($"InitialFrameTokenRanks frames={framesToInspect}, pTop1Frames={pTop1Count}, {string.Join(" | ", frameSummaries)}");
    }

    private string TokenLabel(int token)
    {
        return vocab.TryGetValue(token, out var label) ? label : $"id{token}";
    }

    private (float[] data, int sampleRate, int channels) LoadWavFile(string path)
    {
        byte[] wavBytes = File.ReadAllBytes(path);
        int channels = BitConverter.ToInt16(wavBytes, 22);
        int sampleRate = BitConverter.ToInt32(wavBytes, 24);
        int bitsPerSample = BitConverter.ToInt16(wavBytes, 34);

        int dataStart = 44;
        int sampleCount = (wavBytes.Length - dataStart) / (bitsPerSample / 8);
        float[] audioData = new float[sampleCount];

        if (bitsPerSample == 16)
        {
            for (int i = 0; i < sampleCount; i++)
                audioData[i] = BitConverter.ToInt16(wavBytes, dataStart + i * 2) / 32768f;
        }
        else if (bitsPerSample == 32)
        {
            for (int i = 0; i < sampleCount; i++)
                audioData[i] = BitConverter.ToSingle(wavBytes, dataStart + i * 4);
        }
        else
        {
            throw new Exception($"Unsupported bits per sample: {bitsPerSample}");
        }

        return (audioData, sampleRate, channels);
    }

    private float[] ConvertToMono(float[] audioData, int channels)
    {
        if (channels == 1) return audioData;
        int monoLength = audioData.Length / channels;
        float[] mono = new float[monoLength];

        for (int i = 0; i < monoLength; i++)
        {
            float sum = 0;
            for (int c = 0; c < channels; c++)
                sum += audioData[i * channels + c];
            mono[i] = sum / channels;
        }

        return mono;
    }

    private float[] Resample(float[] input, int fromRate, int toRate)
    {
        if (fromRate == toRate) return input;
        float ratio = (float)fromRate / toRate;
        int outputLength = Mathf.RoundToInt(input.Length / ratio);
        float[] output = new float[outputLength];

        for (int i = 0; i < outputLength; i++)
        {
            float srcIdx = i * ratio;
            int idx1 = Mathf.FloorToInt(srcIdx);
            int idx2 = Mathf.Min(idx1 + 1, input.Length - 1);
            float frac = srcIdx - idx1;
            output[i] = Mathf.Lerp(input[idx1], input[idx2], frac);
        }

        return output;
    }

    private float[] NormalizeAudio(float[] audioData)
    {
        float maxAbs = audioData.Max(Mathf.Abs);
        if (maxAbs > 0)
            return audioData.Select(s => s / maxAbs).ToArray();
        return audioData;
    }

    private float[] PrependSilence(float[] audioData, int sampleRate, float seconds)
    {
        if (audioData == null || audioData.Length == 0)
            return audioData;

        if (seconds <= 0f || sampleRate <= 0)
            return audioData;

        int silenceSamples = Mathf.RoundToInt(sampleRate * seconds);
        if (silenceSamples <= 0)
            return audioData;

        float[] padded = new float[audioData.Length + silenceSamples];
        Array.Copy(audioData, 0, padded, silenceSamples, audioData.Length);
        return padded;
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        LogDebug("Disposing worker...");
        worker?.Dispose();
        worker = null;
        model = null;
        isDisposed = true;
        BackendLogger.Info(LogSource, "WorkerDisposed", "gpuResourcesReleased=true");
    }

    private void LogDebug(string message)
    {
        BackendLogger.Verbose(enableDebugLogs, LogSource, "Diagnostic", message);
    }
}