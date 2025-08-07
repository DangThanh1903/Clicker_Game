using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryController : MonoBehaviour
{
    public static InventoryController Instance;
    [SerializeField] private InventoryUIManager uiManager;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // ==============================
    // SECTION: UI
    // ==============================

    private void Start()
    {
        uiManager.CreateAllInventorySlots();
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
        Debug.Log("About to swap");
        var itemA = fromData.Items[fromIndex];
        var itemB = toData.Items[toIndex];

        if (!fromRule.CanAccept(itemB) && itemB.itemData.Type != ItemType.None && fromData != toData)
        {
            Debug.LogError("Wrong drop! 1");
            return false;
        }
        if (!toRule.CanAccept(itemA) && fromData != toData)
        {
            Debug.LogError("Wrong drop! 2");
            return false;
        }


        fromData.SetItem(fromIndex, itemB);
        toData.SetItem(toIndex, itemA);

        Debug.Log("Swapped");

        UpdateStat(fromData, toData);
        return true;
    }

    // ==============================
    // SECTION: Stat Logic
    // ==============================

    void UpdateStat(InventoryData fromData, InventoryData toData)
    {
        if (fromData.inventoryType == InventoryType.Inventory
        && toData.inventoryType == InventoryType.Inventory)
            return;
        
        foreach (var inv in uiManager.inventorySections)
        {
            foreach (var item in inv.inventoryData.Items)
            {
                if (item.itemData is IStatProvider provider)
                {
                    IEnumerable<StatModifier> modifiers = provider.GetStatModifiers();

                    foreach (var mod in modifiers)
                    {
                        StatsManager.Ins.Set(mod.statType, mod.value);
                        Debug.Log($"Stat: {mod.statType}, Value: {mod.value}");
                    }
                }

            }
        }
    }

}
