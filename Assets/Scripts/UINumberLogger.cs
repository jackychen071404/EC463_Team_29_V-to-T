using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UINumberLogger : MonoBehaviour
{
    [Header("UI References")]
    public GameObject startPopupPanel;
    public Button startButton;

    public GameObject popupPanel;
    public Button speakButton;
    public TMP_Text feedbackText;

    public GameObject testBubble;   

    private float startPressedTime;
    private float bubbleClickedTime;
    private float speakPressedTime;

    void Start()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartPressed);

        if (speakButton != null)
            speakButton.onClick.AddListener(OnSpeakPressed);

        Debug.Log("<color=cyan>[NumberUITestLogger]</color> Ready for UI timing test.");
    }

    public void OnStartPressed()
    {
        startPressedTime = Time.time;
        Debug.Log($"▶️ Start pressed at {startPressedTime:F2}s");
        StartCoroutine(WaitForStartPopupToHide());
    }

    private IEnumerator WaitForStartPopupToHide()
    {
        yield return new WaitUntil(() => startPopupPanel == null || !startPopupPanel.activeSelf);
        float elapsed = Time.time - startPressedTime;
        Debug.Log($"⌛ Start popup disappeared after {elapsed:F2}s");
    }

    public void OnBubbleClicked()
    {
        bubbleClickedTime = Time.time;
        Debug.Log($"Bubble clicked at {bubbleClickedTime:F2}s");
        StartCoroutine(WaitForPopupAppear());
    }

    private IEnumerator WaitForPopupAppear()
    {
        yield return new WaitUntil(() => popupPanel.activeSelf);
        float elapsed = Time.time - bubbleClickedTime;
        Debug.Log($"Popup appeared after {elapsed:F2}s (from bubble click)");
    }

    public void OnSpeakPressed()
    {
        speakPressedTime = Time.time;
        Debug.Log($"Speak pressed at {speakPressedTime:F2}s");

        StartCoroutine(WaitForFeedback());
        StartCoroutine(WaitForBubblePopOnSuccess());
    }

    private IEnumerator WaitForFeedback()
    {
        yield return new WaitUntil(() =>
            feedbackText != null &&
            (feedbackText.text.ToLower().Contains("accuracy") ||
             feedbackText.text.ToLower().Contains("correct") ||
             feedbackText.text.ToLower().Contains("try again")));

        float elapsed = Time.time - speakPressedTime;
        Debug.Log($"Feedback text appeared after {elapsed:F2}s");
    }
    public IEnumerator WaitForBubblePopOnSuccess()
    {
        yield return new WaitUntil(() => testBubble == null || !testBubble.activeSelf);

        // only log timing if feedback shows success text
        if (feedbackText != null &&
            feedbackText.text.ToLower().Contains("great job"))
        {
            float elapsed = Time.time - speakPressedTime;
            Debug.Log($"Bubble popped after {elapsed:F2}s (successful trial)");
        }
        else
        {
            Debug.Log($"Bubble not popped (unsuccessful trial)");
        }
    }

    
}
