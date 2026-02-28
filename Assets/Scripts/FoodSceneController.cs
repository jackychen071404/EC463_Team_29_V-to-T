using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class FoodSceneController : MonoBehaviour
{
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
            Debug.LogWarning("[FoodSceneController] No bounce target assigned. Cookie bounce will do nothing.");

        StartRound(0);
    }

    private void GoToNextScene()
    {
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError("[FoodSceneController] nextSceneName is empty.");
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private void StartRound(int i)
    {
        index = Mathf.Clamp(i, 0, words.Length - 1);

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

        if (wordText) wordText.text = $"Say:\n{CurrentWord().ToUpper()}";
    }
    // ADD MIC USAGE AND SCORE HERE
    private void OnMicPressed()
    {
        if (busy) return;

        // Tap 1: start listening
        if (!isListening)
        {
            isListening = true;
            listenStartTime = Time.time;

            StopAllFailFeedback();
            if (feedbackText) feedbackText.text = "";

            SetMicUI(enabled: true, label: "Tap to Stop");
            return;
        }

        // Tap 2: stop and score
        isListening = false;
        busy = true;

        SetMicUI(enabled: false, label: "Scoring...");

        float listenedFor = Time.time - listenStartTime;
        if (listenedFor < minListenSeconds)
        {
            StartCoroutine(QuickRetry("Hold it for a sec"));
            return;
        }

        // SCORING ALGORITHM WOULD GO HERE
        float score = Random.Range(randomMinScore, randomMaxScore + 1);
        bool pass = score >= passThreshold;

        if (pass) ShowSuccess(score);
        else ShowFail(score);
    }

    private void ShowSuccess(float score)
    {
        busy = true;
        StopAllFailFeedback();

        string w = CurrentWord();

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
            Debug.LogWarning("[FoodSceneController] Fail feedback missing on this word.");
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