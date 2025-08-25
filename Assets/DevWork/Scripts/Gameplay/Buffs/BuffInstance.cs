using System;
using UniRx;
using UnityEngine;

public class BuffInstance : IDisposable
{
    public BuffSO buffData;
    private StatsManagerBase stats;
    private IDisposable durationTimer;
    private IDisposable conditionCheck;
    public bool IsActive { get; private set; } = false;
    public int StackCount { get; private set; } = 1;
    public Item SourceItem;
    private Action<BuffInstance> onExpireCallback;
    private bool disposed = false;

    public BuffInstance(BuffSO buff, StatsManagerBase statsManager, Action<BuffInstance> onExpire = null)
    {
        buffData = buff;
        stats = statsManager;
        onExpireCallback = onExpire;

        if (buff is ConditionalBuffSO conditional)
        {
            // Reactive condition for item conditional buff
            conditionCheck = conditional.ObserveCondition(stats)
                .Subscribe(active =>
                {
                    if (active && !IsActive) ApplyEffect();
                    else if (!active && IsActive) RemoveEffect();
                });

            if (conditional.CheckCondition(stats)) ApplyEffect();
        }
        else
        {
            // Consumable buff
            ApplyEffect();
            if (!buff.IsPermanent && buff.duration > 0)
                durationTimer = Observable.Timer(TimeSpan.FromSeconds(buff.duration))
                    .Subscribe(_ => Dispose());
        }
    }

    public void ApplyEffect()
    {
        if (IsActive) return;
        stats.Add(buffData.statType, buffData.amount * (buffData.isStackable ? StackCount : 1));
        IsActive = true;
    }

    public void RemoveEffect()
    {
        if (!IsActive) return;
        stats.Sub(buffData.statType, buffData.amount * (buffData.isStackable ? StackCount : 1));
        IsActive = false;
    }

    public void IncrementStack()
    {
        if (!buffData.isStackable) return;
        RemoveEffect();
        StackCount++;
        ApplyEffect();
        if (durationTimer != null)
            durationTimer.Dispose();
    }

    public void Deactivate()  // For unequip
    {
        conditionCheck?.Dispose();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        RemoveEffect();
        durationTimer?.Dispose();
        conditionCheck?.Dispose();
        onExpireCallback?.Invoke(this);
    }
}
