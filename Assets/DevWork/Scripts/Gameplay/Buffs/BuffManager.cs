using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuffManager : MonoBehaviour
{
    [SerializeField] private StatsManagerBase statsManager; // auto-wire if null in Awake

    private readonly List<BuffInstance> activeBuffs = new List<BuffInstance>();

    public IEnumerable<BuffInstance> GetAllBuffs() => activeBuffs;
    public IEnumerable<BuffInstance> GetDisplayBuffs() =>
    activeBuffs.Where(b => b.IsActive);

    // Only used for the PLAYER path (item-originated buffs)
    private readonly Dictionary<Item, Dictionary<BuffSO, BuffInstance>> itemBuffMap
        = new Dictionary<Item, Dictionary<BuffSO, BuffInstance>>();

    void Awake()
    {
        if (!statsManager) statsManager = GetComponent<StatsManagerBase>();
    }

    // -------- Generic (enemy/boss OR player) ----------
    public BuffInstance ApplyBuff(BuffSO buff, object source = null)
    {
        if (buff == null || statsManager == null) return null;

        var srcItem = source as Item;

        // 1) Look for an existing instance of this buff (same BuffSO + same source item)
        var existing = activeBuffs.FirstOrDefault(b =>
            b.buffData == buff &&
            b.SourceItem == srcItem);

        if (existing != null)
        {
            // ── Non-stackable: extend duration ─────────────────
            if (!buff.isStackable)
            {
                if (existing.HasDuration)
                    existing.ExtendDuration(buff.duration);
                // no need to RecalculateAllStats here – extending duration
                // doesn't change the stat value, only how long it lasts
            }
            // ── Stackable: add stack & reset duration ─────────
            else
            {
                existing.AddStackAndResetDuration();
            }

            return existing;
        }

        // 2) No existing buff instance → create a new one
        var inst = new BuffInstance(buff, statsManager)
        {
            SourceItem = srcItem
        };

        activeBuffs.Add(inst);

        // Normal buffs are activated here (after being in the list)
        if (buff is not ConditionalBuffSO)
        {
            inst.Activate();
        }

        return inst;
    }


    public void RemoveBuff(BuffInstance buff)
    {
        if (buff == null) return;
        if (activeBuffs.Remove(buff))
            buff.Dispose();
    }

    public void ClearAllBuffs()
    {
        foreach (var b in activeBuffs)
            b?.Dispose();

        activeBuffs.Clear();
        itemBuffMap.Clear();
    }

    public IReadOnlyList<BuffInstance> GetActiveBuffs() =>
        activeBuffs
            .Where(b => b.IsActive && b.buffData is not ConditionalBuffSO)
            .ToList();

    public IReadOnlyList<BuffInstance> GetConditionBuffs() =>
        activeBuffs
            .Where(b => b.buffData is ConditionalBuffSO)
            .ToList();

    // -------- Player-only (item-originated) ----------
    public void ApplyItemBuffs(Item item, IEnumerable<BuffSO> buffs)
    {
        if (item == null || buffs == null) return;

        if (!itemBuffMap.ContainsKey(item))
            itemBuffMap[item] = new Dictionary<BuffSO, BuffInstance>();

        foreach (var buff in buffs)
        {
            if (buff == null) continue;
            if (itemBuffMap[item].ContainsKey(buff)) continue;
            if (activeBuffs.Any(b => b.buffData == buff && b.SourceItem == item)) continue;

            var inst = new BuffInstance(buff, statsManager) { SourceItem = item };
            activeBuffs.Add(inst);
            itemBuffMap[item][buff] = inst;

            if (buff is not ConditionalBuffSO)
            {
                inst.Activate();
            }
        }
    }

    public void RemoveBuffsFromItem(Item item)
    {
        if (!itemBuffMap.TryGetValue(item, out var map)) return;

        foreach (var kvp in map)
            kvp.Value?.Deactivate(); // each Deactivate() triggers RecalculateAllStats()

        activeBuffs.RemoveAll(b => b.SourceItem == item);
        itemBuffMap.Remove(item);
    }

    void OnDisable()
    {
        ClearAllBuffs(); // good for pooled enemies
    }
}
