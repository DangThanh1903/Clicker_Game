using System;
using Lean.Localization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshPro))]
public class LocalizedTextInGame : LeanLocalizedTextMeshPro
{
    [SerializeField] private string nativeText;
    [SerializeField] private string prefix;
    [SerializeField] private string suffix;

    private TextMeshPro cachedText;
    private TMP_FontAsset defaultFont;
    private Material defaultMaterial;

    public TextMeshPro Text
    {
        get
        {
            if (cachedText == null)
                cachedText = GetComponent<TextMeshPro>();
            return cachedText;
        }
    }

    public void ChangeTranslationKey(string key, bool forceChange = false)
    {
        if (!forceChange && TranslationName == key)
            return;

        if (string.IsNullOrWhiteSpace(nativeText))
            nativeText = Text != null ? Text.text : string.Empty;

        FallbackText = nativeText;

        if (!string.IsNullOrWhiteSpace(key) && LocalizedTextUtility.HasTranslation(key) == false)
        {
            TranslationName = key;
            UpdateTranslation(null);
            return;
        }

        TranslationName = key;
    }

    public override void UpdateTranslation(LeanTranslation translation)
    {
        base.UpdateTranslation(translation);

        var text = Text;
        if (text == null)
            return;

        text.text = prefix + ProcessText(text.text) + suffix;
        LocalizedTextUtility.ApplyLanguageStyle(text, true, defaultFont, defaultMaterial);
    }

    protected override void Awake()
    {
        cachedText = GetComponent<TextMeshPro>();
        CaptureDefaultStyle();

        base.Awake();

        if (string.IsNullOrWhiteSpace(nativeText))
            nativeText = cachedText != null ? cachedText.text : string.Empty;

        if (string.IsNullOrWhiteSpace(FallbackText))
            FallbackText = nativeText;
    }

    protected virtual string ProcessText(string text)
    {
        return text;
    }

    protected string FormatTextSafe(string format, params object[] args)
    {
        if (string.IsNullOrEmpty(format) || args == null || args.Length == 0)
            return format;

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private void CaptureDefaultStyle()
    {
        var text = Text;
        if (text == null)
            return;

        if (defaultFont == null)
            defaultFont = text.font;
        if (defaultMaterial == null)
            defaultMaterial = text.fontSharedMaterial;
    }
}
