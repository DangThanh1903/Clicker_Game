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

    // =========================
    // BASIC LOOKUPS
    // =========================

    public BlockUVEntry GetByName(string name)
        => blocks.Find(b => b.blockName == name);

    public int GetAtlasIndex(string name)
        => GetByName(name)?.atlasIndex ?? -1;

    public int GetHealth(string name)
        => GetByName(name)?.health ?? 0;

    public float GetWeight(string name)
        => GetByName(name)?.weight ?? 0f;

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
        return blocks.Where(b =>
            (location == BlockSpawnLocation.Any ||
             b.locationCondition == BlockSpawnLocation.Any ||
             b.locationCondition == location) &&

            (b.timeStateCondition == TimeState.Any ||
             b.timeStateCondition == timeState) &&

            (b.normalWeatherCondition == NormalWeatherName.Any ||
             b.normalWeatherCondition == normalWeather) &&

            (b.specialWeatherCondition == SpecialWeatherName.Any ||
             b.specialWeatherCondition == specialWeather)
        ).ToList();
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

    public List<(Item item, int amount)> GetDroppedItemsByName(string name, float luck)
    {
        var block = GetByName(name);
        if (block == null)
            return new List<(Item item, int amount)>();

        return block.GetDroppedItems(luck);
    }

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

    [Header("Drop Settings")]
    public List<ItemDrop> drops = new();

    public List<(Item item, int amount)> GetDroppedItems(float luck)
    {
        List<(Item item, int amount)> droppedItems = new();

        foreach (var drop in drops)
        {
            float chance = drop.dropChance;

            if (luck > 0f && chance < 1f)
                chance = LuckMath.BoostChance(chance, luck);

            if (UnityEngine.Random.value <= chance)
            {
                int amount = UnityEngine.Random.Range(drop.minAmount, drop.maxAmount + 1);
                droppedItems.Add((drop.item, amount));
            }
        }

        return droppedItems;
    }

    public List<ItemDropResult> GetDropResults(float luck)
    {
        List<ItemDropResult> droppedItems = new();

        foreach (var drop in drops)
        {
            if (drop == null) continue;

            float chance = drop.dropChance;

            if (luck > 0f && chance < 1f)
                chance = LuckMath.BoostChance(chance, luck);

            if (UnityEngine.Random.value <= chance)
            {
                int amount = UnityEngine.Random.Range(drop.minAmount, drop.maxAmount + 1);
                droppedItems.Add(new ItemDropResult(drop, amount));
            }
        }

        return droppedItems;
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
