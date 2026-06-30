using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public readonly struct DropGrantEntry
{
    public readonly Item Item;
    public readonly int Amount;

    public DropGrantEntry(Item item, int amount)
    {
        Item = item;
        Amount = amount;
    }
}

public static class DropGrantService
{
    public static bool TryGrantRolledDrops(
        IReadOnlyList<(Item item, int amount)> drops,
        out string grantedSummary,
        Action<Item, int> onItemGranted = null,
        string logContext = "[DropGrant]")
    {
        grantedSummary = string.Empty;
        if (drops == null || drops.Count == 0)
            return false;

        var grantEntries = new List<DropGrantEntry>(drops.Count);
        for (int i = 0; i < drops.Count; i++)
        {
            (Item item, int amount) result = drops[i];
            if (result.item == null || result.item.Type == ItemType.None)
                continue;

            int safeAmount = Mathf.Max(0, result.amount);
            if (safeAmount <= 0)
                continue;

            grantEntries.Add(new DropGrantEntry(result.item, safeAmount));
        }

        return TryGrantDrops(grantEntries, out grantedSummary, onItemGranted, logContext);
    }

    public static IEnumerator ResolveItemFromDrop_Co(ItemDrop drop, string sourceContext, Action<Item> onResolved)
    {
        if (drop == null)
        {
            onResolved?.Invoke(null);
            yield break;
        }

        if (drop.item != null)
        {
            onResolved?.Invoke(drop.item);
            yield break;
        }

        string address = drop.GetItemAddress();
        if (string.IsNullOrEmpty(address))
        {
            Debug.LogWarning($"[DropGrant] Missing item address for {sourceContext}.");
            onResolved?.Invoke(null);
            yield break;
        }

        AsyncOperationHandle<Item> handle = Addressables.LoadAssetAsync<Item>(address);
        yield return handle;

        Item item = null;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            item = handle.Result;
        }
        else
        {
            Debug.LogWarning($"[DropGrant] Failed to load item address '{address}' for {sourceContext}. Status={handle.Status}");
        }

        Addressables.Release(handle);
        onResolved?.Invoke(item);
    }

    public static bool TryGrantDrops(
        IEnumerable<DropGrantEntry> drops,
        out string grantedSummary,
        Action<Item, int> onItemGranted = null,
        string logContext = "[DropGrant]")
    {
        grantedSummary = string.Empty;
        if (drops == null)
            return false;

        var inventory = InventoryController.Instance;
        if (inventory == null)
        {
            Debug.LogWarning($"{logContext} InventoryController.Instance is null, cannot add drops.");
            return false;
        }

        bool hasGrantedAny = false;
        var summaryBuilder = new StringBuilder(64);

        foreach (var entry in drops)
        {
            Item item = entry.Item;
            if (item == null || item.Type == ItemType.None)
                continue;

            int requested = Mathf.Max(0, entry.Amount);
            if (requested <= 0)
                continue;

            var toAdd = new InventoryItem(item, requested);
            _ = inventory.TryAddItemToInventory(toAdd);
            int remaining = toAdd.quantity != null ? Mathf.Max(0, toAdd.quantity.Value) : 0;
            int added = Mathf.Max(0, requested - remaining);
            if (added <= 0)
                continue;

            hasGrantedAny = true;
            QuestSignals.CollectItem(item.itemName, added);

            Toaster.ShowPickupItems(item.icon, added);
            BiomeProgressionService.NotifyItemEarned(item, added);

            if (summaryBuilder.Length > 0)
                summaryBuilder.Append(", ");
            summaryBuilder.Append(added);
            summaryBuilder.Append(' ');
            summaryBuilder.Append(item.itemName);

            onItemGranted?.Invoke(item, added);
        }

        grantedSummary = summaryBuilder.ToString();
        return hasGrantedAny;
    }
}
