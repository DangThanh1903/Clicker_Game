using System;

public static class EnumLocalization
{
    public static string ToLocalizationKey<TEnum>(this TEnum value, string keyPrefix = "Enums")
        where TEnum : Enum
    {
        string enumType = typeof(TEnum).Name.ToLower();
        string key = $"{enumType}_{value.ToString().ToLower()}";
        return string.IsNullOrWhiteSpace(keyPrefix) ? key : $"{keyPrefix}/{key}";
    }

    public static string ToLocalizedText<TEnum>(this TEnum value, string keyPrefix = "Enums")
        where TEnum : Enum
    {
        return LocalizedTextUtility.GetLocalizedString(value.ToLocalizationKey(keyPrefix), value.ToString());
    }
}
