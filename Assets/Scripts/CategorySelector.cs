using UnityEngine;
using UnityEngine.SceneManagement;

public class CategorySelector : MonoBehaviour
{
    public static string selectedCategory;

    public void SelectCategory(string category)
    {
        selectedCategory = category;

        // load the correct scene based on what was clicked
        if (category == "Numbers")
        {
            SceneManager.LoadScene("NumberPractice");
        }
        else if (category == "Colors")
        {
            SceneManager.LoadScene("ColorPractice");
        }
        else if (category == "Food")
        {
            SceneManager.LoadScene("FoodPractice");
        }
        else
        {
            Debug.LogWarning("No matching scene for category: " + category);
        }
    }
}
