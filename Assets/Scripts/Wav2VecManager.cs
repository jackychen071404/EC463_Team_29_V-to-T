using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using Unity.InferenceEngine;

public class Wav2VecManager : MonoBehaviour
{
    public static Wav2VecManager Instance;
    public ModelAsset wav2vecModel;

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
    }

    // Modified function to return score via callback
    public void GetScoreFromFile(string recordingPath, string targetWord, Action<float> onScoreReady)
    {
        StartCoroutine(LoadClipAndScore(recordingPath, targetWord, onScoreReady));
    }

    private IEnumerator LoadClipAndScore(string path, string targetWord, Action<float> onScoreReady)
    {
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

            var wav2vec = new Wav2VecONNX(wav2vecModel);
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
}
