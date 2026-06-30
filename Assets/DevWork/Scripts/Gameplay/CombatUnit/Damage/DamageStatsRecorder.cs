public static class DamageStatsRecorder
{
    public static void RecordDamage(float damage, float clickDelta)
    {
        StatsManager.Ins.Add(StatType.TotalDamageDealed, damage);
        StatsManager.Ins.Add(StatType.Clicks, clickDelta);
    }

    public static void RecordBlockBreak()
    {
        if (StatsManager.Ins == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogWarning("[QuestDebug] RecordBlockBreak skipped: StatsManager missing.");
#endif
            return;
        }

        float before = StatsManager.Ins.Get(StatType.TotalBlockBreaked);
        StatsManager.Ins.Add(StatType.TotalBlockBreaked, 1);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float after = StatsManager.Ins.Get(StatType.TotalBlockBreaked);
        UnityEngine.Debug.Log($"[QuestDebug] RecordBlockBreak TotalBlockBreaked {before} -> {after}");
#endif
    }
}
