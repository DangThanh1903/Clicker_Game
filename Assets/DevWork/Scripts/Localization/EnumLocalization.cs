using System;
using UnityEngine.Localization;

public static class EnumLocalization
{
    /// <summary>
    /// Convert any enum value to a LocalizedString, 
    /// using pattern: {tableName}/{enumType}_{enumValue}
    /// Example: WeatherType.Rain -> "Enums", key "weather_rain"
    /// </summary>
    public static LocalizedString ToLocalized<TEnum>(this TEnum value, string tableName = "Enums")
        where TEnum : Enum
    {
        string enumType = typeof(TEnum).Name.ToLower();  // "weathertype" -> maybe shorten if you want
        string key = $"{enumType}_{value.ToString().ToLower()}";
        return new LocalizedString(tableName, key);
    }
}
