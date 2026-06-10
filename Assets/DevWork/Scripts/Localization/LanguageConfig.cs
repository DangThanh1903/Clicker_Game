using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "DevWork/Localization/Language Config", fileName = "LanguageConfig")]
public class LanguageConfig : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public GameLanguage language;
        public string leanLanguageName;
        public string displayName;
        public string[] cultureCodes;
        public bool useDefaultFont = true;
        public TMP_FontAsset uiFont;
        public Material uiFontMaterial;
        public TMP_FontAsset worldFont;
        public Material worldFontMaterial;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    public bool TryGetEntry(GameLanguage language, out Entry entry)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].language == language)
            {
                entry = entries[i];
                return true;
            }
        }

        entry = null;
        return false;
    }

    public string GetLeanLanguageName(GameLanguage language)
    {
        if (TryGetEntry(language, out var entry) && !string.IsNullOrWhiteSpace(entry.leanLanguageName))
            return entry.leanLanguageName;

        return LocalizationLanguageUtility.GetDefaultLeanLanguageName(language);
    }

    public string GetDisplayName(GameLanguage language)
    {
        if (TryGetEntry(language, out var entry) && !string.IsNullOrWhiteSpace(entry.displayName))
            return entry.displayName;

        return LocalizationLanguageUtility.GetDefaultDisplayName(language);
    }

    public bool UsesDefaultFont(GameLanguage language)
    {
        if (TryGetEntry(language, out var entry))
            return entry.useDefaultFont;

        return LocalizationLanguageUtility.UsesDefaultFont(language);
    }
}
