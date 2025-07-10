using UniRx;
using UnityEngine;

public enum StatType
{
    Clicks,
    ClickPerTick
}


[System.Serializable]
public class ReactiveStat
{
    public StatType statType;
    public ReactiveProperty<float> value = new ReactiveProperty<float>(0);

    public void Add(float amount) => value.Value += amount;
    public void Set(float amount) => value.Value = amount;
    public float Get() => value.Value;
}

