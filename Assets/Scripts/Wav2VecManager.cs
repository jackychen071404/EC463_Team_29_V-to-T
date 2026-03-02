using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using Unity.InferenceEngine;

public class Wav2VecManager : MonoBehaviour
{
    public static Wav2VecManager Instance;
    public ModelAsset wav2vecModel;
    [Header("Debug")]
    public bool enableDetailedLogs = true;

    [Header("Warm-up")]
    public bool warmUpOnStart = true;
    public int warmUpSampleCount = 16000;

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
        var cmuFile = Resources.Load<TextAsset>("cmudict");
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
            Debug.LogError("[Wav2VecManager] wav2vecModel is not assigned.");
            return;
        }

        wav2vec = new Wav2VecONNX(wav2vecModel, enableDetailedLogs);
        Debug.Log("[Wav2VecManager] Wav2VecONNX instance initialized.");
    }

    private void WarmUpModel()
    {
        if (wav2vec == null)
        {
            Debug.LogWarning("[Wav2VecManager] Warm-up skipped because model is not initialized.");
            return;
        }

        try
        {
            Debug.Log($"[Wav2VecManager] Warm-up started. Samples={warmUpSampleCount}");
            wav2vec.WarmUp(warmUpSampleCount);
            Debug.Log("[Wav2VecManager] Warm-up completed.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Wav2VecManager] Warm-up failed: {ex.Message}");
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
            Debug.LogError("[Wav2VecManager] Model is not initialized.");
            onScoreReady?.Invoke(-1f);
            yield break;
        }

        if (enableDetailedLogs)
            Debug.Log($"[Wav2VecManager][DEBUG] Loading WAV from path: {path}");

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file:///" + path, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to load audio: " + www.error);
                onScoreReady?.Invoke(-1f); // return -1 on failure
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            if (enableDetailedLogs)
                Debug.Log($"[Wav2VecManager][DEBUG] Audio clip loaded. Samples={clip.samples}, Channels={clip.channels}, Frequency={clip.frequency}");

            string predictedPhonemes = wav2vec.GetPhonemesFromClip(clip);
            string targetPhonemes = PhonemeConverter.ConvertWordAsString(targetWord);

            Debug.Log($"Target Word: {targetWord}");
            Debug.Log($"Predicted Phonemes: {predictedPhonemes}");
            Debug.Log($"Target Phonemes: {targetPhonemes}");

            float score = PhonemeScoringEngine.CalculateSimilarity(predictedPhonemes, targetPhonemes);

            Debug.Log($"Similarity Score: {score}");

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

        Debug.Log("[Wav2VecManager] Disposing Wav2VecONNX...");
        wav2vec.Dispose();
        wav2vec = null;
    }
}
