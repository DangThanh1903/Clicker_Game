using UnityEngine;
using UniRx;
using System;

[CreateAssetMenu(fileName = "ConditionalBuff", menuName = "Buff System/ConditionalBuff")]
public class ConditionalBuffSO : BuffSO
{
    public BuffConditionType conditionType;

    public override bool IsPermanent => true;

    public bool CheckCondition(StatsManagerBase stats)
    {
        return conditionType switch
        {
            BuffConditionType.HPBelow50Percent => stats.Get(StatType.CurrentHP) < stats.Get(StatType.HP) * 0.5f,
            BuffConditionType.ManaBelow50Percent => stats.Get(StatType.CurrentMana) < stats.Get(StatType.Mana) * 0.5f,
            BuffConditionType.ClickPerTick20 => stats.Get(StatType.ClickPerTick) > 20f,
            _ => true,
        };
    }

    public IObservable<bool> ObserveCondition(StatsManagerBase stats)
    {
        return Observable.EveryUpdate()
            .Select(_ => CheckCondition(stats))
            .DistinctUntilChanged();
    }
}

public enum BuffConditionType
{
    None,
    HPBelow50Percent,
    ManaBelow50Percent,
    ClickPerTick20
    // add more as needed
}
