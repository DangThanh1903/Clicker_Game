using UnityEngine;

public sealed class ClickPerTickService
{
    private const float UpdateIntervalSeconds = 1f;

    private readonly ClickRateTracker clickRateTracker = new ClickRateTracker();
    private float nextWriteTime = float.NegativeInfinity;
    private bool hasInitializedWriteTime;

    public void RecordHit()
    {
        clickRateTracker.RecordHitNow();
    }

    public int GetRecentHitCount(float windowSeconds = 1f)
    {
        return clickRateTracker.GetRecentHitCount(windowSeconds);
    }

    public void Tick(float nowTime)
    {
        if (!hasInitializedWriteTime)
        {
            nextWriteTime = nowTime + UpdateIntervalSeconds;
            hasInitializedWriteTime = true;
            return;
        }

        if (nowTime < nextWriteTime)
            return;

        if (StatsManager.Ins != null)
            StatsManager.Ins.Set(StatType.ClickPerTick, clickRateTracker.GetRecentHitCount());

        while (nowTime >= nextWriteTime)
            nextWriteTime += UpdateIntervalSeconds;
    }

    public void ResetRuntime()
    {
        clickRateTracker.Reset();
        nextWriteTime = float.NegativeInfinity;
        hasInitializedWriteTime = false;

        if (StatsManager.Ins != null)
            StatsManager.Ins.Set(StatType.ClickPerTick, 0f);
    }
}
