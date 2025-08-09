using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DontAccpetAnyRuleSO : SlotAcceptRuleSO
{
    public override bool CanAccept(Item item)
    {
        return item.Type == ItemType.None;
    }

    public override bool CanAccept(InventoryItem inventoryItem)
    {
        return inventoryItem.itemData.Type == ItemType.None;
    }
}
