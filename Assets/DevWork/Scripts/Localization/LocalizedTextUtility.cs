using Lean.Localization;
using TMPro;
using UnityEngine;

public static class LocalizedTextUtility
{
    public static bool HasTranslation(string key)
    {
        return string.IsNullOrWhiteSpace(key) == false && LeanLocalization.GetTranslation(key) != null;
    }

    public static string GetLocalizedString(string key, string fallback = "")
    {
        var translation = string.IsNullOrWhiteSpace(key) ? null : LeanLocalization.GetTranslation(key);
        if (translation != null && translation.Data is string text)
            return text;

        return fallback;
    }

    public static void ApplyLanguageStyle(TMP_Text text, bool worldText, TMP_FontAsset defaultFont, Material defaultMaterial)
    {
        if (text == null)
            return;

        var manager = LocalizationManager.Ins;
        if (manager == null || manager.UsesDefaultFont(manager.CurrentLanguage))
        {
            if (defaultFont != null)
                text.font = defaultFont;
            if (defaultMaterial != null)
                text.fontSharedMaterial = defaultMaterial;
            return;
        }

        if (manager.TryGetTextStyle(worldText, out var font, out var material))
        {
            if (font != null)
            {
                text.font = font;
                if (material == null)
                    text.fontSharedMaterial = font.material;
            }
            if (material != null)
                text.fontSharedMaterial = material;
        }
    }
}
