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

    protected sealed class ModifierAccumulator
    {
        public readonly Dictionary<StatType, float> AddTotals = new Dictionary<StatType, float>();
        public readonly Dictionary<StatType, float> MultiplyTotals = new Dictionary<StatType, float>();
    }

    protected ModifierAccumulator CreateModifierAccumulator() => new ModifierAccumulator();

    protected void AccumulateModifier(ModifierAccumulator accumulator, StatModifier mod)
    {
        if (accumulator == null)
            return;

        if (mod.mode == StatModifierMode.Multiply)
        {
            if (accumulator.MultiplyTotals.TryGetValue(mod.statType, out float currentMul))
                accumulator.MultiplyTotals[mod.statType] = currentMul * mod.value;
            else
                accumulator.MultiplyTotals[mod.statType] = mod.value;
            return;
        }

        if (accumulator.AddTotals.TryGetValue(mod.statType, out float currentAdd))
            accumulator.AddTotals[mod.statType] = currentAdd + mod.value;
        else
            accumulator.AddTotals[mod.statType] = mod.value;
    }

    protected void ApplyAccumulatedModifiers(ModifierAccumulator accumulator)
    {
        if (accumulator == null)
            return;

        foreach (var add in accumulator.AddTotals)
            Add(add.Key, add.Value);

        foreach (var mul in accumulator.MultiplyTotals)
            Set(mul.Key, Get(mul.Key) * mul.Value);
    }

    private static bool ShouldPreserveAcrossRecalculate(StatType type)
    {
        switch (type)
        {
            case StatType.CurrentHP:
            case StatType.CurrentMana:
            case StatType.CurrentStamina:
            case StatType.Clicks:
            case StatType.ClickPerTick:
            case StatType.Diamond:
            case StatType.HoldedTime:
            case StatType.TotalBlockBreaked:
            case StatType.TotalDamageDealed:
            case StatType.TotalTimePlayed:
                return true;
            default:
                return false;
        }
    }

    public void ClearAll()
    {
        Dictionary<StatType, float> preservedValues = null;
        if (stats != null)
        {
            foreach (var entry in stats)
            {
                if (!ShouldPreserveAcrossRecalculate(entry.Key) || entry.Value == null)
                    continue;

                preservedValues ??= new Dictionary<StatType, float>();
                preservedValues[entry.Key] = entry.Value.Get();
            }
        }

        foreach (var stat in stats.Values)
        {
            if (baseStatsDict.TryGetValue(stat.statType, out var bs))
                stat.Set(bs.Get());
        }

        if (preservedValues != null)
        {
            foreach (var entry in preservedValues)
            {
                if (HasStat(entry.Key))
                    Set(entry.Key, entry.Value);
            }
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

        var accumulator = CreateModifierAccumulator();
        CollectBuffModifiers(accumulator);
        ApplyAccumulatedModifiers(accumulator);

        ReCalculateHPAndMP();

        RaiseStatsRecalculated();
    }

    // Shared buff collection for everyone.
    protected virtual void CollectBuffModifiers(ModifierAccumulator accumulator)
    {
        if (buffManager == null || accumulator == null)
            return;

        foreach (var buffInst in buffManager.GetAllBuffs())
        {
            if (!buffInst.IsActive) continue;

            var data  = buffInst.buffData;
            int stacks = buffInst.StackCount;

            foreach (var mod in data.GetEffectiveModifiers(stacks))
            {
                AccumulateModifier(accumulator, mod);
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

        if (HasStat(StatType.CurrentStamina) && HasStat(StatType.Stamina))
        {
            float staminaMax = Mathf.Max(0f, Get(StatType.Stamina));
            Set(StatType.CurrentStamina, Mathf.Clamp(Get(StatType.CurrentStamina), 0f, staminaMax));
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

        stat = CreateRuntimeStat(type, 0f);
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
                float value = ReadStatValue(stat);

                if (stats.TryGetValue(stat.statType, out var existing))
                {
                    if (overwriteValues)
                        existing.Set(value);
                }
                else
                {
                    stats[stat.statType] = CreateRuntimeStat(stat.statType, value);
                }
            }
        }

        if (baseStat.baseStats != null)
        {
            foreach (var bs in baseStat.baseStats)
            {
                if (bs == null) continue;
                activeTypes?.Add(bs.statType);
                float baseValue = ReadStatValue(bs);

                if (baseStatsDict.TryGetValue(bs.statType, out var existingBase))
                    existingBase.Set(baseValue);
                else
                    baseStatsDict[bs.statType] = CreateRuntimeStat(bs.statType, baseValue);

                if (!stats.ContainsKey(bs.statType))
                    stats[bs.statType] = CreateRuntimeStat(bs.statType, baseValue);
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

    private static float ReadStatValue(ReactiveStat stat)
    {
        if (stat == null)
            return 0f;
        return stat.value != null ? stat.value.Value : 0f;
    }

    private static ReactiveStat CreateRuntimeStat(StatType type, float value)
    {
        return new ReactiveStat
        {
            statType = type,
            value = new ReactiveProperty<float>(value)
        };
    }
}
