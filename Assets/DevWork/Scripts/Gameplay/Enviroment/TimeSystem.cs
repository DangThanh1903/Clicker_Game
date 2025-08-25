using UnityEngine;
using UniRx;
using System;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;

public enum TimeState { Day, Night, Any }

public class TimeSystem : MonoBehaviour
{
    public static TimeSystem Instance { get; private set; }
    [Header("Setting")]
    [SerializeField] private float maxSunIntensity = 2f;
    [SerializeField] private float maxMoonIntensity = 0.4f;
    [SerializeField] private float updateTick = 0.1f;
    public float dayDuration = 60f;
    public float nightDuration = 60f;

    [Header("Ref")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Light moonLight;
    public TimeState CurrentTimeState;
    public ReactiveProperty<float> CurrentTime { get; private set; } = new ReactiveProperty<float>();

    private void Awake()
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
        CurrentTime.Subscribe(time =>
        {
            if (CurrentTimeState == TimeState.Day && time >= dayDuration)
            {
                SwitchToNight();
            }
            else if (CurrentTimeState == TimeState.Night && time >= dayDuration + nightDuration)
            {
                SwitchToDay();
            }

            float lightRotation = time / (dayDuration + nightDuration) * 360f;
            sunLight.transform.localRotation = Quaternion.Euler(lightRotation, -30f, 0f);
            moonLight.transform.localRotation = Quaternion.Euler(lightRotation + 180f, -30f, 0f);
            sunLight.intensity = GetSunIntensity(time);
            moonLight.intensity = GetMoonIntensity(time);

        }).AddTo(this);

        Observable.Interval(TimeSpan.FromSeconds(updateTick))
            .Subscribe(_ => CurrentTime.Value += updateTick)
            .AddTo(this);
    }

    void SwitchToDay()
    {
        CurrentTimeState = TimeState.Day;
        CurrentTime.Value = 0;
        Debug.Log("Switched to Day");

        WeatherManager.Instance?.ClearSpecialWeatherAndTriggerNext();
    }

    void SwitchToNight()
    {
        CurrentTimeState = TimeState.Night;
        Debug.Log("Switched to Night");

        WeatherManager.Instance?.ClearSpecialWeatherAndTriggerNext();
    }


    float GetSunIntensity(float time)
    {
        if (time < 0 || time > dayDuration + nightDuration) return 0f;

        if (time <= dayDuration / 4f)
            return Mathf.Lerp(0f, maxSunIntensity, time / (dayDuration / 4f));
        else if (time <= dayDuration / 2f)
            return maxSunIntensity;
        else if (time <= dayDuration)
            return Mathf.Lerp(maxSunIntensity, 0f, (time - dayDuration / 2f) / (dayDuration / 2f));
        else
            return 0f;
    }
    float GetMoonIntensity(float time)
    {
        float nightStart = dayDuration;
        float nightEnd = dayDuration + nightDuration;

        if (time < nightStart || time > nightEnd)
            return 0f;

        float nightTime = time - nightStart;
        float halfNight = nightDuration / 2f;

        if (nightTime <= halfNight)
            return Mathf.Lerp(0f, maxMoonIntensity, nightTime / halfNight); // fade in
        else
            return Mathf.Lerp(maxMoonIntensity, 0f, (nightTime - halfNight) / halfNight); // fade out
    }

}
