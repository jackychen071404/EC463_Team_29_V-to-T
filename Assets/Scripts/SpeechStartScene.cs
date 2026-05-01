using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SpeechStartScreen : MonoBehaviour
{
    [Header("Clouds")]
    public RectTransform[] clouds;

    [Header("Star")]
    public RectTransform star;

    [Header("Buttons")]
    public Button eatButton;
    public Button outfitButton;

    [Header("Scene Names")]
    public string eatSceneName    = "EatScene";
    public string outfitSceneName = "OutfitScene";

    // Cloud state
    Vector2[] cloudOrigins;
    float[]   cloudSpeeds;
    float[]   cloudPhases;

    // Star state
    Vector2 starOrigin;
    float   starTimer;

    void Start()
    {
        InitClouds();
        InitStar();

        if (eatButton)    eatButton.onClick.AddListener(()    => StartCoroutine(LoadScene(eatSceneName)));
        if (outfitButton) outfitButton.onClick.AddListener(() => StartCoroutine(LoadScene(outfitSceneName)));
    }

    void Update()
    {
        TickClouds();
        TickStar();
    }
    void InitClouds()
    {
        if (clouds == null || clouds.Length == 0) return;
        cloudOrigins = new Vector2[clouds.Length];
        cloudSpeeds  = new float[clouds.Length];
        cloudPhases  = new float[clouds.Length];
        for (int i = 0; i < clouds.Length; i++)
        {
            cloudOrigins[i] = clouds[i].anchoredPosition;
            cloudSpeeds[i]  = Random.Range(0.4f, 0.9f);
            cloudPhases[i]  = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    void TickClouds()
    {
        if (cloudOrigins == null) return;
        for (int i = 0; i < clouds.Length; i++)
        {
            float ox = Mathf.Sin(Time.time * cloudSpeeds[i] * 0.4f + cloudPhases[i]) * 14f;
            clouds[i].anchoredPosition = cloudOrigins[i] + new Vector2(ox, 0f);
        }
    }
    void InitStar()
    {
        if (star == null) return;
        starOrigin = star.anchoredPosition;
    }

    void TickStar()
    {
        if (star == null) return;
        starTimer += Time.deltaTime;
        star.anchoredPosition = starOrigin + new Vector2(0f, Mathf.Sin(starTimer * 1.8f) * 7f);
        star.localRotation    = Quaternion.Euler(0f, 0f, Mathf.Sin(starTimer * 1.1f) * 14f);
    }
    IEnumerator LoadScene(string sceneName)
    {
        yield return new WaitForSeconds(0.05f);
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    static Color Hex(string h)
    {
        ColorUtility.TryParseHtmlString("#" + h, out var c);
        return c;
    }
}
