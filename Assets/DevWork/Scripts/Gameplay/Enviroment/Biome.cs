using UnityEngine;
using UniRx;
using System;

public class Biome : MonoBehaviour
{
    [SerializeField] private GameObject[] lightSource;

    private void Start()
    {
        var timeObs    = TimeSystem.Instance.CurrentTimeState;
        var normalObs  = WeatherManager.Instance.CurrentNormalWeather;
        var specialObs = WeatherManager.Instance.CurrentSpecialWeather;

        Observable.CombineLatest(timeObs, normalObs, specialObs,
            (time, normal, special) => new { time, normal, special })
            .Subscribe(s =>
            {
                bool rain = IsNormal(s.normal, NormalWeatherName.Rain);
                bool foggy  = IsNormal(s.normal, NormalWeatherName.Foggy);
                bool eclipse = IsSpecial(s.special, SpecialWeatherName.Eclipe);

                bool lightsOn;

                if (rain)
                {
                    lightsOn = false;
                }
                else if (foggy)
                {
                    lightsOn = true;
                }
                else if (eclipse)
                {
                    lightsOn = true;
                }
                else
                {
                    // fallback to time of day rule
                    lightsOn = s.time == TimeState.Night;
                }

                SetLightObjects(lightsOn);
            })
            .AddTo(this);
    }

    private void SetLightObjects(bool on)
    {
        if (lightSource == null) return;
        foreach (var go in lightSource)
        {
            if (go) go.SetActive(on);
        }
    }

    private bool IsNormal(WeatherData data, NormalWeatherName name)
    {
        var n = data as NormalWeatherData;
        return n != null && n.weatherName == name;
    }

    private bool IsSpecial(WeatherData data, SpecialWeatherName name)
    {
        var s = data as SpecialWeatherData;
        return s != null && s.weatherName == name;
    }
}
