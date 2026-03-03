using UnityEngine;
using UniRx;
using System;

[CreateAssetMenu(fileName = "ConditionalBuff", menuName = "Buff System/ConditionalBuff")]
public class ConditionalBuffSO : BuffSO
{
    public BuffConditionType conditionType;
    [SerializeField, Min(0.02f)] private float checkIntervalSeconds = 0.1f;

    public override bool IsPermanent => true;

    public bool CheckCondition(StatsManagerBase stats)
    {
        if (stats == null)
            return false;

        return conditionType switch
        {
            BuffConditionType.HPBelow50Percent => stats.Get(StatType.CurrentHP) < stats.Get(StatType.HP) * 0.5f,
            BuffConditionType.ManaBelow50Percent => stats.Get(StatType.CurrentMana) < stats.Get(StatType.Mana) * 0.5f,
            BuffConditionType.ClickPerTick20 => stats.Get(StatType.ClickPerTick) > 20f,
            BuffConditionType.DayTime => TimeSystem.Instance != null && TimeSystem.Instance.CurrentTimeState.Value == TimeState.Day,
            BuffConditionType.NightTime => TimeSystem.Instance != null && TimeSystem.Instance.CurrentTimeState.Value == TimeState.Night,
            // Pre-arm before the hit so the 5th click can benefit from this conditional buff.
            BuffConditionType.Every5Click => ((int)stats.Get(StatType.Clicks) + 1) % 5 == 0,
            BuffConditionType.Every8Click => ((int)stats.Get(StatType.Clicks) + 1) % 8 == 0,
            BuffConditionType.Holded3Sec => (int)stats.Get(StatType.HoldedTime) >= 3,
            BuffConditionType.Holded6Sec => (int)stats.Get(StatType.HoldedTime) >= 6,
            BuffConditionType.IsInPlain => DataSaver.Ins != null && DataSaver.Ins.currentLocation == BlockSpawnLocation.Plain,
            BuffConditionType.IsBossOutOfCondition => BlockManager.Ins != null && BlockManager.Ins.IsBossOutOfCondition(),
            _ => true,
        };
    }

    public IObservable<bool> ObserveCondition(StatsManagerBase stats)
    {
        if (stats == null)
            return Observable.Return(false);

        if (conditionType == BuffConditionType.Every5Click ||
            conditionType == BuffConditionType.Every8Click)
        {
            return stats.GetReactive(StatType.Clicks)
                .Select(_ => CheckCondition(stats))
                .StartWith(CheckCondition(stats))
                .DistinctUntilChanged();
        }

        return Observable.Interval(TimeSpan.FromSeconds(Mathf.Max(0.02f, checkIntervalSeconds)))
            .StartWith(0L)
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
    Every5Click,
    Every8Click,
    Holded3Sec,
    Holded6Sec,
    // Area
    IsInPlain,
    IsBossOutOfCondition,
}
