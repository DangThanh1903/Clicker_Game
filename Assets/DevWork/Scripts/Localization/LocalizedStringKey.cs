using UnityEngine;

public class LocalizedStringKey : LocalizedText
{
    [SerializeField] private string placeholderKey;
    [SerializeField] private string placeholderFallback;

    public void SetPlaceholderKey(string key, string fallback = "")
    {
        placeholderKey = key;
        placeholderFallback = fallback;
        UpdateLocalization();
    }

    protected override string ProcessText(string text)
    {
        string placeholder = LocalizedTextUtility.GetLocalizedString(placeholderKey, placeholderFallback);
        return FormatTextSafe(text, placeholder);
    }
}
