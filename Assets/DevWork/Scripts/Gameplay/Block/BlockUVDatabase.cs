using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum BlockSpawnLocation
{
    Any,
    Plain,
    Ice,
    Underground,
    SkyIsland,
    Desert,
    Cave,
    Ocean,
    Musshroom,
    Hell,
    Dungeon,
    Hallow
}


[CreateAssetMenu(fileName = "BlockUVDatabase", menuName = "Block/UV Database")]
public class BlockUVDatabase : ScriptableObject
{
    [Header("Block Entries")]
    public List<BlockUVEntry> blocks = new();
    private Dictionary<string, BlockUVEntry> blocksByName;

    // =========================
    // BASIC LOOKUPS
    // =========================

    public BlockUVEntry GetByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        EnsureCache();
        return blocksByName.TryGetValue(name, out var entry) ? entry : null;
    }

    public int GetAtlasIndex(string name)
        => GetByName(name)?.atlasIndex ?? -1;

    public int GetHealth(string name)
        => GetByName(name)?.health ?? 0;

    public float GetWeight(string name)
        => GetByName(name)?.weight ?? 0f;

    public Color GetOutlineColor(string name)
        => GetByName(name)?.outlineColor ?? Color.black;

    public float GetGlowIntensity(string name)
        => Mathf.Max(0f, GetByName(name)?.glowIntensity ?? 0f);

    public bool IsBlockValidForConditions(
        string name,
        BlockSpawnLocation location,
        TimeState timeState,
        NormalWeatherName normalWeather,
        SpecialWeatherName specialWeather
    )
    {
        BlockUVEntry entry = GetByName(name);
        return MatchesConditions(entry, location, timeState, normalWeather, specialWeather);
    }

    // =========================
    // FILTERING
    // =========================

    public List<BlockUVEntry> GetBlocksByConditions(
        BlockSpawnLocation location,
        TimeState timeState,
        NormalWeatherName normalWeather,
        SpecialWeatherName specialWeather
    )
    {
        return blocks.Where(b => MatchesConditions(b, location, timeState, normalWeather, specialWeather)).ToList();
    }

    // =========================
    // LUCK-AWARE BLOCK SPAWN
    // =========================

    public BlockUVEntry GetRandomBlockByConditions(
        BlockSpawnLocation location,
        TimeState timeState,
        NormalWeatherName normalWeather,
        SpecialWeatherName specialWeather,
        float luck
    )
    {
        var filtered = GetBlocksByConditions(location, timeState, normalWeather, specialWeather);

        if (filtered == null || filtered.Count == 0)
        {
            Debug.LogWarning($"[Block] No blocks match conditions: {location}, {timeState}, {normalWeather}, {specialWeather}");
            return null;
        }

        return GetBlockByRarity(filtered, luck);
    }

    private BlockUVEntry GetBlockByRarity(List<BlockUVEntry> entries, float luck)
    {
        if (entries == null || entries.Count == 0)
            return null;

        // Find min/max weights for rarity normalization
        float minW = float.MaxValue;
        float maxW = float.MinValue;

        foreach (var e in entries)
        {
            minW = Mathf.Min(minW, e.weight);
            maxW = Mathf.Max(maxW, e.weight);
        }

        float range = Mathf.Max(0.0001f, maxW - minW);

        float totalWeight = 0f;
        float[] boostedWeights = new float[entries.Count];

        for (int i = 0; i < entries.Count; i++)
        {
            float baseWeight = Mathf.Max(0f, entries[i].weight);

            // rarityScore: 0 = common, 1 = rare
            float rarityScore = 1f - ((baseWeight - minW) / range);

            float boosted = (luck > 0f)
                ? LuckMath.BoostWeightForRarity(baseWeight, rarityScore, luck)
                : baseWeight;

            boostedWeights[i] = boosted;
            totalWeight += boosted;
        }

        if (totalWeight <= 0f)
            return entries.Last();

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < entries.Count; i++)
        {
            cumulative += boostedWeights[i];
            if (roll <= cumulative)
                return entries[i];
        }

        return entries.Last();
    }

    // =========================
    // LUCK-AWARE DROPS
    // =========================

    public List<ItemDropResult> GetDropResultsByName(string name, float luck)
    {
        var block = GetByName(name);
        if (block == null)
            return new List<ItemDropResult>();

        return block.GetDropResults(luck);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (blocks == null) return;
        BuildCache();
        bool changed = false;
        foreach (var block in blocks)
        {
            if (block == null || block.drops == null) continue;
            foreach (var drop in block.drops)
            {
                if (drop != null && drop.SyncAddressFromItem())
                    changed = true;
            }
        }
        if (changed)
            EditorUtility.SetDirty(this);
    }
