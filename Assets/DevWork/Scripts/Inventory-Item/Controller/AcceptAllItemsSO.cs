using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Rules/InventoryItemSlot")]
public class AcceptAllItemsSO : SlotAcceptRuleSO
{
    public override bool CanAccept(Item item)
    {
        return item != null;
    }

    public override bool CanAccept(InventoryItem inventoryItem)
    {
        return inventoryItem != null && inventoryItem.itemData != null;
    }
}
