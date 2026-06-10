using System;
using System.Collections.Generic;
using Lean.Localization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LeanLocalization))]
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Ins { get; private set; }

    public static event Action<GameLanguage> OnLanguageChanged;

    public const string PlayerPrefsKey = "game.language";
    private const string LegacyLocalePrefsKey = "game.locale.code";

    [SerializeField] private LeanLocalization leanLocalization;
    [SerializeField] private LanguageConfig languageConfig;
    [SerializeField] private GameLanguage defaultLanguage = GameLanguage.English;
    [SerializeField] private bool dontDestroyOnLoad = true;

    private GameLanguage currentLanguage;

    public event Action<GameLanguage> LanguageChanged;

    public GameLanguage CurrentLanguage => currentLanguage;
    public LanguageConfig Config => languageConfig;

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }

        Ins = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        if (leanLocalization == null)
            leanLocalization = GetComponent<LeanLocalization>();

        if (leanLocalization != null)
        {
            leanLocalization.DetectLanguage = LeanLocalization.DetectType.None;
            leanLocalization.SaveLoad = LeanLocalization.SaveLoadType.None;
            leanLocalization.DefaultLanguage = GetLeanLanguageName(defaultLanguage);
        }

        ApplyLanguage(LoadLanguage(), false);
    }

    private void OnDestroy()
    {
        if (Ins == this)
            Ins = null;
    }

    public static LocalizationManager GetOrCreateInstance()
    {
        if (Ins != null)
            return Ins;

        var existing = FindObjectOfType<LocalizationManager>();
        if (existing != null)
            return existing;

        var go = new GameObject(nameof(LocalizationManager));
        return go.AddComponent<LocalizationManager>();
    }

    public IReadOnlyList<GameLanguage> GetSupportedLanguages()
    {
        if (languageConfig != null && languageConfig.Entries.Count > 0)
        {
            var languages = new List<GameLanguage>();
            foreach (var entry in languageConfig.Entries)
            {
                if (entry != null && languages.Contains(entry.language) == false)
                    languages.Add(entry.language);
            }

            return languages;
        }

        return LocalizationLanguageUtility.AllLanguages;
    }

    public string GetDisplayName(GameLanguage language)
    {
        if (languageConfig != null)
            return languageConfig.GetDisplayName(language);

        return LocalizationLanguageUtility.GetDefaultDisplayName(language);
    }

    public string GetLeanLanguageName(GameLanguage language)
    {
        if (languageConfig != null)
            return languageConfig.GetLeanLanguageName(language);

        return LocalizationLanguageUtility.GetDefaultLeanLanguageName(language);
    }

    public bool UsesDefaultFont(GameLanguage language)
    {
        if (languageConfig != null)
            return languageConfig.UsesDefaultFont(language);

        return LocalizationLanguageUtility.UsesDefaultFont(language);
    }

    public void SetLanguage(GameLanguage language)
    {
        ApplyLanguage(language, true);
    }

    public void SetLanguageByCode(string code)
    {
        if (LocalizationLanguageUtility.TryParseLanguage(code, out var language))
        {
            SetLanguage(language);
            return;
        }

        Debug.LogWarning($"[LocalizationManager] Language '{code}' not found.", this);
    }

    public void ToggleEnglishVietnamese()
    {
        SetLanguage(currentLanguage == GameLanguage.Vietnamese ? GameLanguage.English : GameLanguage.Vietnamese);
    }

    public bool TryGetTextStyle(bool worldText, out TMP_FontAsset font, out Material material)
    {
        font = null;
        material = null;

        if (UsesDefaultFont(currentLanguage) || languageConfig == null)
            return false;

        if (languageConfig.TryGetEntry(currentLanguage, out var entry) == false || entry == null)
            return false;

        font = worldText ? entry.worldFont : entry.uiFont;
        material = worldText ? entry.worldFontMaterial : entry.uiFontMaterial;

        return font != null || material != null;
    }

    [ContextMenu("Update Localization Data")]
    public void UpdateLocalizationData()
    {
        foreach (var csv in GetComponentsInChildren<LeanLanguageCSV>(true))
        {
            csv.LoadFromSource();
        }

        LeanLocalization.UpdateTranslations();
    }

    public static void UpdateActiveLocalizationData()
    {
        if (Ins != null)
            Ins.UpdateLocalizationData();
        else
            LeanLocalization.UpdateTranslations();
    }

    private GameLanguage LoadLanguage()
    {
        string savedLanguage = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (LocalizationLanguageUtility.TryParseLanguage(savedLanguage, out var language))
            return language;

        string legacyLocale = PlayerPrefs.GetString(LegacyLocalePrefsKey, string.Empty);
        if (LocalizationLanguageUtility.TryParseLanguage(legacyLocale, out language))
            return language;

        return defaultLanguage;
    }

    private void ApplyLanguage(GameLanguage language, bool save)
    {
        bool changed = currentLanguage != language;
        currentLanguage = language;

        string leanLanguageName = GetLeanLanguageName(language);
        if (leanLocalization != null)
            leanLocalization.CurrentLanguage = leanLanguageName;

        LeanLocalization.SetCurrentLanguageAll(leanLanguageName);

        if (save)
        {
            PlayerPrefs.SetString(PlayerPrefsKey, language.ToString());
            PlayerPrefs.SetString(LegacyLocalePrefsKey, LocalizationLanguageUtility.GetDefaultCultureCode(language));
            PlayerPrefs.Save();
        }

        if (changed || save)
        {
            LanguageChanged?.Invoke(language);
            OnLanguageChanged?.Invoke(language);
        }
    }
}
