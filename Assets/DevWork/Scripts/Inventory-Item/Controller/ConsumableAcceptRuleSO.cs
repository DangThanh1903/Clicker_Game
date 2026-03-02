using UnityEngine;


[CreateAssetMenu(menuName = "Inventory/Rules/Consumable")]
public class ConsumableAcceptRuleSO : SlotAcceptRuleSO
{
    public override bool CanAccept(Item item) => item != null && item.Type == ItemType.Consumable;

    public override bool CanAccept(InventoryItem inventoryItem) =>
        inventoryItem != null &&
        inventoryItem.itemData != null &&
        inventoryItem.itemData.Type == ItemType.Consumable;
}
