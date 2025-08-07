using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Inventory/Rules/Consumable")]
public class ConsumableAcceptRuleSO : SlotAcceptRuleSO
{
    public override bool CanAccept(Item item) => item.Type == ItemType.Consumable;

    public override bool CanAccept(InventoryItem inventoryItem) => inventoryItem.itemData.Type == ItemType.Consumable;
}
