using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;
using System.Collections.Generic;

public enum InventoryType
{
    Inventory,
    Accessory,
    Pickaxe,
    CraftingStation,
    CraftingOut,
    Split
}

[CreateAssetMenu(fileName = "NewInventoryData", menuName = "Inventory/InventoryData")]
public class InventoryData : ScriptableObject
{
    [SerializeField] private int size = 20;
    [SerializeField] private Item nullItem;
    public InventoryType inventoryType;

    [ShowInInspector]
    public ReactiveCollection<InventoryItem> Items { get; private set; } = new ReactiveCollection<InventoryItem>();

    private void OnEnable()
    {
        if (Items.Count == 0)
        {
            for (int i = 0; i < size; i++)
                Items.Add(new InventoryItem(nullItem, 1));
        }
    }
    public int GetSize() => size;

    public void SetItem(int index, InventoryItem item)
    {
        if (item == null || item.quantity.Value == 0)
        {
            Items[index] = new InventoryItem(nullItem, 0);
        }
        else
        {
            Items[index] = item;
        }
    }

    public bool AddItem(InventoryItem newItem) =>
        TryStackItem(newItem) || TryPlaceInEmptySlot(newItem);

    private bool TryStackItem(InventoryItem item)
    {
        foreach (var existing in Items)
        {
            if (existing != null && existing.CanStackWith(item))
            {
                int added = existing.AddQuantity(item.quantity.Value);
                item.quantity.Value -= added;
                if (item.quantity.Value <= 0) return true;
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
                int toAdd = Mathf.Min(item.quantity.Value, item.itemData.MaxStack);
                Items[i] = new InventoryItem(item.itemData, toAdd);
                item.quantity.Value -= toAdd;
                if (item.quantity.Value <= 0) return true;
            }
        }
        return false;
    }

    public bool RemoveItemAt(int index)
    {
        if (IsValidSlot(index))
        {
            Items[index] = new InventoryItem(nullItem, 0);    
            return true;
        }
        return false;
    }

    public bool RemoveItem(InventoryItem itemToRemove)
    {
        foreach (var slot in Items)
        {
            if (slot?.itemData == itemToRemove.itemData)
            {
                slot.quantity.Value -= itemToRemove.quantity.Value;
                if (slot.quantity.Value <= 0)
                    ClearSlot(slot);
                return true;
            }
        }
        return false;
    }

    public bool HasItem(Item targetItem, int requiredQuantity)
    {
        int totalFound = 0;
        foreach (var slot in Items)
        {
            if (slot?.itemData == targetItem)
            {
                totalFound += slot.quantity.Value;
                if (totalFound >= requiredQuantity) return true;
            }
        }
        return false;
    }

    public InventoryItem GetItem(int index) => Items[index];

    private bool IsValidSlot(int index) => index >= 0 && index < Items.Count && Items[index] != null;

    private void ClearSlot(InventoryItem slot)
    {
        slot.itemData = null;
        slot.quantity.Value = 0;
    }
}
