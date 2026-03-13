using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ClickRateTracker
{
    private const long DefaultWindowMs = 1000;
    private readonly List<long> hitTimestamps;
    private readonly long defaultWindowMs;

    public ClickRateTracker(int initialCapacity = 16, long defaultWindowMs = DefaultWindowMs)
    {
        hitTimestamps = new List<long>(Mathf.Max(1, initialCapacity));
        this.defaultWindowMs = Mathf.Max(1, (int)defaultWindowMs);
    }

    public void RecordHitNow()
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        hitTimestamps.Add(nowMs);
        Trim(nowMs, defaultWindowMs);
    }

    public int GetRecentHitCount(float windowSeconds = 1f)
    {
        if (hitTimestamps.Count == 0)
            return 0;

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long windowMs = Math.Max(
            50L,
            (long)Mathf.RoundToInt(Mathf.Max(0.05f, windowSeconds) * 1000f));

        Trim(nowMs, windowMs);
        return hitTimestamps.Count;
    }

    public void Reset()
    {
        hitTimestamps.Clear();
    }

    private void Trim(long nowMs, long windowMs)
    {
        if (windowMs <= 0)
            windowMs = defaultWindowMs;

        int removeCount = 0;
        for (int i = 0; i < hitTimestamps.Count; i++)
        {
            if (nowMs - hitTimestamps[i] <= windowMs)
                break;
            removeCount++;
        }

        if (removeCount > 0)
            hitTimestamps.RemoveRange(0, removeCount);
    }
}
