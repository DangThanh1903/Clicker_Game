using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Rules/Pickaxe")]
public class PickaxeAcceptRuleSO : SlotAcceptRuleSO
{
    public override bool CanAccept(Item item) => item.Type == ItemType.Pickaxe;

    public override bool CanAccept(InventoryItem inventoryItem) => inventoryItem.itemData.Type == ItemType.Pickaxe;
}

