using System.Collections.Generic;
using UnityEngine;
using UniRx;

public class StatsManager : MonoBehaviour
{
    public static StatsManager Ins { get; private set; }

    [SerializeField]
    private List<ReactiveStat> statsList;

    private Dictionary<StatType, ReactiveStat> stats;

    void Awake()
    {
        if (Ins == null) Ins = this;
        else Destroy(gameObject);

        stats = new Dictionary<StatType, ReactiveStat>();
        foreach (var stat in statsList)
        {
            stats[stat.statType] = stat;
        }
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
}
