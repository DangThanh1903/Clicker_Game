using UnityEngine;
using UniRx;
using System.Collections.Generic;

public class SplitInventoryController : MonoBehaviour
{
    public InventorySection splitInventorySection;

    void Start()
    {
        InventorySlotFactory.CreateSlots(splitInventorySection);
    }
    public void SplitCurrentItems()
    {
        var inventoryData = splitInventorySection.inventoryData;
        if (inventoryData == null || inventoryData.Items.Count == 0)
            return;

        Item commonItem = null;
        int totalQuantity = 0;

        // Check all slots for item consistency and sum quantity
        for (int i = 0; i < inventoryData.Items.Count; i++)
        {
            var slot = inventoryData.Items[i];
            if (slot == null || slot.itemData == null || slot.itemData.Type == ItemType.None || slot.quantity.Value == 0)
                continue; // skip empty slots

            if (commonItem == null)
            {
                commonItem = slot.itemData;
            }
            else if (slot.itemData != commonItem)
            {
                Debug.LogWarning("Split inventory has mixed items, cannot split.");
                return;
            }

            totalQuantity += slot.quantity.Value;
        }

        if (commonItem == null || totalQuantity <= 0)
            return;

        int slotCount = inventoryData.Items.Count;
        int baseAmount = totalQuantity / slotCount;
        int remainder = totalQuantity % slotCount;

        // Distribute quantity evenly or clear slot if zero
        for (int i = 0; i < slotCount; i++)
        {
            int quantityToSet = baseAmount + (i < remainder ? 1 : 0);
            if (quantityToSet > 0)
            {
                inventoryData.SetItem(i, new InventoryItem(commonItem, quantityToSet));
            }
            else
            {
                // Clear slot (set to null item or null)
                inventoryData.SetItem(i, new InventoryItem(null, 0));
            }
        }
    }

}
