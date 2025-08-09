using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryController : MonoBehaviour
{
    public static InventoryController Instance;
    [SerializeField] private InventoryUIManager uiManager;
    [SerializeField] private CraftingController craftingController;
    [SerializeField] private SplitInventoryController splitInventoryController;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);


        InventoryItem.LoadNoneItem(() =>
        {
            Debug.Log("'None' item ready for inventory.");
        });
    }

    // ==============================
    // SECTION: Inventory Logic
    // ==============================

    public void AddItemToInventory(InventoryItem inventoryItem)
    {
        uiManager.AddItemToInventorySection(inventoryItem);
    }

    public bool TrySwap(
    InventoryData fromData, int fromIndex,
    InventoryData toData, int toIndex,
    SlotAcceptRuleSO fromRule, SlotAcceptRuleSO toRule)
    {
        Debug.Log("Attempting swap...");

        if (fromData == null || toData == null) return false;
        if (fromIndex < 0 || fromIndex >= fromData.Items.Count) return false;
        if (toIndex < 0 || toIndex >= toData.Items.Count) return false;

        var itemA = fromData.Items[fromIndex];
        var itemB = toData.Items[toIndex];

        // Self-drop: same slot, same inventory
        if (fromData == toData && fromIndex == toIndex)
            return false;

        // Reject if either can't accept the other's item (cross-inventory)
        if (fromData != toData)
        {
            if (!fromRule.CanAccept(itemB) && itemB.itemData.Type != ItemType.None)
            {
                Debug.LogError("Drop rejected: fromRule");
                return false;
            }
            if (!toRule.CanAccept(itemA) && itemA.itemData.Type != ItemType.None)
            {
                Debug.LogError("Drop rejected: toRule");
                return false;
            }
        }

        // Same item type and stackable → Proper stacking logic
        if (itemA.itemData == itemB.itemData && itemA.itemData.MaxStack > 1)
        {
            Debug.Log("Stacking items");

            int totalQuantity = itemA.quantity.Value + itemB.quantity.Value;
            int maxStack = itemB.itemData.MaxStack;

            if (totalQuantity <= maxStack)
            {
                // All can be stacked into the target slot
                toData.Items[toIndex].quantity.Value = totalQuantity;
                fromData.RemoveItemAt(fromIndex);
            }
            else
            {
                // Only stack up to max in target
                toData.Items[toIndex].quantity.Value = maxStack;

                // Calculate remaining amount
                int remainder = totalQuantity - maxStack;

                // Put the remainder back in the from slot
                fromData.Items[fromIndex].quantity.Value = remainder;
            }

            return true;
        }


        // Perform regular swap
        fromData.SetItem(fromIndex, itemB);
        toData.SetItem(toIndex, itemA);

        Debug.Log("Swapped items");

        if (fromData.inventoryType != InventoryType.Inventory || toData.inventoryType != InventoryType.Inventory)
        {
            UpdateStat();
        }
        return true;
    }


    // ==============================
    // SECTION: Stat Logic
    // ==============================

    void UpdateStat()
    {
        StatsManager.Ins.ClearAll();

        foreach (var section in uiManager.inventorySections)
        {
            // ✅ Only check Pickaxe and Accessory slots
            if (section.inventoryData.inventoryType != InventoryType.Pickaxe &&
                section.inventoryData.inventoryType != InventoryType.Accessory)
                continue;

            foreach (var item in section.inventoryData.Items)
            {
                if (item?.itemData == null)
                    continue;

                if (item.itemData is IStatProvider provider)
                {
                    foreach (var mod in provider.GetStatModifiers())
                    {
                        StatsManager.Ins.Add(mod.statType, mod.value);
                        Debug.Log($"[Stat] {mod.statType}: +{mod.value} from {item.itemData.name}");
                    }
                }
            }
        }
    }




}
