using UnityEngine;
public enum TimeRequirement { Any, DayOnly, NightOnly }
public enum SpecialWeatherName
{
    Any,
    Normal,
    BloodMoon,
    Eclipe
}

[CreateAssetMenu(menuName = "Weather/SpecialWeatherData")]
public class SpecialWeatherData : WeatherData
{
    public override WeatherType Type => WeatherType.Special;
    [Header("Special Weather Setting")]
    public SpecialWeatherName weatherName;
    public TimeRequirement timeRequirement = TimeRequirement.Any;
}
