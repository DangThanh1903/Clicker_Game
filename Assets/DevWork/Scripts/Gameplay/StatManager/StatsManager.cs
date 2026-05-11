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

        InventoryData bagInventory = uiManager.GetInventoryData(InventoryType.Inventory);
        if (WeaponSelectionService.TryGetStrongestWeaponItem(bagInventory, out InventoryItem strongestWeaponItem))
            AccumulateItemModifiers(accumulator, strongestWeaponItem);

        foreach (var section in uiManager.inventorySections)
        {
            if (section == null || section.inventoryData == null)
                continue;

            var invData = section.inventoryData;
            if (invData.inventoryType != InventoryType.Accessory &&
                invData.inventoryType != InventoryType.Pet)
            {
                continue;
            }

            foreach (var invItem in invData.Items)
            {
                AccumulateItemModifiers(accumulator, invItem);
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

        InventoryData bagInventory = uiManager != null
            ? uiManager.GetInventoryData(InventoryType.Inventory)
            : null;
        if (WeaponSelectionService.TryGetStrongestWeaponItem(bagInventory, out InventoryItem strongestWeaponItem))
            ApplyPassiveBuffsFromItem(strongestWeaponItem);

        if (uiManager != null && uiManager.inventorySections != null)
        {
            foreach (var section in uiManager.inventorySections)
            {
                if (section == null || section.inventoryData == null)
                    continue;

                var invData = section.inventoryData;
                if (invData.inventoryType != InventoryType.Accessory &&
                    invData.inventoryType != InventoryType.Pet)
                {
                    continue;
                }

                foreach (var invItem in invData.Items)
                {
                    ApplyPassiveBuffsFromItem(invItem);
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

    private void AccumulateItemModifiers(ModifierAccumulator accumulator, InventoryItem inventoryItem)
    {
        if (accumulator == null || inventoryItem == null)
            return;

        var item = inventoryItem.itemData;
        if (item == null || item.Type == ItemType.None)
            return;

        if (item is IStatProvider provider)
        {
            foreach (var mod in provider.GetStatModifiers())
                AccumulateModifier(accumulator, mod);
        }

        foreach (var prefixModifier in ItemPrefixConfig.GetFlatMods(inventoryItem.prefix))
            AccumulateModifier(accumulator, prefixModifier);
    }

    private void ApplyPassiveBuffsFromItem(InventoryItem inventoryItem)
    {
        if (inventoryItem == null)
            return;

        var item = inventoryItem.itemData;
        if (item == null || item.Type == ItemType.None)
            return;

        if (item is Pickaxe weapon)
        {
            buffManager.ApplyItemBuffs(item, weapon.GetPassiveBuffs());
            currentEquippedPassiveItems.Add(item);
            return;
        }

        if (item is Accessory accessory)
        {
            buffManager.ApplyItemBuffs(item, accessory.GetPassiveBuffs());
            currentEquippedPassiveItems.Add(item);
            return;
        }

        if (item is PetItem pet)
        {
            buffManager.ApplyItemBuffs(item, pet.GetPassiveBuffs());
            currentEquippedPassiveItems.Add(item);
        }
    }
}
