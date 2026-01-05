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
        stats = new Dictionary<StatType, ReactiveStat>();
        foreach (var stat in baseStat.statsList)
        {
            stats[stat.statType] = stat;
        }

        baseStatsDict = new Dictionary<StatType, ReactiveStat>();
        foreach (var bs in baseStat.baseStats)
        {
            baseStatsDict[bs.statType] = bs;
        }

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

    public ReactiveProperty<float> GetReactive(StatType type) => stats[type].value;
    public float Get(StatType type) => stats[type].Get();
    public void Set(StatType type, float value) => stats[type].Set(value);
    public void Add(StatType type, float amount) => stats[type].Add(amount);
    public void Sub(StatType type, float amount) => stats[type].Sub(amount);

    public void ClearAll()
    {
        foreach (var stat in stats.Values)
        {
            if (baseStatsDict.TryGetValue(stat.statType, out var bs))
                stat.Set(bs.Get());
        }
    }

    public void SetBaseStat(BaseStat insertBaseStat) => baseStat = insertBaseStat;

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
        if (Get(StatType.CurrentHP) > Get(StatType.HP))
            Set(StatType.CurrentHP, Get(StatType.HP));
        if (Get(StatType.CurrentMana) > Get(StatType.Mana))
            Set(StatType.CurrentMana, Get(StatType.Mana));
    }
}
