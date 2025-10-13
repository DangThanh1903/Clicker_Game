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
            BuffConditionType.DayTime => TimeSystem.Instance.CurrentTimeState.Value == TimeState.Day,
            BuffConditionType.NightTime => TimeSystem.Instance.CurrentTimeState.Value == TimeState.Night,
            BuffConditionType.Every4Click => (int)stats.Get(StatType.Clicks) % 4 == 0,
            BuffConditionType.Every8Click => (int)stats.Get(StatType.Clicks) % 8 == 0,
            BuffConditionType.Holded3Sec => (int)stats.Get(StatType.HoldedTime) >= 3,
            BuffConditionType.Holded6Sec => (int)stats.Get(StatType.HoldedTime) >= 6,
            BuffConditionType.IsInPlain => DataSaver.Ins.currentLocation == BlockSpawnLocation.Plain,
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
    ClickPerTick20,
    DayTime,
    NightTime,
    Every4Click,
    Every8Click,
    Holded3Sec,
    Holded6Sec,
    // Area
    IsInPlain
}
