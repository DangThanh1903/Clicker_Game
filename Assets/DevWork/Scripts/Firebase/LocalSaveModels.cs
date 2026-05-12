using System;
using System.Collections.Generic;

[Serializable]
public class LocalSaveData
{
    public LocalGameplayData gameplay = new LocalGameplayData();
    public LocalProfileData profile = new LocalProfileData();
    public List<LocalInventorySave> inventories = new List<LocalInventorySave>();
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
    public float clicks;
    public float diamonds;
    public float currentTime;
    public float totalPlaytime;
    public List<LocalBiomeCraftNodeState> craftNodeStatesByBiome;
    public List<int> craftNodeStates;
    public List<LocalBiomeEssenceEarnedState> biomeEssenceEarned;
    public List<LocalBiomeProgressClaimState> biomeProgressClaims;

    public LocalGameplayData() { }

    public LocalGameplayData(GameplaySaveData src)
    {
        if (src == null) return;
        currentBlock = src.currentBlock;
        currentLocation = src.currentLocation;
        peakLocation = src.peakLocation;
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
        craftNodeStates = src.craftNodeStates != null ? new List<int>(src.craftNodeStates) : null;

        if (src.biomeEssenceEarned != null)
        {
            biomeEssenceEarned = new List<LocalBiomeEssenceEarnedState>(src.biomeEssenceEarned.Count);
            foreach (var state in src.biomeEssenceEarned)
            {
                if (state == null) continue;
                biomeEssenceEarned.Add(new LocalBiomeEssenceEarnedState
                {
                    biome = state.biome,
                    amount = state.amount
                });
            }
        }

        if (src.biomeProgressClaims != null)
        {
            biomeProgressClaims = new List<LocalBiomeProgressClaimState>(src.biomeProgressClaims.Count);
            foreach (var state in src.biomeProgressClaims)
            {
                if (state == null) continue;
                biomeProgressClaims.Add(new LocalBiomeProgressClaimState
                {
                    biome = state.biome,
                    claimedLevel = state.claimedLevel
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
            clicks = clicks,
            diamonds = diamonds,
            currentTime = currentTime,
            totalPlaytime = totalPlaytime,
            craftNodeStatesByBiome = BuildBiomeCraftNodeStates(),
            craftNodeStates = craftNodeStates != null ? new List<int>(craftNodeStates) : null,
            biomeEssenceEarned = BuildBiomeEssenceEarnedStates(),
            biomeProgressClaims = BuildBiomeProgressClaimStates()
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

    private List<BiomeEssenceEarnedState> BuildBiomeEssenceEarnedStates()
    {
        if (biomeEssenceEarned == null)
            return null;

        var result = new List<BiomeEssenceEarnedState>(biomeEssenceEarned.Count);
        foreach (var state in biomeEssenceEarned)
        {
            if (state == null) continue;
            result.Add(new BiomeEssenceEarnedState
            {
                biome = state.biome,
                amount = state.amount
            });
        }

        return result;
    }

    private List<BiomeProgressClaimState> BuildBiomeProgressClaimStates()
    {
        if (biomeProgressClaims == null)
            return null;

        var result = new List<BiomeProgressClaimState>(biomeProgressClaims.Count);
        foreach (var state in biomeProgressClaims)
        {
            if (state == null) continue;
            result.Add(new BiomeProgressClaimState
            {
                biome = state.biome,
                claimedLevel = state.claimedLevel
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
public class LocalBiomeEssenceEarnedState
{
    public string biome;
    public int amount;
}

[Serializable]
public class LocalBiomeProgressClaimState
{
    public string biome;
    public int claimedLevel;
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
