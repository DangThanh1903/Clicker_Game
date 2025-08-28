using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    public BlockUVEntry GetByName(string name)
    {
        return blocks.Find(b => b.blockName == name);
    }

    public int GetAtlasIndex(string name) =>
        blocks.Find(b => b.blockName == name)?.atlasIndex ?? -1;

    public int GetHealth(string name) =>
        blocks.Find(b => b.blockName == name)?.health ?? 0;

    public float GetWeight(string name) =>
        blocks.Find(b => b.blockName == name)?.weight ?? 0;
    BlockUVEntry GetBlockByRarity(List<BlockUVEntry> entries)
    {
        float totalWeight = entries.Sum(e => e.weight);
        if (totalWeight <= 0) return null;

        float rand = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var entry in entries)
        {
            cumulative += entry.weight;
            if (rand <= cumulative)
                return entry;
        }

        return entries.Last();
    }

    public List<(Item item, int amount)> GetDroppedItemsByName(string name)
    {
        var block = GetByName(name);
        if (block == null)
            return new List<(Item item, int amount)>();

        return block.GetDroppedItems();
    }

    public List<BlockUVEntry> GetBlocksByConditions(
        BlockSpawnLocation location,
        TimeState timeState,
        NormalWeatherName normalWeather,
        SpecialWeatherName specialWeather
    )
    {
        return blocks.Where(b =>
            (location == BlockSpawnLocation.Any || b.locationCondition == BlockSpawnLocation.Any || b.locationCondition == location) &&
            (b.timeStateCondition == TimeState.Any || b.timeStateCondition == timeState) &&
            (b.normalWeatherCondition == NormalWeatherName.Any || b.normalWeatherCondition == normalWeather) &&
            (b.specialWeatherCondition == SpecialWeatherName.Any || b.specialWeatherCondition == specialWeather)
        ).ToList();
    }


    public BlockUVEntry GetRandomBlockByConditions(
        BlockSpawnLocation location,
        TimeState timeState,
        NormalWeatherName normalWeather,
        SpecialWeatherName specialWeather
    )
    {
        var filtered = GetBlocksByConditions(location, timeState, normalWeather, specialWeather);

        if (filtered == null)
        {
            Debug.LogWarning($"[Block] GetBlocksByConditions returned NULL. " +
                            $"Location: {location}, Time: {timeState}, NormalWeather: {normalWeather}, SpecialWeather: {specialWeather}");
            return null;
        }

        if (filtered.Count == 0)
        {
            Debug.LogWarning($"[Block] No blocks match the condition. " +
                            $"Location: {location}, Time: {timeState}, NormalWeather: {normalWeather}, SpecialWeather: {specialWeather}");
            return null;
        }

        return GetBlockByRarity(filtered);
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

    [Header("Drop Settings")]
    public List<ItemDrop> drops = new();

    public List<(Item item, int amount)> GetDroppedItems()
    {
        List<(Item item, int amount)> droppedItems = new();

        foreach (var drop in drops)
        {
            float roll = UnityEngine.Random.value; // 0 to 1
            if (roll <= drop.dropChance)
            {
                int amount = UnityEngine.Random.Range(drop.minAmount, drop.maxAmount + 1);
                droppedItems.Add((drop.item, amount));
            }
        }

        return droppedItems;
    }
}

[Serializable]
public class ItemDrop
{
    public Item item;
    public int minAmount = 1;
    public int maxAmount = 1;
    public float dropChance = 1f;
}
