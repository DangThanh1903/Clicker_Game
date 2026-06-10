using UnityEngine;

public class LocalizedFormat : LocalizedText
{
    [SerializeField] private float number;

    public void SetNumber(float value)
    {
        number = value;
        UpdateLocalization();
    }

    public void SetNumber(int value)
    {
        number = value;
        UpdateLocalization();
    }

    protected override string ProcessText(string text)
    {
        return FormatTextSafe(text, number);
    }
}