#endif

    private void OnEnable()
    {
        BuildCache();
    }

    private void EnsureCache()
    {
        if (blocksByName == null || blocksByName.Count != (blocks?.Count ?? 0))
            BuildCache();
    }

    private void BuildCache()
    {
        blocksByName = new Dictionary<string, BlockUVEntry>(blocks?.Count ?? 0);
        if (blocks == null) return;
        foreach (var block in blocks)
        {
            if (block == null || string.IsNullOrEmpty(block.blockName)) continue;
            if (!blocksByName.ContainsKey(block.blockName))
                blocksByName[block.blockName] = block;
        }
    }

    private static bool MatchesConditions(
        BlockUVEntry entry,
        BlockSpawnLocation location,
        TimeState timeState,
        NormalWeatherName normalWeather,
        SpecialWeatherName specialWeather)
    {
        if (entry == null)
            return false;

        return (location == BlockSpawnLocation.Any ||
                entry.locationCondition == BlockSpawnLocation.Any ||
                entry.locationCondition == location) &&
               (entry.timeStateCondition == TimeState.Any ||
                entry.timeStateCondition == timeState) &&
               (entry.normalWeatherCondition == NormalWeatherName.Any ||
                entry.normalWeatherCondition == normalWeather) &&
               (entry.specialWeatherCondition == SpecialWeatherName.Any ||
                entry.specialWeatherCondition == specialWeather);
    }
}


[Serializable]
public class BlockUVEntry
{
    public string blockName;
    public int atlasIndex;
    public int health;
    public string BreakingSound;

    [Header("Spawn Settings")]
    public BlockSpawnLocation locationCondition;
    public TimeState timeStateCondition;
    public NormalWeatherName normalWeatherCondition;
    public SpecialWeatherName specialWeatherCondition;
    public float weight = 0.5f;

    [Header("Visual")]
    public Color outlineColor = Color.black;
    public float glowIntensity = 0f;

    [Header("Drop Settings")]
    public List<ItemDrop> drops = new();

    public List<ItemDropResult> GetDropResults(float luck)
    {
        return DropRollService.RollDropResults(drops, luck);
    }
}


[Serializable]
public class ItemDrop
{
    public Item item;
    [SerializeField] private string itemAddress;
    public int minAmount = 1;
    public int maxAmount = 1;
    public float dropChance = 1f;
    [Tooltip("If true, only visible in discovery list after it drops.")]
    public bool isSecret;

    public string GetItemAddress()
    {
        if (!string.IsNullOrEmpty(itemAddress)) return itemAddress;
        if (item == null) return string.Empty;
        return string.IsNullOrEmpty(item.itemName) ? item.name : item.itemName;
    }

#if UNITY_EDITOR
    public bool SyncAddressFromItem()
    {
        if (item == null) return false;
        if (!string.IsNullOrEmpty(itemAddress)) return false;
        itemAddress = string.IsNullOrEmpty(item.itemName) ? item.name : item.itemName;
        return true;
    }
#endif
}

public struct ItemDropResult
{
    public ItemDrop drop;
    public int amount;

    public ItemDropResult(ItemDrop drop, int amount)
    {
        this.drop = drop;
        this.amount = amount;
    }
}
