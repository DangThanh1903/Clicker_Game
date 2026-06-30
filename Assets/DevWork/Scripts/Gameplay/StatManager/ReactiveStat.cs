using UniRx;
using UnityEngine;

public enum StatType
{
    Clicks = 0,
    ClickPerTick = 1,
    Diamond = 2,
    NormalPower = 3,
    HoldPower = 4,
    IdlePower = 5,
    HP = 6,
    CurrentHP = 7,
    HpRegen = 8,
    Mana = 9,
    CurrentMana = 10,
    ManaRegen = 11,
    Stamina = 12,
    CurrentStamina = 13,
    StaminaCostPerClick = 14,
    StaminaRegenTick = 15,
    HaveStaminaDamageMul = 16,
    LowStaminaDamageMultiplier = 17,
    CritChance = 19,
    CritDmg = 20,
    Pen = 21,
    Lucky = 22,


    // For record
    TotalBlockBreaked = 23,
    TotalDamageDealed = 24,
    TotalTimePlayed = 25,
    HoldedTime = 26,
    SummonAttackSpeed = 27,
    HighManaMul = 28,
    MiddleManaMul = 29,
    IdleStackDamagePerStack = 30,
    IdleMaxStack = 31,
    IdleStackResetTime = 32,
    IgnoreStaminaEffect = 33,
    HighManaThreshold = 34,
    MiddleManaThreshold = 35
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

