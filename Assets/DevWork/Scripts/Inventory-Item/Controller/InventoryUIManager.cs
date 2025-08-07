using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIManager : MonoBehaviour
{
    [Serializable]
    public class InventorySection
    {
        public string name;
        public InventoryData inventoryData;
        public Transform slotParent;
        public GameObject slotPrefab;
    }

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

    public void CreateAllInventorySlots()
    {
        foreach (var section in inventorySections)
        {
            CreateInventorySlots(section);
        }
    }

    public void CreateInventorySlots(InventorySection section)
    {
        // Clear existing slots
        foreach (Transform child in section.slotParent)
            Destroy(child.gameObject);

        for (int i = 0; i < section.inventoryData.Items.Count; i++)
        {
            var slot = Instantiate(section.slotPrefab, section.slotParent);
            slot.GetComponent<InventorySlotUI>().Bind(section.inventoryData, i);
        }
    }
}
