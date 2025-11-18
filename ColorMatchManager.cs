using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ColorMatchManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject popupPanel;
    public TextMeshProUGUI prompt;
    public TextMeshProUGUI feedback;
    public Button recordButton;

    [Header("Color Shapes")]
    public Image redShape;
    public Image blueShape;
    public Image greenShape;
    public Image yellowShape;
    public Image purpleShape;

    private List<Image> activeShapes = new();
    private string targetColor;
    private float accuracyThreshold = 85f;

    void Start()
    {
        popupPanel.SetActive(false);
        activeShapes.AddRange(new[] { redShape, blueShape, greenShape, yellowShape, purpleShape });
        recordButton.onClick.AddListener(() => StartCoroutine(ProcessSpeech()));
        StartCoroutine(StartNextRound());
    }

    private IEnumerator StartNextRound()
    {
        if (activeShapes.Count == 0)
        {
            feedback.gameObject.SetActive(true);
            feedback.text = "Yay! All colors matched! Great job!";
            yield break;
        }

        yield return new WaitForSeconds(1f);

        popupPanel.SetActive(true);
        prompt.gameObject.SetActive(true);
        feedback.gameObject.SetActive(false);

        // pick a random color
        int i = Random.Range(0, activeShapes.Count);
        targetColor = activeShapes[i].name.Replace("Shape", "");  
        prompt.text = $"Say the color {targetColor}";
    }

    private IEnumerator ProcessSpeech()
    {
        recordButton.interactable = false;
        prompt.gameObject.SetActive(false);
        feedback.gameObject.SetActive(true);
        feedback.text = "Processing speech...";

        yield return new WaitForSeconds(1f);

        float accuracy = Random.Range(60f, 100f);
        feedback.text = $"Accuracy: {accuracy:F1}%";

        if (accuracy >= accuracyThreshold)
        {
            feedback.text += "\n[Great!] Correct!";
            yield return new WaitForSeconds(0.7f);

            // remove the matched color
            Image matched = activeShapes.Find(x => x.name.Contains(targetColor));
            if (matched) matched.gameObject.SetActive(false);
            activeShapes.Remove(matched);

            popupPanel.SetActive(false);
            recordButton.interactable = true;
            StartCoroutine(StartNextRound());
        }
        else
        {
            feedback.text += "\n[X] Try again!";
            yield return new WaitForSeconds(1f);
            prompt.gameObject.SetActive(true);
            feedback.gameObject.SetActive(false);
            recordButton.interactable = true;
        }
    }
}
