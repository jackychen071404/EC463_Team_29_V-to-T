using UnityEngine;
using UnityEngine.UI;

public class FridgeManager : MonoBehaviour
{
    public GameObject fridgeClosed;
    public GameObject fridgeOpen;

    public void OpenFridge()
    {
        fridgeClosed.SetActive(false);
        fridgeOpen.SetActive(true);
    }
}
