using System;
using System.Collections.Generic;

public static class LocalizationLanguageUtility
{
    private static readonly GameLanguage[] allLanguages =
    {
        GameLanguage.English,
        GameLanguage.Vietnamese,
        GameLanguage.Spanish,
        GameLanguage.Portuguese,
        GameLanguage.German,
        GameLanguage.Indonesian,
        GameLanguage.Japanese,
        GameLanguage.Korean,
        GameLanguage.ChineseSimplified,
        GameLanguage.ChineseTraditional,
        GameLanguage.Thai,
        GameLanguage.Arabic,
        GameLanguage.Russian
    };

    private static readonly HashSet<GameLanguage> defaultFontLanguages = new HashSet<GameLanguage>
    {
        GameLanguage.English,
        GameLanguage.Vietnamese,
        GameLanguage.Spanish,
        GameLanguage.Portuguese,
        GameLanguage.German,
        GameLanguage.Indonesian
    };

    public static IReadOnlyList<GameLanguage> AllLanguages => allLanguages;

    public static string GetDefaultLeanLanguageName(GameLanguage language)
    {
        switch (language)
        {
            case GameLanguage.English: return "English";
            case GameLanguage.Vietnamese: return "Vietnamese";
            case GameLanguage.Spanish: return "Spanish";
            case GameLanguage.Portuguese: return "Portuguese";
            case GameLanguage.German: return "German";
            case GameLanguage.Indonesian: return "Indonesian";
            case GameLanguage.Japanese: return "Japanese";
            case GameLanguage.Korean: return "Korean";
            case GameLanguage.ChineseSimplified: return "ChineseSimplified";
            case GameLanguage.ChineseTraditional: return "ChineseTraditional";
            case GameLanguage.Thai: return "Thai";
            case GameLanguage.Arabic: return "Arabic";
            case GameLanguage.Russian: return "Russian";
            default: return language.ToString();
        }
    }

    public static string GetDefaultDisplayName(GameLanguage language)
    {
        switch (language)
        {
            case GameLanguage.English: return "English";
            case GameLanguage.Vietnamese: return "Vietnamese";
            case GameLanguage.Spanish: return "Spanish";
            case GameLanguage.Portuguese: return "Portuguese";
            case GameLanguage.German: return "German";
            case GameLanguage.Indonesian: return "Indonesian";
            case GameLanguage.Japanese: return "Japanese";
            case GameLanguage.Korean: return "Korean";
            case GameLanguage.ChineseSimplified: return "Chinese Simplified";
            case GameLanguage.ChineseTraditional: return "Chinese Traditional";
            case GameLanguage.Thai: return "Thai";
            case GameLanguage.Arabic: return "Arabic";
            case GameLanguage.Russian: return "Russian";
            default: return language.ToString();
        }
    }

    public static string GetDefaultCultureCode(GameLanguage language)
    {
        switch (language)
        {
            case GameLanguage.English: return "en";
            case GameLanguage.Vietnamese: return "vi";
            case GameLanguage.Spanish: return "es";
            case GameLanguage.Portuguese: return "pt";
            case GameLanguage.German: return "de";
            case GameLanguage.Indonesian: return "id";
            case GameLanguage.Japanese: return "ja";
            case GameLanguage.Korean: return "ko";
            case GameLanguage.ChineseSimplified: return "zh-CN";
            case GameLanguage.ChineseTraditional: return "zh-TW";
            case GameLanguage.Thai: return "th";
            case GameLanguage.Arabic: return "ar";
            case GameLanguage.Russian: return "ru";
            default: return language.ToString();
        }
    }

    public static bool UsesDefaultFont(GameLanguage language)
    {
        return defaultFontLanguages.Contains(language);
    }

    public static bool TryParseLanguage(string value, out GameLanguage language)
    {
        language = GameLanguage.English;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = Normalize(value);
        foreach (var candidate in allLanguages)
        {
            if (Normalize(candidate.ToString()) == normalized ||
                Normalize(GetDefaultLeanLanguageName(candidate)) == normalized ||
                Normalize(GetDefaultDisplayName(candidate)) == normalized ||
                Normalize(GetDefaultCultureCode(candidate)) == normalized)
            {
                language = candidate;
                return true;
            }
        }

        if (normalized.StartsWith("en", StringComparison.Ordinal)) { language = GameLanguage.English; return true; }
        if (normalized.StartsWith("vi", StringComparison.Ordinal)) { language = GameLanguage.Vietnamese; return true; }
        if (normalized.StartsWith("es", StringComparison.Ordinal)) { language = GameLanguage.Spanish; return true; }
        if (normalized.StartsWith("pt", StringComparison.Ordinal)) { language = GameLanguage.Portuguese; return true; }
        if (normalized.StartsWith("de", StringComparison.Ordinal)) { language = GameLanguage.German; return true; }
        if (normalized.StartsWith("id", StringComparison.Ordinal)) { language = GameLanguage.Indonesian; return true; }
        if (normalized.StartsWith("ja", StringComparison.Ordinal)) { language = GameLanguage.Japanese; return true; }
        if (normalized.StartsWith("ko", StringComparison.Ordinal)) { language = GameLanguage.Korean; return true; }
        if (normalized == "zhcn" || normalized == "zhhans") { language = GameLanguage.ChineseSimplified; return true; }
        if (normalized == "zhtw" || normalized == "zhhant") { language = GameLanguage.ChineseTraditional; return true; }
        if (normalized.StartsWith("th", StringComparison.Ordinal)) { language = GameLanguage.Thai; return true; }
        if (normalized.StartsWith("ar", StringComparison.Ordinal)) { language = GameLanguage.Arabic; return true; }
        if (normalized.StartsWith("ru", StringComparison.Ordinal)) { language = GameLanguage.Russian; return true; }

        return false;
    }

    private static string Normalize(string value)
    {
        return value.Trim().Replace("-", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
    }
}
