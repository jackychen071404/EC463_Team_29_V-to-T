using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using Unity.InferenceEngine;

public class Wav2VecManager : MonoBehaviour
{
    private const string LogSource = "Wav2VecManager.cs";
    public static Wav2VecManager Instance;
    public ModelAsset wav2vecModel;
    [Header("Debug")]
    public bool enableDetailedLogs = true;

    [Header("Warm-up")]
    public bool warmUpOnStart = true;
    public int warmUpSampleCount = BackendConfig.Ml.DefaultWarmUpSampleCount;

    private Wav2VecONNX wav2vec;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

    }

    void Start()
    {
        var cmuFile = Resources.Load<TextAsset>(BackendConfig.Ml.CmuDictResourceName);
        if (cmuFile == null)
        {
            BackendLogger.Error(LogSource, "CMUDictLoadFailed", $"resourceName={BackendConfig.Ml.CmuDictResourceName}");
        }
        else
        {
            BackendLogger.Info(LogSource, "CMUDictLoaded", $"resourceName={BackendConfig.Ml.CmuDictResourceName}, charLength={cmuFile.text?.Length ?? 0}");
        }
        PhonemeConverter.LoadCMUDict(cmuFile);

        InitializeModel();

        if (warmUpOnStart)
            WarmUpModel();
    }

    private void InitializeModel()
    {
        if (wav2vec != null)
            return;

        if (wav2vecModel == null)
        {
            BackendLogger.Error(LogSource, "ModelMissing", "wav2vecModel is not assigned in inspector");
            return;
        }

        wav2vec = new Wav2VecONNX(wav2vecModel, enableDetailedLogs);
        BackendLogger.Info(LogSource, "ModelInitialized", $"detailedLogs={enableDetailedLogs}");
    }

    private void WarmUpModel()
    {
        if (wav2vec == null)
        {
            BackendLogger.Warn(LogSource, "WarmUpSkipped", "reason=model_not_initialized");
            return;
        }

        try
        {
            BackendLogger.Info(LogSource, "WarmUpStarted", $"sampleCount={warmUpSampleCount}");
            wav2vec.WarmUp(warmUpSampleCount);
            BackendLogger.Info(LogSource, "WarmUpCompleted", $"sampleCount={warmUpSampleCount}");
        }
        catch (Exception ex)
        {
            BackendLogger.Error(LogSource, "WarmUpFailed", ex, $"sampleCount={warmUpSampleCount}");
        }
    }

    // Modified function to return score via callback
    public void GetScoreFromFile(string recordingPath, string targetWord, Action<float> onScoreReady)
    {
        StartCoroutine(LoadClipAndScore(recordingPath, targetWord, onScoreReady));
    }

    private IEnumerator LoadClipAndScore(string path, string targetWord, Action<float> onScoreReady)
    {
        if (wav2vec == null)
        {
            BackendLogger.Error(LogSource, "ScoreRequestFailed", "reason=model_not_initialized");
            onScoreReady?.Invoke(BackendConfig.Ml.ScoreErrorValue);
            yield break;
        }

        BackendLogger.Verbose(enableDetailedLogs, LogSource, "ScoreRequestStarted", $"audioPath={path}, targetWord={targetWord}");

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file:///" + path, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                BackendLogger.Error(LogSource, "AudioLoadFailed", $"audioPath={path}, webRequestError={www.error}, result={www.result}");
                onScoreReady?.Invoke(BackendConfig.Ml.ScoreErrorValue); // return -1 on failure
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            BackendLogger.Verbose(enableDetailedLogs, LogSource, "AudioLoaded", $"samples={clip.samples}, channels={clip.channels}, frequencyHz={clip.frequency}");

            string predictedPhonemes = wav2vec.GetPhonemesFromClip(clip);
            string targetPhonemes = PhonemeConverter.ConvertWordAsString(targetWord);

            BackendLogger.Info(LogSource, "PhonemeExtractionCompleted", $"targetWord={targetWord}, predicted='{predictedPhonemes}', target='{targetPhonemes}'");

            bool startsWithConsonant = Regex.IsMatch(targetWord, @"^[^aeiouAEIOU]");
            BackendLogger.Verbose(enableDetailedLogs, LogSource, "InitialConsonantCheck", $"targetWord={targetWord}, startsWithConsonant={startsWithConsonant}");
            float score = PhonemeScoringEngine.CalculateSimilarity(predictedPhonemes, targetPhonemes, startsWithConsonant);

            BackendLogger.Info(LogSource, "ScoreComputed", $"targetWord={targetWord}, score={score:F3}");

            // Return the score via callback
            onScoreReady?.Invoke(score);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            DisposeModel();
            Instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        if (Instance == this)
            DisposeModel();
    }

    private void DisposeModel()
    {
        if (wav2vec == null)
            return;

        BackendLogger.Info(LogSource, "ModelDisposeStarted", null);
        wav2vec.Dispose();
        wav2vec = null;
        BackendLogger.Info(LogSource, "ModelDisposeCompleted", null);
    }
}
