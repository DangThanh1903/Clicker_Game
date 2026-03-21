using System.Collections.Generic;
using UnityEngine;
using UniRx;

public class Biome : MonoBehaviour
{
    [SerializeField] private GameObject[] lightSources;

    [Header("Light Toggle")]
    [SerializeField] private bool toggleByIntensity = true;
    [SerializeField] private bool toggleSourceGameObject = false;

    private readonly List<LightEntry> lightEntries = new List<LightEntry>(16);
    private readonly List<GameObject> sourcesWithoutLight = new List<GameObject>(8);
    private readonly HashSet<Light> seenLights = new HashSet<Light>();
    private bool cacheBuilt;

    private void Awake()
    {
        BuildLightCache();
    }

    private void Start()
    {
        BuildLightCache();

        if (TimeSystem.Instance == null || WeatherManager.Instance == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[Biome] Missing TimeSystem or WeatherManager. Light toggle subscription skipped.", this);
#endif
            return;
        }

        Observable.CombineLatest(
                TimeSystem.Instance.CurrentTimeState,
                WeatherManager.Instance.CurrentNormalWeather,
                WeatherManager.Instance.CurrentSpecialWeather,
                (time, normal, special) => ShouldLightsBeOn(time, normal, special))
            .Subscribe(SetLightObjects)
            .AddTo(this);
    }

    private bool ShouldLightsBeOn(TimeState time, WeatherData normal, WeatherData special)
    {
        if (IsNormal(normal, NormalWeatherName.Rain))
            return false;

        if (IsNormal(normal, NormalWeatherName.Foggy))
            return true;

        if (IsSpecial(special, SpecialWeatherName.Eclipe))
            return true;

        return time == TimeState.Night;
    }

    private void SetLightObjects(bool on)
    {
        if (!cacheBuilt)
            BuildLightCache();

        if (toggleByIntensity)
        {
            for (int i = 0; i < lightEntries.Count; i++)
            {
                var entry = lightEntries[i];
                if (entry.light != null)
                    entry.light.intensity = on ? entry.onIntensity : 0f;
            }
        }
        else
        {
            for (int i = 0; i < lightEntries.Count; i++)
            {
                var entry = lightEntries[i];
                if (entry.light != null)
                    entry.light.enabled = on;
            }
        }

        if (toggleSourceGameObject)
        {
            if (lightSources == null)
                return;

            for (int i = 0; i < lightSources.Length; i++)
            {
                var source = lightSources[i];
                if (source != null)
                    source.SetActive(on);
            }
            return;
        }

        for (int i = 0; i < sourcesWithoutLight.Count; i++)
        {
            var source = sourcesWithoutLight[i];
            if (source != null)
                source.SetActive(on);
        }
    }

    private void BuildLightCache()
    {
        if (cacheBuilt)
            return;

        cacheBuilt = true;
        lightEntries.Clear();
        sourcesWithoutLight.Clear();
        seenLights.Clear();

        if (lightSources == null)
            return;

        for (int i = 0; i < lightSources.Length; i++)
        {
            var source = lightSources[i];
            if (source == null)
                continue;

            var lights = source.GetComponentsInChildren<Light>(true);
            if (lights == null || lights.Length == 0)
            {
                sourcesWithoutLight.Add(source);
                continue;
            }

            for (int j = 0; j < lights.Length; j++)
            {
                var light = lights[j];
                if (light == null || !seenLights.Add(light))
                    continue;

                lightEntries.Add(new LightEntry
                {
                    light = light,
                    onIntensity = Mathf.Max(0f, light.intensity)
                });
            }
        }
    }

    private static bool IsNormal(WeatherData data, NormalWeatherName name)
    {
        var normal = data as NormalWeatherData;
        return normal != null && normal.weatherName == name;
    }

    private static bool IsSpecial(WeatherData data, SpecialWeatherName name)
    {
        var special = data as SpecialWeatherData;
        return special != null && special.weatherName == name;
    }

    private void OnValidate()
    {
        if (toggleByIntensity)
            toggleSourceGameObject = false;

        cacheBuilt = false;
    }

    private struct LightEntry
    {
        public Light light;
        public float onIntensity;
    }
}
