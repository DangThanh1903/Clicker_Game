using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;
using System.Collections.Generic;
using System;

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

    public Subject<(int index, InventoryItem newItem)> OnPlayerSetItem = new Subject<(int, InventoryItem)>();

    public Subject<InventoryItem> InventoryChanged = new Subject<InventoryItem>();

    private void OnEnable()
    {
        if (Items.Count == 0)
        {
            for (int i = 0; i < size; i++)
                Items.Add(new InventoryItem(nullItem, 1));
        }
    }
    public int GetSize() => size;

    public void SetItem(int index, InventoryItem item, bool isPlayerAction = false)
    {
        if (item == null || item.quantity.Value == 0)
        {
            Items[index] = new InventoryItem(nullItem, 0);
        }
        else
        {
            Items[index] = item;
            InventoryChanged.OnNext(item);
        }
        if (isPlayerAction)
        {
            OnPlayerSetItem.OnNext((index, item));
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
                InventoryChanged.OnNext(item);
                Debug.Log("Try place in empty slot");
                if (item.quantity.Value <= 0) return true;
            }
        }
        return false;
    }

    public bool RemoveItemAt(int index,  bool isPlayerAction = false)
    {
        if (IsValidSlot(index))
        {
            SetItem(index, new InventoryItem(null, 0), isPlayerAction);
            return true;
        }
        return false;
    }
    public bool SubtractQuantity(int index, int amount, bool isPlayerAction = false)
    {
        if (!IsValidSlot(index)) return false;

        var item = Items[index];
        if (item == null || item.itemData == nullItem || item.quantity.Value <= 1) return false;
        // careful, i just temporary use <= 1 for buff

        item.quantity.Value -= amount;
        if (item.quantity.Value <= 0)
        {
            SetItem(index, new InventoryItem(nullItem, 0), isPlayerAction);
        }
        else
        {
            SetItem(index, item, isPlayerAction);
        }
        return true;
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
}
