using UnityEngine;


[CreateAssetMenu(menuName = "Inventory/Rules/Accessory")]
public class AccessoryAcceptRuleSO : SlotAcceptRuleSO
{
    [SerializeField] private InventoryData equippedAccessories;
    public override bool CanAccept(Item item)
    {
        if (item == null || item.Type != ItemType.Accessory)
            return false;

        if (equippedAccessories == null || equippedAccessories.Items == null)
            return true;

        foreach (var equippedItem in equippedAccessories.Items)
        {
            if (equippedItem != null && equippedItem.itemData == item)
                return false;
        }

        return true;
    }

    public override bool CanAccept(InventoryItem inventoryItem)
    {
        if (inventoryItem == null || inventoryItem.itemData == null || inventoryItem.itemData.Type != ItemType.Accessory)
            return false;

        if (equippedAccessories == null || equippedAccessories.Items == null)
            return true;

        foreach (var equippedItem in equippedAccessories.Items)
        {
            if (equippedItem != null && equippedItem.itemData == inventoryItem.itemData)
                return false;
        }

        return true;
    }


}
