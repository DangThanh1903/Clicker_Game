using UnityEngine;

public class LocalizedStringParams : LocalizedText
{
    [SerializeField] private string[] editorParams;

    private object[] runtimeParams;

    public void SetParams(params object[] args)
    {
        runtimeParams = args;
        UpdateLocalization();
    }

    protected override string ProcessText(string text)
    {
        if (runtimeParams != null && runtimeParams.Length > 0)
            return FormatTextSafe(text, runtimeParams);

        if (editorParams != null && editorParams.Length > 0)
            return FormatTextSafe(text, editorParams);

        return text;
    }
}
