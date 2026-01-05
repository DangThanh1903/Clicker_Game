using UnityEngine;

public class StatsManager : StatsManagerBase
{
    public static StatsManager Ins { get; private set; }

    [SerializeField] private InventoryUIManager uiManager; // to read equipped items

    protected override void Awake()
    {
        if (Ins != null)
        {
            Destroy(gameObject);
            return;
        }

        Ins = this;
        base.Awake();
        EnableQuestStatSignals();
    }

    public override void RecalculateAllStats()
    {
        ClearAll();

        // 1) Equipment stats (from pickaxe/accessory)
        if (uiManager != null)
        {
            foreach (var section in uiManager.inventorySections)
            {
                var invData = section.inventoryData;
                if (invData == null) continue;

                if (invData.inventoryType != InventoryType.Pickaxe &&
                    invData.inventoryType != InventoryType.Accessory)
                    continue;

                foreach (var invItem in invData.Items)
                {
                    if (invItem == null) continue;

                    var item = invItem.itemData;
                    if (item == null || item.Type == ItemType.None) continue;

                    // Base item stats (from ScriptableObject)
                    if (item is IStatProvider provider)
                    {
                        foreach (var mod in provider.GetStatModifiers())
                            Add(mod.statType, mod.value);
                    }

                    // Prefix stats (flat bonuses only, per instance)
                    // NOTE: requires InventoryItem.prefix (unified prefix)
                    foreach (var pm in ItemPrefixConfig.GetFlatMods(invItem.prefix))
                        Add(pm.statType, pm.value);
                }
            }
        }

        // 2) Apply all buffs (starter + item + consumable + conditional)
        ApplyBuffs();

        ReCalculateHPAndMP();

        // 3) Fire event
        RaiseStatsRecalculated();
    }


    // For consumables
    public void ApplyConsumableBuff(BuffSO buff)
    {
        if (buffManager == null || buff == null) return;
        buffManager.ApplyBuff(buff);
    }
}
