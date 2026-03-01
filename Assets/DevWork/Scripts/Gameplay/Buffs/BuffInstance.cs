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

    public float Duration => buffData.duration;
    public float RemainingTime { get; private set; }
    public bool HasDuration => !buffData.IsPermanent && buffData.duration > 0;

    private Action<BuffInstance> onExpireCallback;
    private bool disposed = false;

    public BuffInstance(
        BuffSO buff,
        StatsManagerBase statsManager,
        Action<BuffInstance> onExpire = null)
    {
        buffData = buff;
        stats    = statsManager;
        this.onExpireCallback = onExpire;

        if (buff is ConditionalBuffSO conditional)
        {
            // Only observe condition; activation controlled by observable
            conditionCheck = conditional.ObserveCondition(stats)
                .Subscribe(active =>
                {
                    if (active && !IsActive) Activate();
                    else if (!active && IsActive) Deactivate();
                });
        }
        else
        {
            // normal buff: we will Activate() from BuffManager AFTER adding to list
        }

        // Start duration ticking if it has a duration
        if (HasDuration)
        {
            StartDurationTimer();
        }
    }

    private void StartDurationTimer()
    {
        RemainingTime = Duration;

        durationTimer?.Dispose();
        durationTimer = Observable
            .Interval(TimeSpan.FromSeconds(0.1f))
            .Subscribe(_ =>
            {
                RemainingTime -= 0.1f;
                if (RemainingTime <= 0f)
                {
                    Dispose();
                }
            });
    }

    // -------------------------
    // Activate / deactivate
    // -------------------------

    public void Activate()
    {
        if (IsActive) return;
        IsActive = true;
        stats.RecalculateAllStats();
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        stats.RecalculateAllStats();
    }

    // -------------------------
    // Removing / expiring
    // -------------------------

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        bool wasActive = IsActive;
        IsActive = false;

        durationTimer?.Dispose();
        durationTimer = null;
        conditionCheck?.Dispose();
        conditionCheck = null;

        onExpireCallback?.Invoke(this);

        if (wasActive && stats != null)
            stats.RecalculateAllStats();
    }
    // -------------------------
    // Helpers
    // -------------------------

    // Extend duration for non-stackable buffs
    public void ExtendDuration(float extra)
    {
        if (!HasDuration) return;
        RemainingTime += extra;
    }

    // For stackable buffs: add stack and reset timer
    public void AddStackAndResetDuration()
    {
        if (!buffData.isStackable) return;

        if (StackCount < buffData.maxStack)
            StackCount++;
        
        if (HasDuration)
        {
            StartDurationTimer(); 
        }

        stats.RecalculateAllStats();
    }

}
