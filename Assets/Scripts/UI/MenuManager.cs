using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class MenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;
    public GameObject extrasPanel;

    [Header("UI")]
    public CanvasGroup canvasGroup;
    public Text titleText;

    [Header("Audio")]
    public AudioSource music;
    public AudioSource sfx;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Background")]
    public RectTransform background;

    Vector3 startPos;

    void Start()
    {
        ShowMain();

        if (background != null)
            startPos = background.localPosition;

        StartCoroutine(FadeIn());
        StartCoroutine(TitleGlitch());
    }

    void Update()
    {
        if (background != null)
        {
            background.localPosition = startPos +
            new Vector3(Mathf.Sin(Time.time * 0.2f) * 10f,
                        Mathf.Cos(Time.time * 0.15f) * 10f, 0);
        }
    }

    // ===== SCENE =====
    public void PlayGame()
    {
        SceneManager.LoadScene(1);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    // ===== PANELS =====
    public void ShowMain()
    {
        mainPanel.SetActive(true);
        settingsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        extrasPanel.SetActive(false);
    }

    public void ShowSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void ShowCredits()
    {
        mainPanel.SetActive(false);
        creditsPanel.SetActive(true);
    }

    public void ShowExtras()
    {
        mainPanel.SetActive(false);
        extrasPanel.SetActive(true);
    }

    // ===== SETTINGS =====
    public void SetMusic(float v)
    {
        music.volume = v;
        PlayerPrefs.SetFloat("music", v);
    }

    public void SetSFX(float v)
    {
        sfx.volume = v;
        PlayerPrefs.SetFloat("sfx", v);
    }

    // ===== EFFECTS =====
    IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;

        canvasGroup.alpha = 0;
        while (canvasGroup.alpha < 1)
        {
            canvasGroup.alpha += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator TitleGlitch()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(6f, 12f));

            if (titleText != null)
            {
                string original = titleText.text;
                titleText.text = "DGU#%@";

                yield return new WaitForSeconds(0.1f);

                titleText.text = original;
            }
        }
    }

    // ===== SECRET =====
    public void Secret()
    {
        ShowExtras();
    }
}
