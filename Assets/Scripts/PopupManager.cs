using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using Unity.InferenceEngine;

public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance;

    [Header("Popup Elements")]
    public GameObject startPopupPanel;
    public GameObject popupPanel;
    public TextMeshProUGUI prompt;
    public TextMeshProUGUI feedback;
    public Button speak;
    private BubbleController currentBubble;

    public ModelAsset wav2vecModel;


    void Awake()
    {
        Instance = this;

        if (startPopupPanel != null)
            startPopupPanel.SetActive(true);
        if (popupPanel != null)
            popupPanel.SetActive(false);
    }

    public void OnStartButtonPressed()
    {
        if (startPopupPanel != null && startPopupPanel.activeSelf)
        {
            startPopupPanel.SetActive(false);
        }
    }

    public void OpenPopup(BubbleController bubble)
    {
        currentBubble = bubble;

        if (popupPanel != null)
            popupPanel.SetActive(true);

        // Show prompt, hide feedback
        prompt.gameObject.SetActive(true);
        feedback.gameObject.SetActive(false);
        prompt.text = $"Say the number {bubble.bubbleNumber}";

        // Setup speak button
        speak.interactable = true;
        speak.onClick.RemoveAllListeners();
        speak.onClick.AddListener(() => StartCoroutine(ProcessSpeech(bubble.bubbleNumber)));
    }

    public void ClosePopup()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);

        currentBubble = null;
    }

    private IEnumerator ProcessSpeech(int bubbleNumber)
    {
        // Safety check
        if (VoiceRecorder.Instance == null)
        {
            Debug.LogError("VoiceRecorder instance not found!");
            feedback.text = "Error: Microphone not ready";
            yield break;
        }

        // Disable button and show recording feedback
        speak.interactable = false;
        prompt.gameObject.SetActive(false);
        feedback.gameObject.SetActive(true);
        feedback.text = "Recording... Speak now!";

        // Start recording
        VoiceRecorder.Instance.StartRecording();

        // Log the interaction
        UINumberLogger logger = Object.FindFirstObjectByType<UINumberLogger>();
        if (logger != null)
            logger.OnSpeakPressed();

        // Wait for recording duration
        yield return new WaitForSeconds(VoiceRecorder.Instance.recordingDuration);

        // Stop and save recording
        VoiceRecorder.Instance.StopAndSaveRecording();

        feedback.text = "Processing speech...";

        // Get the recording path
        string recordingPath = VoiceRecorder.Instance.GetLatestRecordingPath();
        Debug.Log($"Processing recording: {recordingPath}");
        
        string numberToString = changeNumberToString(bubbleNumber);

        Wav2VecManager.Instance.GetScoreFromFile(recordingPath, numberToString, async (score) =>
        {
            if (score < 0f)
            {
                feedback.text = "Error processing audio. Please try again.";
                speak.interactable = true;
                return;
            }

            if (score > 70f)
            {
                feedback.text = "Correct! \n" + $" (Score: {score:F1})";

                // Pop the bubble and close popup
                if (currentBubble != null)
                    currentBubble.PopBubble();

                ClosePopup();
            }
            else
            {
                feedback.text = "Try again! \n" + $" (Score: {score:F1})";

                // // Reset UI to let them try again
                // prompt.gameObject.SetActive(true);
                // feedback.gameObject.SetActive(false);
                // speak.interactable = true;
            }
        });
    }

    private string changeNumberToString(int number)
    {
        switch (number)
        {
            case 0: return "zero";
            case 1: return "one";
            case 2: return "two";
            case 3: return "three";
            case 4: return "four";
            case 5: return "five";
            case 6: return "six";
            case 7: return "seven";
            case 8: return "eight";
            case 9: return "nine";
            default: return number.ToString();
        }
    }
}