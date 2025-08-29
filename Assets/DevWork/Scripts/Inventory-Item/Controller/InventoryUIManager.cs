using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using System.Linq;

public class InventoryUIManager : MonoBehaviour
{
    public InventorySection[] inventorySections;

    private void Start()
    {
        CreateAllInventorySlots();
    }

    public bool AddItemToInventorySection(InventoryItem inventoryItem)
    {
        foreach (var section in inventorySections)
        {
            if (section.inventoryData.inventoryType == InventoryType.Inventory)
            {
                return section.inventoryData.AddItem(inventoryItem);
            }
        }

        Debug.LogWarning("No Inventory section found with type 'Inventory'.");
        return false;
    }
    public void SortInventory()
    {
        var section = inventorySections.FirstOrDefault(s => s.inventoryData.inventoryType == InventoryType.Inventory);
        if (section != null)
            section.inventoryData.SortAndRepack_UsingAddItem();
    }


    public void CreateAllInventorySlots()
    {
        foreach (var section in inventorySections)
        {
            InventorySlotFactory.CreateSlots(section);
        }
    }

    public InventoryData GetInventoryForSlot(InventorySlotUI slotUI)
    {
        foreach (var section in inventorySections)
        {
            if (section.slotUIs.Contains(slotUI))
                return section.inventoryData;
        }
        return null;
    }
    public InventoryItem GetPickaxe()
    {
        foreach (var section in inventorySections)
        {
            if (section.inventoryData.inventoryType == InventoryType.Pickaxe)
                return section.inventoryData.Items[0];
        }
        return null;
    }
}

[Serializable]
public class InventorySection
{
    public string name;
    public InventoryData inventoryData;
    public Transform slotParent;
    public GameObject slotPrefab;

    [NonSerialized] public List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
    [NonSerialized] public CompositeDisposable disposables = new CompositeDisposable();
}
