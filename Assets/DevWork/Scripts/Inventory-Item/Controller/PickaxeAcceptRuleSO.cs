using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Rules/Pickaxe")]
public class PickaxeAcceptRuleSO : SlotAcceptRuleSO
{
    public override bool CanAccept(Item item) => item != null && item.Type == ItemType.Pickaxe;

    public override bool CanAccept(InventoryItem inventoryItem) =>
        inventoryItem != null &&
        inventoryItem.itemData != null &&
        inventoryItem.itemData.Type == ItemType.Pickaxe;
}

