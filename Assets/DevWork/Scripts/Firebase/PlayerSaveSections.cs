using System.Collections.Generic;
using UnityEngine;

public static class LifetimeStatsSection
{
    public static void Write(GameplaySaveData target, LifetimeStats source)
    {
        if (target == null || source == null)
            return;

        target.clicks = source.clicks;
        target.diamonds = source.diamonds;
        target.totalBlockBreaked = source.totalBlockBreaked;
        target.totalDamageDealed = source.totalDamageDealed;
        target.totalTimePlayed = source.totalTimePlayed;
        target.totalPlaytime = source.totalTimePlayed;
    }

    public static LifetimeStats Read(GameplaySaveData source)
    {
        var stats = new LifetimeStats();
        if (source == null)
            return stats;

        stats.Set(
            source.clicks,
            source.diamonds,
            source.totalBlockBreaked,
            source.totalDamageDealed,
            Mathf.Max(source.totalTimePlayed, source.totalPlaytime));
        return stats;
    }
}

public static class InventorySaveSection
{
    public static List<LocalInventorySave> Build(IReadOnlyList<InventoryData> inventories)
    {
        var result = new List<LocalInventorySave>();
        if (inventories == null)
            return result;

        foreach (var inv in inventories)
        {
            if (inv == null)
                continue;

            var invSave = new LocalInventorySave
            {
                inventoryType = inv.inventoryType.ToString()
            };

            foreach (var invItem in inv.Items)
            {
                var item = invItem?.itemData != null ? invItem.itemData : inv.NullItem;
                invSave.items.Add(new LocalInventoryItem
                {
                    itemName = item != null ? item.name : string.Empty,
                    quantity = invItem?.quantity?.Value ?? 0
                });
            }

            result.Add(invSave);
        }

        return result;
    }
}

public static class CraftSection
{
    public static void Write(GameplaySaveData target, List<BiomeCraftNodeState> scopedStates, List<int> currentScopeStates)
    {
        if (target == null)
            return;

        target.craftNodeStatesByBiome = scopedStates;
        target.craftNodeStates = currentScopeStates;
    }

    public static List<BiomeCraftNodeState> ReadScopedStates(GameplaySaveData source)
    {
        return source?.craftNodeStatesByBiome;
    }

    public static List<int> ReadLegacyScopeStates(GameplaySaveData source)
    {
        return source?.craftNodeStates;
    }
}

public static class BiomeProgressSection
{
    public static void Write(
        GameplaySaveData target,
        List<BiomeEssenceEarnedState> essenceStates,
        List<BiomeProgressClaimState> claimStates)
    {
        if (target == null)
            return;

        target.biomeEssenceEarned = essenceStates;
        target.biomeProgressClaims = claimStates;
    }

    public static List<BiomeEssenceEarnedState> ReadEssence(GameplaySaveData source)
    {
        return source?.biomeEssenceEarned;
    }

    public static List<BiomeProgressClaimState> ReadClaims(GameplaySaveData source)
    {
        return source?.biomeProgressClaims;
    }
}
