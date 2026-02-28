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
    public event Action OnStatsRecalculated;

    public IReadOnlyList<BuffInstance> ActiveBuffs    => buffManager?.GetActiveBuffs();
    public IReadOnlyList<BuffInstance> ConditionBuffs => buffManager?.GetConditionBuffs();

    [SerializeField] protected BuffManager buffManager;

    private CompositeDisposable _cd = new CompositeDisposable();

    protected virtual void Awake()
    {
        BuildStatsFromBase(overwriteValues: true);

        if (!buffManager)
            buffManager = GetComponent<BuffManager>();

        // Starter buffs: apply ONCE at startup
        ApplyStarterBuffsOnce();

        // Build final stats once (base + gear (override) + buffs)
        RecalculateAllStats();
    }

    protected virtual void OnDestroy()
    {
        _cd.Dispose();
    }

    protected void EnableQuestStatSignals()
    {
        foreach (var kv in stats)
        {
            var statType = kv.Key;
            var reactive = kv.Value.value;

            reactive
                .DistinctUntilChanged()
                .Subscribe(newValue =>
                {
                    QuestSignals.StatChanged(statType.ToString(), newValue);
                })
                .AddTo(_cd);
        }
    }

    public ReactiveProperty<float> GetReactive(StatType type) => GetOrCreateStat(type).value;
    public float Get(StatType type) => GetOrCreateStat(type).Get();
    public void Set(StatType type, float value) => GetOrCreateStat(type).Set(value);
    public void Add(StatType type, float amount) => GetOrCreateStat(type).Add(amount);
    public void Sub(StatType type, float amount) => GetOrCreateStat(type).Sub(amount);

    public void ClearAll()
    {
        foreach (var stat in stats.Values)
        {
            if (baseStatsDict.TryGetValue(stat.statType, out var bs))
                stat.Set(bs.Get());
        }
    }

    public void SetBaseStat(BaseStat insertBaseStat)
    {
        baseStat = insertBaseStat;
        BuildStatsFromBase(overwriteValues: true);
        ApplyStarterBuffsOnce();
        RecalculateAllStats();
    }

    // Starter buffs (permanent passives defined on BaseStat)
    protected virtual void ApplyStarterBuffsOnce()
    {
        if (buffManager == null || baseStat == null || baseStat.starterBuff == null)
            return;

        foreach (var starterBuff in baseStat.starterBuff)
        {
            if (starterBuff == null) continue;

            bool alreadyHas = buffManager
                .GetAllBuffs()
                .Any(b => b.buffData == starterBuff && b.SourceItem == null);

            if (alreadyHas) continue;

            buffManager.ApplyBuff(starterBuff);
        }
    }

    protected void RaiseStatsRecalculated()
    {
        OnStatsRecalculated?.Invoke();
    }

    public void ForceNotifyStatsChanged()
    {
        RaiseStatsRecalculated();
    }

    // Main rebuild entry point
    public virtual void RecalculateAllStats()
    {
        ClearAll();

        ApplyBuffs();

        ReCalculateHPAndMP();

        RaiseStatsRecalculated();
    }

    // Shared buff application for everyone
    protected virtual void ApplyBuffs()
    {
            if (buffManager == null) return;

            foreach (var buffInst in buffManager.GetAllBuffs())
        {
            if (!buffInst.IsActive) continue;

            var data  = buffInst.buffData;
            int stacks = buffInst.StackCount;

            foreach (var mod in data.GetEffectiveModifiers(stacks))
            {
                Add(mod.statType, mod.value);
            }
        }
    }

    protected virtual void ReCalculateHPAndMP()
    {
        if (HasStat(StatType.CurrentHP) && HasStat(StatType.HP))
        {
            if (Get(StatType.CurrentHP) > Get(StatType.HP))
                Set(StatType.CurrentHP, Get(StatType.HP));
        }

        if (HasStat(StatType.CurrentMana) && HasStat(StatType.Mana))
        {
            if (Get(StatType.CurrentMana) > Get(StatType.Mana))
                Set(StatType.CurrentMana, Get(StatType.Mana));
        }
    }

    private bool HasStat(StatType type) => stats != null && stats.ContainsKey(type);

    private void EnsureInitialized()
    {
        if (stats == null || baseStatsDict == null)
            BuildStatsFromBase(overwriteValues: false);
    }

    private ReactiveStat GetOrCreateStat(StatType type)
    {
        EnsureInitialized();

        if (stats.TryGetValue(type, out var stat) && stat != null)
            return stat;

        stat = new ReactiveStat { statType = type };
        stats[type] = stat;
        return stat;
    }

    private void BuildStatsFromBase(bool overwriteValues)
    {
        if (stats == null)
            stats = new Dictionary<StatType, ReactiveStat>();

        if (baseStatsDict == null)
            baseStatsDict = new Dictionary<StatType, ReactiveStat>();
        else
            baseStatsDict.Clear();

        if (baseStat == null) return;

        HashSet<StatType> activeTypes = overwriteValues ? new HashSet<StatType>() : null;

        if (baseStat.statsList != null)
        {
            foreach (var stat in baseStat.statsList)
            {
                if (stat == null) continue;
                activeTypes?.Add(stat.statType);

                if (stats.TryGetValue(stat.statType, out var existing))
                {
                    if (overwriteValues)
                        existing.Set(stat.Get());
                }
                else
                {
                    stats[stat.statType] = stat;
                }
            }
        }

        if (baseStat.baseStats != null)
        {
            foreach (var bs in baseStat.baseStats)
            {
                if (bs == null) continue;
                activeTypes?.Add(bs.statType);

                baseStatsDict[bs.statType] = bs;

                if (!stats.ContainsKey(bs.statType))
                    stats[bs.statType] = bs;
            }
        }

        if (activeTypes != null && activeTypes.Count > 0)
        {
            foreach (var kvp in stats)
            {
                if (!activeTypes.Contains(kvp.Key))
                    kvp.Value.Set(0f);
            }
        }
    }
}
