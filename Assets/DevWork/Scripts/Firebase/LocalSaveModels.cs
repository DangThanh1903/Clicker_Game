using System;
using System.Collections.Generic;

[Serializable]
public class LocalSaveData
{
    public LocalGameplayData gameplay = new LocalGameplayData();
    public LocalProfileData profile = new LocalProfileData();
    public List<LocalInventorySave> inventories = new List<LocalInventorySave>();
    public int saveVersion;
    public long savedAtUtcTicks;
}

[Serializable]
public class LocalProfileData
{
    public string displayName;
    public string avatarId;

    public LocalProfileData() { }

    public LocalProfileData(UserProfileData src)
    {
        if (src == null) return;
        displayName = src.displayName;
        avatarId = src.avatarId;
    }

    public UserProfileData ToProfileSaveData()
    {
        return new UserProfileData
        {
            displayName = displayName,
            avatarId = avatarId
        };
    }
}

[Serializable]
public class LocalGameplayData
{
    public string currentBlock;
    public string currentLocation;
    public string peakLocation;
    public int mergeProgress;
    public float clicks;
    public float diamonds;
    public float currentTime;
    public float totalPlaytime;
    public List<LocalBiomeCraftNodeState> craftNodeStatesByBiome;
    public List<LocalBiomeMilestoneClaimState> milestoneClaimsByBiome;

    public LocalGameplayData() { }

    public LocalGameplayData(GameplaySaveData src)
    {
        if (src == null) return;
        currentBlock = src.currentBlock;
        currentLocation = src.currentLocation;
        peakLocation = src.peakLocation;
        mergeProgress = src.mergeProgress;
        clicks = src.clicks;
        diamonds = src.diamonds;
        currentTime = src.currentTime;
        totalPlaytime = src.totalPlaytime;
        if (src.craftNodeStatesByBiome != null)
        {
            craftNodeStatesByBiome = new List<LocalBiomeCraftNodeState>(src.craftNodeStatesByBiome.Count);
            foreach (var state in src.craftNodeStatesByBiome)
            {
                if (state == null) continue;
                craftNodeStatesByBiome.Add(new LocalBiomeCraftNodeState
                {
                    biome = state.biome,
                    states = state.states != null ? new List<int>(state.states) : null
                });
            }
        }

        if (src.milestoneClaimsByBiome != null)
        {
            milestoneClaimsByBiome = new List<LocalBiomeMilestoneClaimState>(src.milestoneClaimsByBiome.Count);
            foreach (var claim in src.milestoneClaimsByBiome)
            {
                if (claim == null) continue;
                milestoneClaimsByBiome.Add(new LocalBiomeMilestoneClaimState
                {
                    biome = claim.biome,
                    milestoneIndex = claim.milestoneIndex
                });
            }
        }
    }

    public GameplaySaveData ToGameplaySaveData()
    {
        return new GameplaySaveData
        {
            currentBlock = currentBlock,
            currentLocation = currentLocation,
            peakLocation = peakLocation,
            mergeProgress = mergeProgress,
            clicks = clicks,
            diamonds = diamonds,
            currentTime = currentTime,
            totalPlaytime = totalPlaytime,
            craftNodeStatesByBiome = BuildBiomeCraftNodeStates(),
            milestoneClaimsByBiome = BuildBiomeMilestoneClaimStates()
        };
    }

    private List<BiomeCraftNodeState> BuildBiomeCraftNodeStates()
    {
        if (craftNodeStatesByBiome == null)
            return null;

        var result = new List<BiomeCraftNodeState>(craftNodeStatesByBiome.Count);
        foreach (var state in craftNodeStatesByBiome)
        {
            if (state == null) continue;
            result.Add(new BiomeCraftNodeState
            {
                biome = state.biome,
                states = state.states != null ? new List<int>(state.states) : null
            });
        }

        return result;
    }

    private List<BiomeMilestoneClaimState> BuildBiomeMilestoneClaimStates()
    {
        if (milestoneClaimsByBiome == null)
            return null;

        var result = new List<BiomeMilestoneClaimState>(milestoneClaimsByBiome.Count);
        foreach (var claim in milestoneClaimsByBiome)
        {
            if (claim == null) continue;
            result.Add(new BiomeMilestoneClaimState
            {
                biome = claim.biome,
                milestoneIndex = claim.milestoneIndex
            });
        }

        return result;
    }
}

[Serializable]
public class LocalBiomeCraftNodeState
{
    public string biome;
    public List<int> states;
}

[Serializable]
public class LocalBiomeMilestoneClaimState
{
    public string biome;
    public int milestoneIndex;
}

[Serializable]
public class LocalInventorySave
{
    public string inventoryType;
    public List<LocalInventoryItem> items = new List<LocalInventoryItem>();

    public InventorySaveData ToInventorySaveData()
    {
        var data = new InventorySaveData { items = new List<InventoryItemSave>() };
        foreach (var item in items)
        {
            data.items.Add(new InventoryItemSave
            {
                itemName = item.itemName,
                quantity = item.quantity
            });
        }
        return data;
    }
}

[Serializable]
public class LocalInventoryItem
{
    public string itemName;
    public int quantity;
}
