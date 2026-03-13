public static class DamageStatsRecorder
{
    public static void RecordDamage(float damage, float clickDelta)
    {
        StatsManager.Ins.Add(StatType.TotalDamageDealed, damage);
        StatsManager.Ins.Add(StatType.Clicks, clickDelta);
    }
}
