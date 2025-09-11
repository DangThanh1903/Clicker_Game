using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LocaleSwitcher : MonoBehaviour
{
    const string PREF_KEY = "game.locale.code";

    void Awake()
    {
        var code = PlayerPrefs.GetString(PREF_KEY, "en");
        SetLocaleByCode(code);
    }

    public void SetLocaleByCode(string code)
    {
        foreach (var loc in LocalizationSettings.AvailableLocales.Locales)
        {
            if (loc.Identifier.Code == code || loc.Identifier.CultureInfo?.Name == code)
            {
                LocalizationSettings.SelectedLocale = loc;
                PlayerPrefs.SetString(PREF_KEY, loc.Identifier.Code);
                PlayerPrefs.Save();
                return;
            }
        }
        Debug.LogWarning($"[LocaleSwitcher] Locale '{code}' not found.");
    }

    public void ToggleEnglishVietnamese()
    {
        var cur = LocalizationSettings.SelectedLocale;
        var next = (cur != null && cur.Identifier.Code.StartsWith("vi")) ? "en" : "vi";
        SetLocaleByCode(next);
    }

    public void SetEnglish()   => SetLocaleByCode("en");
    public void SetVietnamese()=> SetLocaleByCode("vi");
}
