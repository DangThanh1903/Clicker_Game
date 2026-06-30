using UnityEngine;

public class EnemyStatsManager : StatsManagerBase
{
    protected override void Awake()
    {
        base.Awake();
    }

    protected override bool ShouldPreserveAcrossRecalculate(StatType type)
    {
        return type == StatType.CurrentHP || base.ShouldPreserveAcrossRecalculate(type);
    }

    protected override void RecalculateResourceCaps()
    {
        base.RecalculateResourceCaps();

        if (HasStat(StatType.CurrentHP) && HasStat(StatType.HP))
        {
            if (Get(StatType.CurrentHP) > Get(StatType.HP))
                Set(StatType.CurrentHP, Get(StatType.HP));
        }
    }
}
