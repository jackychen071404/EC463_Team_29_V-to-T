using UnityEngine;
using UnityEngine.SceneManagement;

public class StartButtonController : MonoBehaviour
{
    public AudioSource musicSource;

    public void OnStartPressed()
    {
        musicSource.Play();
    }
}