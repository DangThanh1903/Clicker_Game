using UnityEngine;
using System.Collections.Generic;

public class StatsManager : StatsManagerBase
{
    public static StatsManager Ins { get; private set; }

    [SerializeField] private InventoryUIManager uiManager; // to read equipped items
    private readonly HashSet<Item> equippedItemsWithPassiveBuffs = new HashSet<Item>();
    private readonly HashSet<Item> currentEquippedPassiveItems = new HashSet<Item>();
    private readonly List<Item> removedPassiveItems = new List<Item>(8);
    private bool isRecalculating;
    private bool recalculateQueued;

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
        if (isRecalculating)
        {
            recalculateQueued = true;
            return;
        }

        isRecalculating = true;
        try
        {
            do
            {
                recalculateQueued = false;
                ClearAll();
                var accumulator = CreateModifierAccumulator();
                CollectEquipmentStatModifiers(accumulator);
                SyncEquippedItemPassiveBuffs();
                CollectBuffModifiers(accumulator);
                ApplyAccumulatedModifiers(accumulator);
                ReCalculateHPAndMP();
                RaiseStatsRecalculated();
            }
            while (recalculateQueued);
        }
        finally
        {
            isRecalculating = false;
        }
    }

    private void CollectEquipmentStatModifiers(ModifierAccumulator accumulator)
    {
        if (accumulator == null || uiManager == null || uiManager.inventorySections == null)
            return;

        foreach (var section in uiManager.inventorySections)
        {
            if (section == null || section.inventoryData == null)
                continue;

            var invData = section.inventoryData;
            if (invData.inventoryType != InventoryType.Pickaxe &&
                invData.inventoryType != InventoryType.Accessory)
            {
                continue;
            }

            foreach (var invItem in invData.Items)
            {
                if (invItem == null)
                    continue;

                var item = invItem.itemData;
                if (item == null || item.Type == ItemType.None)
                    continue;

                if (item is IStatProvider provider)
                {
                    foreach (var mod in provider.GetStatModifiers())
                        AccumulateModifier(accumulator, mod);
                }

                foreach (var pm in ItemPrefixConfig.GetFlatMods(invItem.prefix))
                    AccumulateModifier(accumulator, pm);
            }
        }
    }

    private void SyncEquippedItemPassiveBuffs()
    {
        if (buffManager == null)
        {
            equippedItemsWithPassiveBuffs.Clear();
            return;
        }

        currentEquippedPassiveItems.Clear();

        if (uiManager != null && uiManager.inventorySections != null)
        {
            foreach (var section in uiManager.inventorySections)
            {
                if (section == null || section.inventoryData == null)
                    continue;

                var invData = section.inventoryData;
                if (invData.inventoryType != InventoryType.Pickaxe &&
                    invData.inventoryType != InventoryType.Accessory)
                {
                    continue;
                }

                foreach (var invItem in invData.Items)
                {
                    if (invItem == null)
                        continue;

                    var item = invItem.itemData;
                    if (item == null || item.Type == ItemType.None)
                        continue;

                    if (item is Pickaxe pickaxe)
                    {
                        buffManager.ApplyItemBuffs(item, pickaxe.GetPassiveBuffs());
                        currentEquippedPassiveItems.Add(item);
                    }
                    else if (item is Accessory accessory)
                    {
                        buffManager.ApplyItemBuffs(item, accessory.GetPassiveBuffs());
                        currentEquippedPassiveItems.Add(item);
                    }
                }
            }
        }

        removedPassiveItems.Clear();
        foreach (var item in equippedItemsWithPassiveBuffs)
        {
            if (!currentEquippedPassiveItems.Contains(item))
                removedPassiveItems.Add(item);
        }

        for (int i = 0; i < removedPassiveItems.Count; i++)
        {
            var item = removedPassiveItems[i];
            buffManager.RemoveBuffsFromItem(item);
            equippedItemsWithPassiveBuffs.Remove(item);
        }

        foreach (var item in currentEquippedPassiveItems)
            equippedItemsWithPassiveBuffs.Add(item);
    }


    // For consumables
    public void ApplyConsumableBuff(BuffSO buff)
    {
        if (buffManager == null || buff == null) return;
        buffManager.ApplyBuff(buff);
    }
}
