using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum NormalWeatherName
{
    Any,
    Normal,
    Rain,
    Foggy
}

[CreateAssetMenu(menuName = "Weather/NormalWeatherData")]
public class NormalWeatherData : WeatherData
{
    public override WeatherType Type => WeatherType.Normal;
    [Header("Normal Weather Setting")]
    public NormalWeatherName weatherName;
    public float duration;
}
