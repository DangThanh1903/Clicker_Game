using System.Collections.Generic;
using UnityEngine;
using UniRx;
using System;
using System.Linq;

public abstract class StatsManagerBase : MonoBehaviour
{
    [SerializeField] protected BaseStat baseStat;

    protected Dictionary<StatType, ReactiveStat> stats;
    protected Dictionary<StatType, ReactiveStat> baseStatsDict;


    protected virtual void Awake()
    {
        stats = new Dictionary<StatType, ReactiveStat>();
        foreach (var stat in baseStat.statsList)
        {
            stats[stat.statType] = stat;
        }

        baseStatsDict = new Dictionary<StatType, ReactiveStat>();
        foreach (var baseStat in baseStat.baseStats)
        {
            baseStatsDict[baseStat.statType] = baseStat;
        }

        ClearAll();
    }

    public ReactiveProperty<float> GetReactive(StatType type)
    {
        return stats[type].value;
    }

    public float Get(StatType type)
    {
        return stats[type].Get();
    }

    public void Set(StatType type, float value)
    {
        stats[type].Set(value);
    }

    public void Add(StatType type, float amount)
    {
        stats[type].Add(amount);
    }

    public void Sub(StatType type, float amount)
    {
        stats[type].Sub(amount);
    }

    public void ClearAll()
    {
        foreach (var stat in stats.Values)
        {
            if (baseStatsDict.TryGetValue(stat.statType, out var baseStat))
            {
                stat.Set(baseStat.Get());
            }
        }
    }
    public void SetBaseStat(BaseStat insertBaseStat)
    {
        baseStat = insertBaseStat;
    }
    public List<ConditionalBuffSO> GetAllStarterBuff()
    {
        return baseStat.starterBuff;
    }
}