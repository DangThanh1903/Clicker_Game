using System;
using UniRx;
using UnityEngine;

public class StepTracker : IDisposable
{
    public readonly string StepId;
    public readonly IntReactiveProperty Current = new(0);
    public readonly IntReactiveProperty Required = new(1);
    public readonly BoolReactiveProperty Completed = new(false);

    private readonly CompositeDisposable _cd = new();

    private readonly Func<bool> isUnlocked;
    private readonly string targetId;
    private readonly string questId;
    private readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

    public StepTracker(QuestStepDef def, int initialProgress, Func<bool> isUnlocked = null, string questId = null)
    {
        this.isUnlocked = isUnlocked;
        this.questId = questId;
        StepId = def.stepId;
        targetId = def.targetId ?? string.Empty;
        Required.Value = Mathf.Max(1, def.requiredAmount);
        Current.Value = Mathf.Clamp(initialProgress, 0, Required.Value);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[QuestDebug] Step init quest={this.questId} step={StepId} goal={def.goalType} target={targetId} progress={Current.Value}/{Required.Value}");
#endif

        Current
            .Select(cur => cur >= Required.Value)
            .DistinctUntilChanged()
            .Subscribe(done => Completed.Value = done)
            .AddTo(_cd);

        IObservable<int> incStream = BuildIncrementStream(def);
        if (incStream != null)
        {
            incStream
                .Where(_ => !Completed.Value)
                .Where(_ => this.isUnlocked == null || this.isUnlocked())
                .Subscribe(inc =>
                {
                    int before = Current.Value;
                    Current.Value = Mathf.Clamp(Current.Value + inc, 0, Required.Value);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[QuestDebug] Step progress quest={this.questId} step={StepId} target={targetId} +{inc}: {before} -> {Current.Value}/{Required.Value}");
#endif
                })
                .AddTo(_cd);
        }

        if (def.goalType == GoalType.ReachStat)
        {
            QuestSignals.OnStatChanged
                .Where(t => comparer.Equals(t.statKey, targetId))
                .Subscribe(t =>
                {
                    bool unlocked = this.isUnlocked == null || this.isUnlocked();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[QuestDebug] ReachStat signal quest={this.questId} step={StepId} target={targetId} value={t.value} completed={Completed.Value} unlocked={unlocked}");
#endif
                    if (Completed.Value || !unlocked)
                        return;

                    ApplyReachStatProgress(t.value);
                })
                .AddTo(_cd);

            TryApplyCurrentStatValue();
        }
    }

    private void TryApplyCurrentStatValue()
    {
        if (StatsManager.Ins == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[QuestDebug] ReachStat initial check skipped: StatsManager missing. quest={questId} step={StepId} target={targetId}");
#endif
            return;
        }
        if (!Enum.TryParse(targetId, true, out StatType statType))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[QuestDebug] ReachStat initial check skipped: targetId is not a StatType. quest={questId} step={StepId} target={targetId}");
#endif
            return;
        }

        float value = StatsManager.Ins.Get(statType);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[QuestDebug] ReachStat initial check quest={questId} step={StepId} target={targetId} value={value}");
#endif
        ApplyReachStatProgress(value);
    }

    private void ApplyReachStatProgress(double value)
    {
        int progress = Mathf.Clamp(Mathf.FloorToInt((float)value), 0, Required.Value);
        if (progress > Current.Value)
        {
            int before = Current.Value;
            Current.Value = progress;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[QuestDebug] ReachStat progress quest={questId} step={StepId} target={targetId}: {before} -> {Current.Value}/{Required.Value}");
#endif
        }
    }

    private IObservable<int> BuildIncrementStream(QuestStepDef def)
    {
        switch (def.goalType)
        {
            case GoalType.BreakBlock:
                return QuestSignals.OnBreakBlock
                    .Where(t => comparer.Equals(t.targetId, targetId))
                    .Select(t => t.amount);

            case GoalType.CollectItem:
                return QuestSignals.OnCollectItem
                    .Where(t => comparer.Equals(t.targetId, targetId))
                    .Select(t => t.amount);

            case GoalType.CraftItem:
                return QuestSignals.OnCraftItem
                    .Where(t => comparer.Equals(t.targetId, targetId))
                    .Select(t => t.amount);

            case GoalType.Custom:
                return null;

            default:
                return null;
        }
    }

    public void Dispose() => _cd.Dispose();
}
