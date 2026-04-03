using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class FoodSceneController : MonoBehaviour
{
    private const string LogSource = "FoodSceneController.cs";

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI wordText;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private TextMeshProUGUI micButtonText;
    [SerializeField] private Button micButton;

    [Header("Navigation")]
    [SerializeField] private Button nextButton;
    [SerializeField] private string nextSceneName = "OutfitScene";

    [Header("Penguins (States)")]
    [SerializeField] private GameObject penguin;        // idle
    [SerializeField] private GameObject penguinApple;
    [SerializeField] private GameObject penguinPizza;
    [SerializeField] private GameObject penguinCookie;
    [SerializeField] private GameObject penguinBanana;

    [Header("Prompt Audio")]
    [SerializeField] private AudioSource promptApple;
    [SerializeField] private AudioSource promptPizza;
    [SerializeField] private AudioSource promptCookie;
    [SerializeField] private AudioSource promptBanana;

    [Header("SFX / Feedback")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip appleCrunch;

    [SerializeField] private AudioSource failAppleSource;
    [SerializeField] private AudioSource failPizzaSource;
    [SerializeField] private AudioSource failCookieSource;
    [SerializeField] private AudioSource failBananaSource;

    [Header("Particles")]
    [SerializeField] private ParticleSystem pizzaParticles;
    [SerializeField] private ParticleSystem confettiParticles;

    [Header("Cookie Bounce (pick one target)")]
    [SerializeField] private RectTransform penguinRectTransform;
    [SerializeField] private Transform penguinTransform;

    [Header("Scoring / Flow")]
    [Range(0, 100)] [SerializeField] private float passThreshold = 80f;
    [SerializeField] private int randomMinScore = 50;
    [SerializeField] private int randomMaxScore = 100;

    [SerializeField] private float feedbackSeconds = 3.5f;
    [SerializeField] private float postPromptDelay = 0.15f;
    [SerializeField] private float minListenSeconds = 0.25f;
    [SerializeField] private float micReadyTimeoutSeconds = 1.8f;
    [SerializeField] private float speakReadyDelaySeconds = 0.12f;

    [Header("Bounce Settings")]
    [SerializeField] private float bounceAmount = 25f;
    [SerializeField] private float bounceUpSeconds = 0.18f;
    [SerializeField] private float bounceDownSeconds = 0.18f;

    private readonly string[] words = { "apple", "pizza", "cookie", "banana" };
    private int index = 0;

    private bool busy = false;
    private bool isListening = false;
    private float listenStartTime;

    private void Start()
    {
        if (micButton != null)
        {
            micButton.onClick.RemoveAllListeners();
            micButton.onClick.AddListener(OnMicPressed);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(GoToNextScene);
        }

        if (feedbackText) feedbackText.text = "";

        PrepareParticles(pizzaParticles);
        PrepareParticles(confettiParticles);

        if (penguinRectTransform == null && penguinTransform == null)
            BackendLogger.Warn(LogSource, "CookieBounceTargetMissing", "No bounce target assigned; cookie bounce animation will be skipped");

        BackendLogger.Info(LogSource, "SceneInitialized", $"words={words.Length}, passThreshold={passThreshold:F1}, feedbackSeconds={feedbackSeconds:F2}");

        StartRound(0);
    }

    private void GoToNextScene()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            BackendLogger.Error(LogSource, "NextSceneNavigationFailed", "reason=empty_next_scene_name");
            return;
        }

        BackendLogger.Info(LogSource, "NextSceneNavigation", $"nextScene={nextSceneName}");

        SceneManager.LoadScene(nextSceneName);
    }

    private void StartRound(int i)
    {
        index = Mathf.Clamp(i, 0, words.Length - 1);

        BackendLogger.Info(LogSource, "RoundStarted", $"index={index}, targetWord={CurrentWord()}");

        StopAllPrompts();
        StopAllFailFeedback();

        busy = false;
        isListening = false;

        SetAllFoodPenguinsOff();
        if (penguin) penguin.SetActive(true);

        if (feedbackText) feedbackText.text = "";
        SetMicUI(enabled: false, label: "Listening...");

        if (wordText) wordText.text = $"Penguin is hungry!\nSay: {CurrentWord().ToUpper()}";

        PlayPrompt();
        StartCoroutine(EnableMicAfterPrompt());
    }

    private IEnumerator EnableMicAfterPrompt()
    {
        busy = true;
        SetMicUI(enabled: false, label: "Listening...");

        float wait = CurrentPromptLength();
        if (wait > 0f) yield return new WaitForSeconds(wait);
        if (postPromptDelay > 0f) yield return new WaitForSeconds(postPromptDelay);

        busy = false;
        SetMicUI(enabled: true, label: "Tap to Speak");

        BackendLogger.Verbose(true, LogSource, "MicEnabledAfterPrompt", $"targetWord={CurrentWord()}, promptWaitSec={wait:F2}, postPromptDelaySec={postPromptDelay:F2}");

        if (wordText) wordText.text = $"Say:\n{CurrentWord().ToUpper()}";
    }
    // ADD MIC USAGE AND SCORE HERE
    private void OnMicPressed()
    {
        if (busy)
        {
            BackendLogger.Verbose(true, LogSource, "MicPressIgnored", "reason=controller_busy");
            return;
        }

        // Tap 1: start listening
        if (!isListening)
        {
            StopAllFailFeedback();
            if (feedbackText) feedbackText.text = "";

            StartCoroutine(BeginListeningWhenReady());
            return;
        }

        // Tap 2: stop and score
        isListening = false;
        busy = true;

        SetMicUI(enabled: false, label: "Scoring...");

        float listenedFor = Time.time - listenStartTime;
        if (listenedFor < minListenSeconds)
        {
            BackendLogger.Warn(LogSource, "ListeningTooShort", $"listenedForSec={listenedFor:F3}, minListenSec={minListenSeconds:F3}");
            // Stop recording but don't score
            if (VoiceRecorder.Instance != null && VoiceRecorder.Instance.IsCurrentlyRecording())
            {
                VoiceRecorder.Instance.StopAndSaveRecording();
            }
            
            StartCoroutine(QuickRetry("Hold it for a sec"));
            return;
        }

        // Stop recording and save
        if (VoiceRecorder.Instance != null)
        {
            bool saved = VoiceRecorder.Instance.StopAndSaveRecording();
            if (!saved)
            {
                BackendLogger.Warn(LogSource, "RecordingSaveFailed", "reason=empty_or_unavailable_audio");
                StartCoroutine(QuickRetry("Mic not ready. Try again."));
                return;
            }

            string recordingPath = VoiceRecorder.Instance.GetLatestRecordingPath();
            BackendLogger.Info(LogSource, "RecordingSaved", $"recordingPath={recordingPath}");

            // Get score from Wav2VecManager
            if (Wav2VecManager.Instance != null)
            {
                string targetWord = CurrentWord();
                BackendLogger.Info(LogSource, "ScoreRequested", $"targetWord={targetWord}, recordingPath={recordingPath}");
                
                Wav2VecManager.Instance.GetScoreFromFile(recordingPath, targetWord, OnScoreReceived);
            }
            else
            {
                BackendLogger.Error(LogSource, "ScoreRequestFallback", "reason=wav2vec_manager_missing");
                // Fallback to random score
                float score = Random.Range(randomMinScore, randomMaxScore + 1);
                BackendLogger.Warn(LogSource, "FallbackScoreGenerated", $"score={score:F1}, min={randomMinScore}, max={randomMaxScore}");
                OnScoreReceived(score);
            }
        }
        else
        {
            BackendLogger.Error(LogSource, "ScoreRequestFallback", "reason=voice_recorder_missing");
            // Fallback to random score
            float score = Random.Range(randomMinScore, randomMaxScore + 1);
            BackendLogger.Warn(LogSource, "FallbackScoreGenerated", $"score={score:F1}, min={randomMinScore}, max={randomMaxScore}");
            OnScoreReceived(score);
        }
    }

    private IEnumerator BeginListeningWhenReady()
    {
        busy = true;
        SetMicUI(enabled: false, label: "Starting Mic...");

        if (feedbackText) feedbackText.text = "Get ready...";

        if (VoiceRecorder.Instance == null)
        {
            BackendLogger.Error(LogSource, "ListeningStartFailed", "reason=voice_recorder_missing");
            StartCoroutine(QuickRetry("Mic unavailable. Try again."));
            yield break;
        }

        bool startAccepted = VoiceRecorder.Instance.StartRecording();
        if (!startAccepted)
        {
            BackendLogger.Warn(LogSource, "ListeningStartRejected", "reason=recorder_busy_or_missing");
            StartCoroutine(QuickRetry("Mic is busy. Try again."));
            yield break;
        }

        float timeoutAt = Time.time + Mathf.Max(BackendConfig.Voice.MinMicStartTimeoutSeconds, micReadyTimeoutSeconds);
        while (!VoiceRecorder.Instance.IsCurrentlyRecording() && Time.time < timeoutAt)
            yield return null;

        if (!VoiceRecorder.Instance.IsCurrentlyRecording())
        {
            BackendLogger.Warn(LogSource, "ListeningStartTimeout", $"timeoutSec={micReadyTimeoutSeconds:F2}");
            StartCoroutine(QuickRetry("Mic not ready. Try again."));
            yield break;
        }

        if (speakReadyDelaySeconds > 0f)
            yield return new WaitForSeconds(speakReadyDelaySeconds);

        isListening = true;
        listenStartTime = Time.time;
        busy = false;

        SetMicUI(enabled: true, label: "Tap to Stop");
        if (feedbackText) feedbackText.text = "Speak now!";

        BackendLogger.Info(LogSource, "ListeningStarted", $"targetWord={CurrentWord()}, micReadyDelaySec={speakReadyDelaySeconds:F2}");
    }

    private void OnScoreReceived(float score)
    {
        BackendLogger.Info(LogSource, "ScoreReceived", $"score={score:F3}, threshold={passThreshold:F1}, pass={score >= passThreshold}");
        
        bool pass = score >= passThreshold;

        if (pass) ShowSuccess(score);
        else ShowFail(score);
    }

    private void ShowSuccess(float score)
    {
        busy = true;
        StopAllFailFeedback();

        string w = CurrentWord();

        BackendLogger.Info(LogSource, "RoundPassed", $"targetWord={w}, score={score:F1}");

        if (feedbackText) feedbackText.text = $"Nice job! Score: {Mathf.RoundToInt(score)}% ";
        ShowFoodPenguin();

        switch (w)
        {
            case "apple":
                if (sfxSource && appleCrunch) sfxSource.PlayOneShot(appleCrunch);
                break;

            case "pizza":
                RestartParticles(pizzaParticles);
                break;

            case "cookie":
                StartCoroutine(CookieBounce());
                break;

            case "banana":
                if (feedbackText) feedbackText.text = $"Score: {Mathf.RoundToInt(score)}% You did them all!";
                RestartParticles(confettiParticles);
                break;
        }

        StartCoroutine(NextAfterDelay());
    }

    private void ShowFail(float score)
    {
        StopAllPrompts();
        StopAllFailFeedback();
        PlayFailFeedback();

        BackendLogger.Info(LogSource, "RoundFailed", $"targetWord={CurrentWord()}, score={score:F1}");

        busy = false;
        SetMicUI(enabled: true, label: "Tap to Speak");

        if (feedbackText) feedbackText.text = $"Score: {Mathf.RoundToInt(score)}% — Try again!";

        SetAllFoodPenguinsOff();
        if (penguin) penguin.SetActive(true);
    }

    private IEnumerator NextAfterDelay()
    {
        yield return new WaitForSeconds(feedbackSeconds);

        index++;
        if (index >= words.Length) index = 0;

        StartRound(index);
    }

    private IEnumerator QuickRetry(string msg)
    {
        BackendLogger.Verbose(true, LogSource, "QuickRetry", $"message={msg}");

        if (feedbackText) feedbackText.text = msg;
        yield return new WaitForSeconds(0.9f);

        busy = false;
        SetMicUI(enabled: true, label: "Tap to Speak");
        if (wordText) wordText.text = $"Say:\n{CurrentWord().ToUpper()}";
    }
    private IEnumerator CookieBounce()
    {
        if (penguinRectTransform != null)
        {
            Vector2 start = penguinRectTransform.anchoredPosition;
            Vector2 up = start + Vector2.up * bounceAmount;

            yield return LerpAnchoredPosition(penguinRectTransform, start, up, bounceUpSeconds);
            yield return LerpAnchoredPosition(penguinRectTransform, up, start, bounceDownSeconds);

            penguinRectTransform.anchoredPosition = start;
            yield break;
        }

        if (penguinTransform != null)
        {
            Vector3 start = penguinTransform.localPosition;
            Vector3 up = start + Vector3.up * bounceAmount;

            yield return LerpLocalPosition(penguinTransform, start, up, bounceUpSeconds);
            yield return LerpLocalPosition(penguinTransform, up, start, bounceDownSeconds);

            penguinTransform.localPosition = start;
        }
    }

    private IEnumerator LerpAnchoredPosition(RectTransform rt, Vector2 from, Vector2 to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(from, to, t / seconds);
            yield return null;
        }
    }

    private IEnumerator LerpLocalPosition(Transform tr, Vector3 from, Vector3 to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            tr.localPosition = Vector3.Lerp(from, to, t / seconds);
            yield return null;
        }
    }

    private string CurrentWord() => words[index];

    private void SetMicUI(bool enabled, string label)
    {
        if (micButton) micButton.interactable = enabled;
        if (micButtonText) micButtonText.text = label;
    }

    private AudioSource CurrentPrompt()
    {
        return index switch
        {
            0 => promptApple,
            1 => promptPizza,
            2 => promptCookie,
            3 => promptBanana,
            _ => promptApple
        };
    }

    private void PlayPrompt()
    {
        StopAllPrompts();
        var p = CurrentPrompt();
        if (p && p.clip) p.Play();
    }

    private float CurrentPromptLength()
    {
        var p = CurrentPrompt();
        return (p && p.clip) ? p.clip.length : 0f;
    }

    private void StopAllPrompts()
    {
        if (promptApple) promptApple.Stop();
        if (promptPizza) promptPizza.Stop();
        if (promptCookie) promptCookie.Stop();
        if (promptBanana) promptBanana.Stop();
    }

    private void StopAllFailFeedback()
    {
        if (failAppleSource) failAppleSource.Stop();
        if (failPizzaSource) failPizzaSource.Stop();
        if (failCookieSource) failCookieSource.Stop();
        if (failBananaSource) failBananaSource.Stop();
    }

    private void PlayFailFeedback()
    {
        AudioSource source = index switch
        {
            0 => failAppleSource,
            1 => failPizzaSource,
            2 => failCookieSource,
            3 => failBananaSource,
            _ => null
        };

        if (source == null || source.clip == null)
        {
            BackendLogger.Warn(LogSource, "FailFeedbackMissing", $"targetWord={CurrentWord()}, index={index}");
            return;
        }

        source.Play();
    }

    private void PrepareParticles(ParticleSystem ps)
    {
        if (ps == null) return;
        if (!ps.gameObject.activeInHierarchy) ps.gameObject.SetActive(true);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Clear(true);
    }

    private void RestartParticles(ParticleSystem ps)
    {
        if (ps == null) return;
        PrepareParticles(ps);
        ps.Play(true);
    }

    private void SetAllFoodPenguinsOff()
    {
        if (penguinApple) penguinApple.SetActive(false);
        if (penguinPizza) penguinPizza.SetActive(false);
        if (penguinCookie) penguinCookie.SetActive(false);
        if (penguinBanana) penguinBanana.SetActive(false);
    }

    private void ShowFoodPenguin()
    {
        if (penguin) penguin.SetActive(false);
        SetAllFoodPenguinsOff();

        if (index == 0 && penguinApple) penguinApple.SetActive(true);
        if (index == 1 && penguinPizza) penguinPizza.SetActive(true);
        if (index == 2 && penguinCookie) penguinCookie.SetActive(true);
        if (index == 3 && penguinBanana) penguinBanana.SetActive(true);
    }
}