using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class PopupFood : MonoBehaviour
{
    public static PopupFood Instance;

    [Header("Popup Elements")]
    public GameObject popupPanel;
    public TextMeshProUGUI prompt;
    public TextMeshProUGUI feedback;
    public Button speak;

    private FoodController currentFood;

    void Awake()
    {
        Instance = this;
        popupPanel.SetActive(false);
    }

    public void OpenFoodPopup(FoodController food)
    {
        currentFood = food;
        popupPanel.SetActive(true);

        prompt.gameObject.SetActive(true);
        feedback.gameObject.SetActive(false);

        prompt.text = $"Say the word {food.foodName}";
        speak.interactable = true;

        speak.onClick.RemoveAllListeners();
        speak.onClick.AddListener(() => StartCoroutine(ProcessFoodSpeech()));
    }

    private IEnumerator ProcessFoodSpeech()
{
    Debug.Log("Speak button pressed");
    speak.interactable = false;

    prompt.gameObject.SetActive(false);
    feedback.gameObject.SetActive(true);
    feedback.text = "Processing speech";

    yield return new WaitForSeconds(1.0f);

    float accuracy = Random.Range(70f, 100f);
    feedback.text = $"Accuracy: {accuracy:F1}%";

    if (accuracy >= 85f)
    {
        feedback.text += "\n[OK!] Yummy!";
        yield return new WaitForSeconds(0.4f);

        // show reward character 
        currentFood.ShowCharacterReward();

        // character stays on screen 
        yield return new WaitForSeconds(1.2f);

        // hide food and then close the popup
        currentFood.EatFood();
        popupPanel.SetActive(false);
    }
    else
        {
        
        feedback.text += "\n[X] Try again!";
        yield return new WaitForSeconds(1f);

            // record button comes back
        speak.interactable = true;
        
        //feedback text disappears and prompt txt comes back
        feedback.gameObject.SetActive(false); 
        prompt.gameObject.SetActive(true);
        prompt.text = $"Say the word {currentFood.foodName}";
    }
}

}
