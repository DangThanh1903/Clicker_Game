using UnityEngine;

public class LocaleSwitcher : MonoBehaviour
{
    public void SetLocaleByCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return;

        LocalizationManager.GetOrCreateInstance().SetLanguageByCode(code);
    }

    public void ToggleEnglishVietnamese()
    {
        LocalizationManager.GetOrCreateInstance().ToggleEnglishVietnamese();
    }

    public void SetEnglish() => LocalizationManager.GetOrCreateInstance().SetLanguage(GameLanguage.English);
    public void SetVietnamese() => LocalizationManager.GetOrCreateInstance().SetLanguage(GameLanguage.Vietnamese);
}
