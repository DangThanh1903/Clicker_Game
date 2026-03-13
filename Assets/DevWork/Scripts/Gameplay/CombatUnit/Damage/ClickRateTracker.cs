using System.Collections.Generic;
using UnityEngine;

public sealed class ClickRateTracker
{
    private const float DefaultWindowSeconds = 1f;
    private readonly List<float> hitTimestamps;
    private readonly float defaultWindowSeconds;

    public ClickRateTracker(int initialCapacity = 16, float defaultWindowSeconds = DefaultWindowSeconds)
    {
        hitTimestamps = new List<float>(Mathf.Max(1, initialCapacity));
        this.defaultWindowSeconds = Mathf.Max(0.05f, defaultWindowSeconds);
    }

    public void RecordHitNow()
    {
        float now = Time.unscaledTime;
        hitTimestamps.Add(now);
        Trim(now, defaultWindowSeconds);
    }

    public int GetRecentHitCount(float windowSeconds = 1f)
    {
        if (hitTimestamps.Count == 0)
            return 0;

        float now = Time.unscaledTime;
        float window = Mathf.Max(0.05f, windowSeconds);

        Trim(now, window);
        return hitTimestamps.Count;
    }

    public void Reset()
    {
        hitTimestamps.Clear();
    }

    private void Trim(float now, float windowSeconds)
    {
        if (windowSeconds <= 0f)
            windowSeconds = defaultWindowSeconds;

        int removeCount = 0;
        for (int i = 0; i < hitTimestamps.Count; i++)
        {
            if (now - hitTimestamps[i] <= windowSeconds)
                break;
            removeCount++;
        }

        if (removeCount > 0)
            hitTimestamps.RemoveRange(0, removeCount);
    }
}
