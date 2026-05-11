using UnityEngine;
using UniRx;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ConditionalBuff", menuName = "Buff System/ConditionalBuff")]
public class ConditionalBuffSO : BuffSO
{
    private static readonly HashSet<int> WarnedLegacyConditionValues = new HashSet<int>();
    public BuffConditionType conditionType;
    [SerializeField, Min(0.02f)] private float checkIntervalSeconds = 0.1f;

    public override bool IsPermanent => true;

    public bool CheckCondition(StatsManagerBase stats)
    {
        if (stats == null)
            return false;

        switch (conditionType)
        {
            case BuffConditionType.HPBelow50Percent:
                return stats.Get(StatType.CurrentHP) < stats.Get(StatType.HP) * 0.5f;
            case BuffConditionType.ManaBelow50Percent:
                return stats.Get(StatType.CurrentMana) < stats.Get(StatType.Mana) * 0.5f;
            case BuffConditionType.ClickPerTick20:
                return stats.Get(StatType.ClickPerTick) > 20f;
            case BuffConditionType.DayTime:
                return TimeSystem.Instance != null && TimeSystem.Instance.CurrentTimeState.Value == TimeState.Day;
            case BuffConditionType.NightTime:
                return TimeSystem.Instance != null && TimeSystem.Instance.CurrentTimeState.Value == TimeState.Night;
            // Pre-arm before the hit so the 5th click can benefit from this conditional buff.
            case BuffConditionType.Every5Click:
                return ((int)stats.Get(StatType.Clicks) + 1) % 5 == 0;
            case BuffConditionType.Every8Click:
                return ((int)stats.Get(StatType.Clicks) + 1) % 8 == 0;
            case BuffConditionType.IsInPlain:
                return DataSaver.Ins != null && DataSaver.Ins.currentLocation == BlockSpawnLocation.Plain;
            case BuffConditionType.IsBossOutOfCondition:
                return BlockManager.Ins != null && BlockManager.Ins.IsBossOutOfCondition();
            default:
                LogLegacyConditionFallback(conditionType);
                return false;
        }
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

    private static void LogLegacyConditionFallback(BuffConditionType value)
    {
        int raw = (int)value;
        if (!WarnedLegacyConditionValues.Add(raw))
            return;

        DevLog.Log($"[Buff] Ignore legacy/unknown condition value: {raw}");
    }
}

public enum BuffConditionType
{
    None = 0,
    HPBelow50Percent = 1,
    ManaBelow50Percent = 2,
    ClickPerTick20 = 3,
    DayTime = 4,
    NightTime = 5,
    Every5Click = 6,
    Every8Click = 7,
    // Area
    IsInPlain = 10,
    IsBossOutOfCondition = 11,
}
