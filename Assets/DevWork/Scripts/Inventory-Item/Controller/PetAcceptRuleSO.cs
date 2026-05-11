using UnityEngine;

[CreateAssetMenu(menuName = "Inventory/Rules/Pet")]
public class PetAcceptRuleSO : SlotAcceptRuleSO
{
    public override bool CanAccept(Item item)
    {
        if (item == null)
            return false;

        return item.Type == ItemType.Pet;
    }

    public override bool CanAccept(InventoryItem inventoryItem)
    {
        if (inventoryItem == null || inventoryItem.itemData == null)
            return false;

        return inventoryItem.itemData.Type == ItemType.Pet;
    }
}
