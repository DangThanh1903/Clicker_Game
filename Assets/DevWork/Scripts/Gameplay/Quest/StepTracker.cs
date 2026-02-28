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
    private readonly StringComparer comparer = StringComparer.OrdinalIgnoreCase;

    public StepTracker(QuestStepDef def, int initialProgress, Func<bool> isUnlocked = null)
    {
        this.isUnlocked = isUnlocked;
        StepId = def.stepId;
        targetId = def.targetId ?? string.Empty;
        Required.Value = Mathf.Max(1, def.requiredAmount);
        Current.Value  = Mathf.Clamp(initialProgress, 0, Required.Value);

        // completed khi Current >= Required (distinct để không spam)
        Current
            .Select(cur => cur >= Required.Value)
            .DistinctUntilChanged()
            .Subscribe(done => Completed.Value = done)
            .AddTo(_cd);

        // Ghép stream theo goal
        IObservable<int> incStream = BuildIncrementStream(def);
        if (incStream != null)
        {
            incStream
                .Where(_ => !Completed.Value) // dừng cộng khi đã xong
                .Where(_ => this.isUnlocked == null || this.isUnlocked())
                .Subscribe(inc =>
                {
                    Current.Value = Mathf.Clamp(Current.Value + inc, 0, Required.Value);
                })
                .AddTo(_cd);
        }

        // Với ReachStat kiểu “>= threshold” – không cộng dồn mà check trực tiếp:
        if (def.goalType == GoalType.ReachStat)
        {
            QuestSignals.OnStatChanged
                .Where(t => comparer.Equals(t.statKey, targetId))
                .Select(t => t.value >= def.requiredAmount) // dùng requiredAmount làm ngưỡng
                .DistinctUntilChanged()
                .Where(ok => ok && !Completed.Value)
                .Where(_ => this.isUnlocked == null || this.isUnlocked())
                .Subscribe(_ =>
                {
                    Current.Value = Required.Value;
                })
                .AddTo(_cd);
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
                // Bạn có thể map Custom sang một Subject khác nếu cần
                return null;

            default:
                return null;
        }
    }

    public void Dispose() => _cd.Dispose();
}

