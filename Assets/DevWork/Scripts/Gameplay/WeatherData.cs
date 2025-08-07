using UnityEngine;

public enum WeatherType { Normal, Special }

public abstract  class WeatherData : ScriptableObject
{
    public abstract WeatherType Type { get; }
    [Header("Setting")]
    public GameObject effectPrefab;
    public float weight;

    [Header("Lighting")]
    public Color sunLightColor = Color.white;
    public Color moonLightColor = Color.white;
    public float temperature = 6500f;
}
