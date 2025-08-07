using UniRx;
using System.Collections.Generic;
using UnityEngine;
using System;
using Sirenix.OdinInspector;
using System.Linq;

public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }
    [SerializeField] private List<NormalWeatherData> normalWeatherList;
    [SerializeField] private List<SpecialWeatherData> specialWeatherList;

    [ShowInInspector, ReadOnly]
    public ReactiveProperty<WeatherData> CurrentNormalWeather { get; private set; } = new();

    [ShowInInspector, ReadOnly]
    public ReactiveProperty<WeatherData> CurrentSpecialWeather { get; private set; } = new();

    private float normalRemainingTime;
    private IDisposable normalWeatherTimer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this);
    }
    void Start()
    {
        SetNormalWeather();
    }
    public void SetNormalWeather(float remainingTime = -1)
    {
        var nextWeather = GetRandomWeightedWeather(
            normalWeatherList,
            CurrentNormalWeather.Value as NormalWeatherData,
            TimeSystem.Instance.CurrentTimeState
        );

        if (CurrentNormalWeather.Value == nextWeather) return;

        CurrentNormalWeather.Value = nextWeather;
        var name = (nextWeather as NormalWeatherData)?.weatherName.ToString() ?? "Cleared";
        Debug.Log($"[Normal Weather] {name}");


        normalWeatherTimer?.Dispose();
        if (nextWeather != null)
        {
            if (nextWeather is NormalWeatherData normalWeather)
            {
                normalRemainingTime = remainingTime == -1 ? normalWeather.duration : remainingTime;
            }

            normalWeatherTimer = Observable.Interval(TimeSpan.FromSeconds(1))
                .Subscribe(_ =>
                {
                    normalRemainingTime -= 1f;
                    if (normalRemainingTime <= 0f)
                    {
                        ClearNormalWeatherAndTriggerNext();
                    }
                })
                .AddTo(this);
        }
    }

    private void ClearNormalWeatherAndTriggerNext()
    {
        CurrentNormalWeather.Value = null;
        SetNormalWeather();
    }

    public void SetSpecialWeather()
    {
        var nextWeather = GetRandomWeightedWeather(
            specialWeatherList,
            CurrentSpecialWeather.Value as SpecialWeatherData,
            TimeSystem.Instance.CurrentTimeState
        );

        if (CurrentSpecialWeather.Value == nextWeather) return;

        CurrentSpecialWeather.Value = nextWeather;
        var name = nextWeather?.weatherName.ToString() ?? "Cleared";
        Debug.Log($"[Special Weather] {name}");
    }
    public void ClearSpecialWeatherAndTriggerNext()
    {
        CurrentSpecialWeather.Value = null;
        SetSpecialWeather();
    }


    private T GetRandomWeightedWeather<T>(
    List<T> list,
    T exclude,
    TimeState currentTimeState
    ) where T : WeatherData
    {
        var validWeathers = list
            .Where(w =>
                w != exclude &&
                (!(w is SpecialWeatherData special) ||
                special.timeRequirement == TimeRequirement.Any ||
                (special.timeRequirement == TimeRequirement.DayOnly && currentTimeState == TimeState.Day) ||
                (special.timeRequirement == TimeRequirement.NightOnly && currentTimeState == TimeState.Night)))
            .ToList();

        if (validWeathers.Count == 0)
            return null;

        float totalWeight = validWeathers.Sum(w => w.weight);
        float rand = UnityEngine.Random.Range(0, totalWeight);
        float acc = 0f;

        foreach (var w in validWeathers)
        {
            acc += w.weight;
            if (rand <= acc)
                return w;
        }

        return validWeathers[0]; // fallback
    }
}
