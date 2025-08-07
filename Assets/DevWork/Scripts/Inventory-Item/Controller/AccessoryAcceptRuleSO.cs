using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Inventory/Rules/Accessory")]
public class AccessoryAcceptRuleSO : SlotAcceptRuleSO
{
    [SerializeField] private InventoryData equippedAccessories;
    public override bool CanAccept(Item item)
    {
        if (item.Type != ItemType.Accessory)
            return false;

        foreach (var equippedItem in equippedAccessories.Items)
        {
            if (equippedItem != null && equippedItem.itemData == item)
                return false;
        }

        return true;
    }

    public override bool CanAccept(InventoryItem inventoryItem)
    {
        if (inventoryItem.itemData.Type != ItemType.Accessory)
        {
            return false;
        }

        foreach (var equippedItem in equippedAccessories.Items)
        {
            if (equippedItem != null && equippedItem.itemData == inventoryItem.itemData)
                return false;
        }

        return true;
    }


}
