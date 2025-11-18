using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

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

    void Awake()
    {
        Instance = this;

        // start popup at the beginning; feedback popup stays hidden
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

    // called by a bubble when clicked
    public void OpenPopup(BubbleController bubble)
    {
        currentBubble = bubble;

        if (popupPanel != null)
            popupPanel.SetActive(true);

        // show prompt when popup opens 
        prompt.gameObject.SetActive(true);
        feedback.gameObject.SetActive(false);

        prompt.text = $"Say the number {bubble.bubbleNumber}";
        speak.interactable = true;

        speak.onClick.RemoveAllListeners();
        speak.onClick.AddListener(() => StartCoroutine(ProcessSpeech()));
    }

    private IEnumerator ProcessSpeech()
    {
        speak.interactable = false;
        prompt.gameObject.SetActive(false);
        feedback.gameObject.SetActive(true);
        feedback.text = "Processing speech";

        UINumberLogger logger = Object.FindFirstObjectByType<UINumberLogger>();
        if (logger != null)
        logger.OnSpeakPressed();

        yield return new WaitForSeconds(1.0f);

        //random accuracy
        float accuracy = Random.Range(70f, 100f);
        feedback.text = $"Accuracy: {accuracy:F1}%";

        if (accuracy >= 85f)
        {
            //pop bubble and close popup
            feedback.text += "\n[OK!] Great job!";
            yield return new WaitForSeconds(0.6f);

            currentBubble.PopBubble();

             if (logger != null)
        logger.WaitForBubblePopOnSuccess();

            // reset before closing feedback popup
            prompt.gameObject.SetActive(true);
            feedback.gameObject.SetActive(false);
            popupPanel.SetActive(false);
        }
        else
        {
            feedback.text += "\n[X] Try again!";
            yield return new WaitForSeconds(1.0f);
            
            speak.interactable = true;
            feedback.gameObject.SetActive(false);
            prompt.gameObject.SetActive(true);
            prompt.text = $"Say the number {currentBubble.bubbleNumber}";
        }
    }
}
