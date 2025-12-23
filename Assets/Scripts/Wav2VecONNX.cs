using UnityEngine;
using Unity.InferenceEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class Wav2VecONNX : IDisposable
{
    [Header("Model Settings")]
    public ModelAsset wav2vec2ModelAsset;

    [Header("Model Configuration")]
    public int expectedSampleRate = 16000;
    public bool normalizeAudio = true;

    private Model model;
    private Worker worker;
    private Tensor<float> inputTensor;

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

    private const int BLANK_TOKEN_ID = 42; // [PAD] is blank token for CTC

    public Wav2VecONNX(ModelAsset modelAsset)
    {
        wav2vec2ModelAsset = modelAsset ?? throw new ArgumentNullException(nameof(modelAsset));
        model = ModelLoader.Load(wav2vec2ModelAsset);
        worker = new Worker(model, BackendType.GPUCompute);
        Debug.Log("Wav2Vec2 model loaded!");
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

        return RunInference(monoData);
    }

    private string RunInference(float[] audioData)
    {
        using (var inputTensor = new Tensor<float>(new TensorShape(1, audioData.Length), audioData))
        {
            worker.Schedule(inputTensor);

            // Get the output tensor and ensure it's disposed
            using (var outputTensor = worker.PeekOutput() as Tensor<float>)
            {
                if (outputTensor == null)
                    throw new Exception("Failed to get output tensor");

                outputTensor.DownloadToArray();
                return DecodeCTC(outputTensor);
            }
        }
    }

    private string DecodeCTC(Tensor<float> logits)
    {
        int batchSize = logits.shape[0];
        int timeSteps = logits.shape[1];
        int vocabSize = logits.shape[2];

        float[] logitsData = logits.DownloadToArray();
        List<int> tokenIndices = new List<int>();

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
        }

        // Remove consecutive duplicates and blanks
        List<string> phonemes = new List<string>();
        int prevToken = -1;
        foreach (int token in tokenIndices)
        {
            if (token != prevToken && token != BLANK_TOKEN_ID)
            {
                prevToken = token;
                if (vocab.ContainsKey(token))
                {
                    string ph = vocab[token];
                    if (ph != "[UNK]" && ph != "[PAD]" && ph != "|")
                        phonemes.Add(ph);
                }
            }
        }

        return string.Join(" ", phonemes).Trim();
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

    public void Dispose()
    {
        inputTensor?.Dispose();
        worker?.Dispose();
    }
}
