using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;

public class QuestTracker : IDisposable
{
    public readonly string QuestId;
    public readonly ReactiveCollection<StepTracker> Steps = new();
    public readonly BoolReactiveProperty Completed = new(false);
    public readonly BoolReactiveProperty RewardClaimed = new(false);
    public readonly BoolReactiveProperty Unlocked = new(true);

    private readonly CompositeDisposable _cd = new();

    public QuestTracker(QuestDef def, QuestState saved)
    {
        QuestId = def.id;

        // Tạo StepTracker từ def + saved
        foreach (var sdef in def.steps)
        {
            var sstate = saved?.steps?.FirstOrDefault(s => s.stepId == sdef.stepId);
            var tracker = new StepTracker(sdef, sstate?.currentAmount ?? 0, () => Unlocked.Value, def.id);
            Steps.Add(tracker);

            // Đồng bộ ngược lại state saved (nếu dùng)
            tracker.Current
                .Skip(1)
                .Subscribe(_ => OnStepProgressChanged?.Invoke(this))
                .AddTo(_cd);
        }

        // Load claimed state (prevents re-claim after restart)
        RewardClaimed.Value = saved?.rewardClaimed ?? false;

        // Completed khi tất cả step completed
        var stepsCompleted = Steps.ObserveCountChanged().StartWith(Steps.Count)
            .Select(_ => Steps.All(s => s.Completed.Value))
            .Merge(Steps.Select(s => s.Completed.AsObservable()).Merge())
            .Select(_ => Steps.All(s => s.Completed.Value))
            .DistinctUntilChanged();

        stepsCompleted
            .CombineLatest(Unlocked, (done, unlocked) => done && unlocked)
            .DistinctUntilChanged()
            .Subscribe(done => Completed.Value = done)
            .AddTo(_cd);
    }

    public event Action<QuestTracker> OnStepProgressChanged;

    public void SetUnlocked(bool unlocked)
    {
        Unlocked.Value = unlocked;
    }

    public void Dispose()
    {
        foreach (var s in Steps) s.Dispose();
        _cd.Dispose();
    }
}

