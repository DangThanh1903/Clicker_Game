using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuffManager : MonoBehaviour
{
    [SerializeField] private StatsManagerBase statsManager; // auto-wire if null in Awake

    private readonly List<BuffInstance> activeBuffs = new List<BuffInstance>();

    // Only used for the PLAYER path (item-originated buffs)
    private readonly Dictionary<Item, Dictionary<BuffSO, BuffInstance>> itemBuffMap
        = new Dictionary<Item, Dictionary<BuffSO, BuffInstance>>();

    void Awake()
    {
        if (!statsManager) statsManager = GetComponent<StatsManagerBase>();
    }

    public void Initialize(StatsManagerBase stats) => statsManager = stats;

    // -------- Generic (enemy/boss OR player) ----------
    public BuffInstance ApplyBuff(BuffSO buff, object source = null)
    {
        if (buff == null || statsManager == null) return null;
        var inst = new BuffInstance(buff, statsManager);
        // Track source if you want to remove later by source object (boss phase, aura, etc.)
        inst.SourceItem = source as Item;
        activeBuffs.Add(inst);
        return inst;
    }

    public void RemoveBuff(BuffInstance buff)
    {
        if (buff == null) return;
        if (activeBuffs.Remove(buff)) buff.Dispose();
    }

    public void ClearAllBuffs()
    {
        for (int i = 0; i < activeBuffs.Count; i++) activeBuffs[i]?.Dispose();
        activeBuffs.Clear();
        itemBuffMap.Clear();
    }

    public IReadOnlyList<BuffInstance> GetActiveBuffs() =>
        activeBuffs.Where(b => b.IsActive).ToList();

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
        }
    }

    public void RemoveBuffsFromItem(Item item)
    {
        if (!itemBuffMap.TryGetValue(item, out var map)) return;

        foreach (var kvp in map) kvp.Value?.Deactivate();
        activeBuffs.RemoveAll(b => b.SourceItem == item);
        itemBuffMap.Remove(item);
    }

    void OnDisable() { ClearAllBuffs(); } // good for pooled enemies
}
