using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;
using System.Collections.Generic;

public enum InventoryType
{
    Inventory,
    Accessory,
    Pickaxe
}

[CreateAssetMenu(fileName = "NewInventoryData", menuName = "Inventory/InventoryData")]
public class InventoryData : ScriptableObject
{
    [SerializeField] private int size = 20;
    [SerializeField] private Item nullItem;
    public InventoryType inventoryType;

    [ShowInInspector]
    public ReactiveCollection<InventoryItem> Items { get; private set; }

    private void OnEnable()
    {
        if (Items == null || Items.Count == 0)
        {
            Items = new ReactiveCollection<InventoryItem>();
            for (int i = 0; i < size; i++)
            {
                Items.Add(new InventoryItem(nullItem, 1));
            }
        }
    }

    public void SetItem(int index, InventoryItem item)
    {
        Items[index] = item;
    }

    public bool AddItem(InventoryItem newItem)
    {
        if (TryStackItem(newItem))
            return true;

        if (TryPlaceInEmptySlot(newItem))
            return true;

        return false;
    }

    private bool TryStackItem(InventoryItem item)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            var existing = Items[i];
            if (existing != null && existing.CanStackWith(item))
            {
                int added = existing.AddQuantity(item.quantity.Value);
                item.quantity.Value -= added;

                if (item.quantity.Value <= 0)
                    return true;
            }
        }
        return false;
    }

    private bool TryPlaceInEmptySlot(InventoryItem item)
    {
        for (int i = 0; i < Items.Count; i++)
        {
            if (Items[i] == null || Items[i].itemData == nullItem)
            {
                int stackLimit = item.itemData.MaxStack;
                int toAdd = Mathf.Min(item.quantity.Value, stackLimit);
                Items[i] = new InventoryItem(item.itemData, toAdd);
                item.quantity.Value -= toAdd;

                if (item.quantity.Value <= 0)
                    return true;
            }
        }
        return false;
    }

    public bool RemoveItemAt(int index)
    {
        if (index >= 0 && index < Items.Count && Items[index] != null)
        {
            Items[index] = null;
            return true;
        }
        return false;
    }

    public InventoryItem GetItem(int index) => Items[index];
}
