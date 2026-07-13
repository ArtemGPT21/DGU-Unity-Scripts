using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LocalizedText : MonoBehaviour
{
    public string ru;
    public string en;

    Text uiText;
    TextMeshPro tmpText;

    void Awake()
    {
        uiText = GetComponent<Text>();
        tmpText = GetComponent<TextMeshPro>();

        LanguageManager.OnLanguageChanged += UpdateText;
    }

    void Start()
    {
        UpdateText();
    }

    void OnDestroy()
    {
        LanguageManager.OnLanguageChanged -= UpdateText;
    }

    public void UpdateText()
    {
        string text = LanguageManager.currentLanguage == 0 ? ru : en;

        if (uiText != null) uiText.text = text;
        if (tmpText != null) tmpText.text = text;
    }
}
