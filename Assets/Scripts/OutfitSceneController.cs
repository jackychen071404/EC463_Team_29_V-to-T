using System.Collections;
using TMPro;
using UnityEngine;

public class OutfitSceneController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private GameObject feedbackPanel;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private TMP_Text recordButtonText;

    [Header("Outfit Pieces")]
    [SerializeField] private GameObject hat;
    [SerializeField] private GameObject shirt;
    [SerializeField] private GameObject pants;
    [SerializeField] private GameObject leftShoe;
    [SerializeField] private GameObject rightShoe;

    [Header("Recording")]
    [SerializeField] private int recordHz = 16000;
    [SerializeField] private float maxRecordSeconds = 3f;  
    [SerializeField] private float minListenSeconds = 0.25f;    

    [Header("Scoring")]
    [Range(0, 100)] [SerializeField] private float passThreshold = 80f;
    [SerializeField] private int randomMinScore = 50;
    [SerializeField] private int randomMaxScore = 100;

    [Header("Timing")]
    [SerializeField] private float introDelaySeconds = 3f;
    [SerializeField] private float feedbackHideSeconds = 5f;

    private bool busy = false;
    private bool isListening = false;
    private float listenStartTime;

    private AudioClip clip;
    private string device;
    private Coroutine autoStopCoroutine;
    private Coroutine hideFeedbackCoroutine;

    private void Start()
    {
        device = (Microphone.devices.Length > 0) ? Microphone.devices[0] : null;

        RefreshUnlockedVisibility();
        HideFeedbackNow();
        StartCoroutine(IntroSequence());
    }

    private IEnumerator IntroSequence()
    {
        busy = true;

        if (promptText)
            promptText.text = "Hi! Let’s help the penguin get dressed for the day!";

        yield return new WaitForSeconds(introDelaySeconds);

        UpdatePrompt();
        busy = false;

        // enable mic after intro
        SetMicUI(enabled: true, label: "Tap to Speak");
    }

    private void UpdatePrompt()
    {
        string w = GameManager.I.CurrentWord;

        if (string.IsNullOrEmpty(w))
        {
            if (promptText) promptText.text = "All done! Penguin looks AMAZING!";
            SetMicUI(enabled: false, label: "Done!");
            return;
        }

        string msg = w switch
        {
            "hat"   => "Let’s start with a HAT!\nSay: HAT",
            "shirt" => "Time for a SHIRT!\nSay: SHIRT",
            "pants" => "Now PANTS!\nSay: PANTS",
            "shoes" => "Last step—SHOES!\nSay: SHOES",
            _       => $"Say: {w.ToUpper()}"
        };

        if (promptText) promptText.text = msg;
    }

    public void OnRecordPressed()
    {
        if (busy) return;

        if (!isListening)
            StartListening();
        else
            StopListeningAndScore();
    }

    private void StartListening()
    {
        HideFeedbackNow();

        if (device == null)
        {
            if (promptText) promptText.text = "Can't find a microphone.";
            return;
        }

        isListening = true;
        listenStartTime = Time.time;

        SetMicUI(enabled: true, label: "Tap to Stop");

        clip = Microphone.Start(device, false, Mathf.CeilToInt(maxRecordSeconds), recordHz);

        if (autoStopCoroutine != null) StopCoroutine(autoStopCoroutine);
        autoStopCoroutine = StartCoroutine(AutoStopAfter(maxRecordSeconds));
    }

    private IEnumerator AutoStopAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (isListening)
            StopListeningAndScore();
    }

    private void StopListeningAndScore()
    {
        if (!isListening) return;

        busy = true;
        isListening = false;

        if (autoStopCoroutine != null) StopCoroutine(autoStopCoroutine);
        autoStopCoroutine = null;

        SetMicUI(enabled: false, label: "Scoring...");

        float listenedFor = Time.time - listenStartTime;

        int pos = Microphone.GetPosition(device);
        Microphone.End(device);

        if (listenedFor < minListenSeconds)
        {
            StartCoroutine(QuickRetry("Hold it for a sec"));
            return;
        }

        if (pos <= 0 || clip == null)
        {
            ShowFeedback(0f, false, "I didn’t catch your voice, try again!");
            busy = false;
            SetMicUI(enabled: true, label: "Tap to Speak");
            UpdatePrompt();
            return;
        }

        AudioClip trimmed = TrimClip(clip, pos);

        // SCORING HERE
        float score = Random.Range(randomMinScore, randomMaxScore + 1);
        bool pass = score >= passThreshold;

        if (pass) HandlePass(score);
        else HandleFail(score);

        busy = false;
        SetMicUI(enabled: true, label: "Tap to Speak");
        UpdatePrompt();
    }

    private void HandlePass(float score)
    {
        ShowFeedback(score, true, "Awesome! Penguin is so happy!");

        string word = GameManager.I.CurrentWord;
        UnlockForWord(word);
        RefreshUnlockedVisibility();

        GameManager.I.AdvanceWord();
    }

    private void HandleFail(float score)
    {
        ShowFeedback(score, false, "Almost! Let’s try it one more time.");
    }

    private void SetMicUI(bool enabled, string label)
    {
        if (recordButtonText) recordButtonText.text = label;
    }

    private IEnumerator QuickRetry(string msg)
    {
        if (feedbackText && feedbackPanel)
        {
            feedbackPanel.SetActive(true);
            feedbackText.text = msg;
        }

        yield return new WaitForSeconds(0.9f);

        HideFeedbackNow();
        busy = false;
        SetMicUI(enabled: true, label: "Tap to Speak");
        UpdatePrompt();
    }

    private AudioClip TrimClip(AudioClip source, int samplesToKeep)
    {
        samplesToKeep = Mathf.Max(1, samplesToKeep);

        int channels = source.channels;
        float[] data = new float[samplesToKeep * channels];
        source.GetData(data, 0);

        AudioClip newClip = AudioClip.Create("TrimmedRecording", samplesToKeep, channels, source.frequency, false);
        newClip.SetData(data, 0);
        return newClip;
    }

    private void UnlockForWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return;

        if (word == "shoes")
        {
            GameManager.I.Unlock("left_shoe");
            GameManager.I.Unlock("right_shoe");
        }
        else
        {
            GameManager.I.Unlock(word);
        }
    }

    private void RefreshUnlockedVisibility()
    {
        if (hat) hat.SetActive(GameManager.I.IsUnlocked("hat"));
        if (shirt) shirt.SetActive(GameManager.I.IsUnlocked("shirt"));
        if (pants) pants.SetActive(GameManager.I.IsUnlocked("pants"));

        if (leftShoe) leftShoe.SetActive(GameManager.I.IsUnlocked("left_shoe"));
        if (rightShoe) rightShoe.SetActive(GameManager.I.IsUnlocked("right_shoe"));
    }

    private void ShowFeedback(float score, bool pass, string extraLine)
    {
        if (!feedbackPanel || !feedbackText) return;

        feedbackPanel.SetActive(true);

        int s = Mathf.RoundToInt(score);
        string main = pass ? "Nice job!" : "Try again";
        feedbackText.text = $"Score: {s}%\n{main}\n{extraLine}";

        if (hideFeedbackCoroutine != null) StopCoroutine(hideFeedbackCoroutine);
        hideFeedbackCoroutine = StartCoroutine(HideFeedbackAfter(feedbackHideSeconds));
    }

    private IEnumerator HideFeedbackAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (feedbackPanel) feedbackPanel.SetActive(false);
    }

    private void HideFeedbackNow()
    {
        if (hideFeedbackCoroutine != null) StopCoroutine(hideFeedbackCoroutine);
        hideFeedbackCoroutine = null;
        if (feedbackPanel) feedbackPanel.SetActive(false);
    }

    public void OnResetPressed()
    {
        GameManager.I.ResetProgress();
        RefreshUnlockedVisibility();
        HideFeedbackNow();

        // stop recording cleanly
        if (isListening && device != null)
        {
            Microphone.End(device);
            isListening = false;
        }

        if (autoStopCoroutine != null) StopCoroutine(autoStopCoroutine);
        autoStopCoroutine = null;

        StopAllCoroutines();
        SetMicUI(enabled: false, label: "Listening...");
        StartCoroutine(IntroSequence());
    }
}