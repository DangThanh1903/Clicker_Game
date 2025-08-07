using Sirenix.OdinInspector;
using UnityEngine;
using UniRx;

[System.Serializable]
public class InventoryItem
{
    [AssetSelector]
    [InlineEditor(InlineEditorModes.GUIAndPreview)]
    public Item itemData;
    public ReactiveProperty<int> quantity;

    public InventoryItem(Item itemData, int quantity)
    {
        this.itemData = itemData;
        this.quantity = new ReactiveProperty<int>(quantity);
    }

    public bool CanStackWith(InventoryItem other) =>
        other != null && itemData == other.itemData;

    public int AddQuantity(int amount)
    {
        int stackLimit = itemData.MaxStack;
        int space = stackLimit - quantity.Value;
        int toAdd = Mathf.Min(space, amount);
        quantity.Value += toAdd;
        return toAdd;
    }

    public void Use(GameObject user)
    {
        itemData.Use(user);
        quantity.Value = Mathf.Max(quantity.Value - 1, 0);
    }
}

