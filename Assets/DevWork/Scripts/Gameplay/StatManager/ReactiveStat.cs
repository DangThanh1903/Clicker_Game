using UniRx;
using UnityEngine;

public enum StatType
{
    Clicks,
    ClickPerTick,
    Diamond,
    NormalPower,
    HoldPower,
    IdlePower,
    HP,
    CurrentHP,
    HpRegen,
    Mana,
    CurrentMana,
    ManaRegen,
    Def,
    CritChance,
    CritDmg,
    Pen,
    Lucky,


    // For record
    TotalBlockBreaked,
    TotalDamageDealed,
    TotalTimePlayed,
    HoldedTime
}


[System.Serializable]
public class ReactiveStat
{
    public StatType statType;
    public ReactiveProperty<float> value = new ReactiveProperty<float>(0);

    public void Add(float amount) => value.Value += amount;
    public void Sub(float amount) => value.Value -= amount;
    public void AddPercent(float amount) => value.Value += (int)(value.Value * amount / 100);
    public void SubPercent(float amount) => value.Value -= (int)(value.Value * amount / 100);
    public void Set(float amount) => value.Value = amount;
    public float Get() => value.Value;
}

