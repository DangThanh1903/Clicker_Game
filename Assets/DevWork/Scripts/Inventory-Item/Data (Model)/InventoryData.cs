using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

public enum InventoryType
{
    Inventory,
    Accessory,
    Pickaxe,
    CraftingStation,
    CraftingOut,
    TrashCan,
    Split
}

[CreateAssetMenu(fileName = "NewInventoryData", menuName = "Inventory/InventoryData")]
public class InventoryData : ScriptableObject
{
    [SerializeField] private int size = 20;
    [SerializeField] private Item nullItem;
    public Item NullItem => nullItem;
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

    public bool AddItem(InventoryItem newItem)
    {
        RollPrefixIfNeeded(newItem);
        return TryStackItem(newItem) || TryPlaceInEmptySlot(newItem);
    }

    private bool TryStackItem(InventoryItem item)
    {
        foreach (var existing in Items)
        {
            if (existing == null) continue;
            if (existing.itemData == null || existing.itemData.Type == ItemType.None) continue;
            if (existing.CanStackWith(item))
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
            if (Items[i] == null ||
                Items[i].itemData == null ||
                Items[i].itemData.Type == ItemType.None ||
                Items[i].itemData == nullItem)
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
    private void RollPrefixIfNeeded(InventoryItem it)
    {
        if (it == null) return;

        var item = it.itemData;
        if (item == null || item.Type == ItemType.None) return;

        // ONLY these two item types get prefixes
        bool allowPrefix = item.Type == ItemType.Pickaxe || item.Type == ItemType.Accessory;
        if (!allowPrefix) return;

        // roll once
        if (it.prefix == ItemPrefix.None)
            it.prefix = ItemPrefixConfig.GetRandomFor(item);
    }
    public void SortAndRepack_UsingAddItem()
    {
        if (inventoryType != InventoryType.Inventory) return; // only sort main inventory

        bool IsEmpty(InventoryItem it) =>
            it == null || it.itemData == null || it.itemData == nullItem ||
            it.itemData.Type == ItemType.None || it.quantity.Value <= 0;

        // 1) Snapshot non-empty items
        var nonEmpty = Items.Where(it => !IsEmpty(it)).ToList();

        // 2) Group by stack key (assumes CanStackWith primarily checks itemData).
        // If your CanStackWith has more rules, replace key with a composite key matching those rules.
        var groups = nonEmpty
            .GroupBy(it => it.itemData)
            .Select(g => new
            {
                Item = g.Key,
                TotalQty = g.Sum(x => Mathf.Max(0, x.quantity.Value))
            })
            .OrderBy(g => (int)g.Item.Type)                                   // Type first
            .ThenBy(g => g.Item.name, StringComparer.OrdinalIgnoreCase)       // then Name
            .ToList();

        // 3) Clear to true empties (pad with your None item)
        for (int i = 0; i < Items.Count; i++)
            Items[i] = new InventoryItem(nullItem, 0);

        // 4) Re-add in order using your existing AddItem (stacks then places)
        foreach (var g in groups)
        {
            int max = (g.Item.MaxStack <= 0) ? int.MaxValue : g.Item.MaxStack;
            int remaining = g.TotalQty;

            while (remaining > 0)
            {
                int take = Mathf.Min(remaining, max);
                var stack = new InventoryItem(g.Item, take);
                // AddItem will first try stacking (no-op on empty), then place into first empty slot
                bool ok = AddItem(stack);
                if (!ok)
                {
                    // Inventory full (shouldn't happen because we cleared), but guard anyway
                    Debug.LogWarning($"SortAndRepack: Not enough slots for {g.Item.name} (left {remaining}).");
                    break;
                }
                remaining -= take;
            }
        }
    }



    public bool RemoveItemAt(int index, bool isPlayerAction = false)
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
        if (item == null || item.itemData == nullItem || item.quantity.Value < amount) return false;
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
