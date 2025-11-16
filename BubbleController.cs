using UnityEngine;
using UnityEngine.UI;

public class BubbleController : MonoBehaviour
{
    public int bubbleNumber;                      // set unique number in Inspector
    private RectTransform rect;
    private Button button;

    void Start()
    {
        rect = GetComponent<RectTransform>();
        button = GetComponent<Button>();
        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
        }

        button.onClick.AddListener(OnBubbleClicked);
    }

    void OnBubbleClicked()
    {
        PopupManager.Instance.OpenPopup(this);
        UINumberLogger logger = Object.FindFirstObjectByType<UINumberLogger>();
if (logger != null) logger.OnBubbleClicked();

    }

    public void PopBubble()
    {
        Destroy(gameObject);
    }
}
