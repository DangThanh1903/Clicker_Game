using System;
using UnityEngine;

public partial class ClickableObject
{
    public void HandleClick()
    {
        float power = DamageInputPowerResolver.GetClickPower();
        TakeDamage(power, "click", countAsHit: true);
    }

    public void SetIdleAnimationSuppressed(bool suppressed)
    {
        animCtrl?.SetIdleSuppressed(suppressed);
    }

    public void ApplyDamageInput(DamageInputKind inputKind)
    {
        switch (inputKind)
        {
            case DamageInputKind.Click:
                HandleClick();
                return;
            case DamageInputKind.Hold:
                HandleHold();
                return;
            case DamageInputKind.Idle:
                HandleIdle();
                return;
            default:
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[ClickableObject] Unknown damage input kind: {inputKind}", this);
#endif
                return;
        }
    }

    public void HandleHold()
    {
        float dt = DamageInputPowerResolver.GetInputDeltaTime();
        StatsManager.Ins.Add(StatType.HoldedTime, dt);
        if (DamageTickAccumulator.TryConsumeTick(ref accumulatedHoldTime, dt, timeHoldReset))
        {
            float power = DamageInputPowerResolver.GetHoldTickPower(timeHoldReset);
            TakeDamage(power, "hold", timeHoldReset, countAsHit: true);
        }
    }

    public void HandleIdle()
    {
        float power = DamageInputPowerResolver.GetIdleTickPower(timeIdleReset);
        TakeDamage(power, "idle", timeIdleReset, countAsHit: false);
        CombatFeedbackRuntime.NotifyIdleDamageDealt(power, transform.position);
    }

    private void TakeDamage(float power, string source, float timeReset = 1f, bool countAsHit = true)
    {
        if (power <= 0f)
            return;

        if (MaxHealth > 0f)
            lastDamageRatioNormalized = Mathf.Clamp01(power / MaxHealth);
        else
            lastDamageRatioNormalized = 0f;
        lastDamageFrame = Time.frameCount;

        if (countAsHit)
            CombatFeedbackRuntime.NotifyDamageHit();

        CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - power);

        DamageStatsRecorder.RecordDamage(power, 1f * timeReset);

        AnalyticsManager.Ins?.TrackBlockClick(blockName, GetLocationString(), power, source);

        Toaster.Show($"-{power:F1}", null, 0.2f, onClickPos);

        if ((string.Equals(source, "click", StringComparison.Ordinal) ||
             string.Equals(source, "hold", StringComparison.Ordinal)) &&
            hasLastClickWorldPoint)
        {
            VFXManager.Ins?.PlayBlockClickVfx(lastClickWorldPoint, currentOutlineColor);
        }

        if (string.Equals(source, "hold", StringComparison.Ordinal))
            animCtrl?.PlayHold();
        else
            animCtrl?.PlayClick();
    }

    public int GetRecentHitCount(float windowSeconds = 1f)
    {
        return CombatFeedbackRuntime.GetRecentHitCount(windowSeconds);
    }
}
