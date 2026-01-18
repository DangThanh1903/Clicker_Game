using UnityEngine;
using System.Collections;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class LocaleSwitcher : MonoBehaviour
{
    const string PREF_KEY = "game.locale.code";

    void Awake()
    {
        StartCoroutine(InitLocale());
    }

    IEnumerator InitLocale()
    {
        var init = LocalizationSettings.InitializationOperation;
        if (!init.IsDone)
            yield return init;

        var code = PlayerPrefs.GetString(PREF_KEY, "en");
        SetLocaleByCode(code);
    }

    public void SetLocaleByCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return;
        string want = code.ToLowerInvariant();
        foreach (var loc in LocalizationSettings.AvailableLocales.Locales)
        {
            string locCode = loc.Identifier.Code?.ToLowerInvariant();
            string culture = loc.Identifier.CultureInfo?.Name?.ToLowerInvariant();
            if (locCode == want || culture == want ||
                (!string.IsNullOrEmpty(locCode) && locCode.StartsWith(want)))
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
