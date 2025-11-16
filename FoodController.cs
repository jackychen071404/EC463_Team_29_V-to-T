using UnityEngine;
using UnityEngine.UI;

public class FoodController : MonoBehaviour
{
    public string foodName;              
    public GameObject characterImage;    // the character that appears when correct
    private Button button;

    void Start()
    {
        button = GetComponent<Button>();
        if (button == null)
            button = gameObject.AddComponent<Button>();
        button.onClick.AddListener(OnFoodClicked);

        if (characterImage != null)
            characterImage.SetActive(false); // hide at start
    }

    void OnFoodClicked()
    {
        Debug.Log("Clicked food: " + foodName);
        PopupFood.Instance.OpenFoodPopup(this);
    }

    public void EatFood()
    {
        // hide the food
        gameObject.SetActive(false);
    }

    public void ShowCharacterReward()
    {
        if (characterImage != null)
        {
            characterImage.SetActive(true);
            StartCoroutine(HideCharacterAfterDelay());
        }
    }

    private System.Collections.IEnumerator HideCharacterAfterDelay()
    {
        yield return new WaitForSeconds(1.2f);
        characterImage.SetActive(false);
    }
}
