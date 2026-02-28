using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Rules/CraftingOut")]
public class DontAccpetAnyRuleSO : SlotAcceptRuleSO
{
    public override bool CanAccept(Item item)
    {
        return item == null || item.Type == ItemType.None;
    }

    public override bool CanAccept(InventoryItem inventoryItem)
    {
        return inventoryItem == null
            || inventoryItem.itemData == null
            || inventoryItem.itemData.Type == ItemType.None;
    }
}
