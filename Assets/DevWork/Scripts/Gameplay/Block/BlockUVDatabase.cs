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
    private Dictionary<string, ItemDrop> dropTemplatesByAddress;

    [Header("Biome Progression (New)")]
    [SerializeField] private BiomeProgressionDatabaseSO biomeProgressionDatabase;
    public BiomeProgressionDatabaseSO BiomeProgressionDatabase => biomeProgressionDatabase;

    [Header("Merge Progression (Plain Biome)")]
    [SerializeField] private bool usePlainMergeProgression = true;
    [SerializeField, Min(0)] private int grassUnlockProgress = 1;
    [SerializeField, Min(0)] private int clayUnlockProgress = 3;
    [SerializeField, Range(0f, 1f)] private float stage1GrassSpawnChance = 0.2f;
    [SerializeField, Range(0f, 1f)] private float stage3ClaySpawnChance = 0.95f;
    [SerializeField, Range(0f, 1f)] private float stage1GrassDropChance = 0.2f;
    [SerializeField, Range(0f, 1f)] private float stage3ClayDropChance = 0.95f;
    [SerializeField, Range(0f, 1f)] private float stage3FlintDropChance = 0.05f;
    [SerializeField] private string dirtBlockName = "Dirt";
    [SerializeField] private string grassBlockName = "Grass";
    [SerializeField] private string clayBlockName = "Clay";
    [SerializeField] private string dirtDropAddress = "Dirt";
    [SerializeField] private string grassDropAddress = "Grass";
    [SerializeField] private string clayDropAddress = "Clay";
    [SerializeField] private string flintDropAddress = "Flint";
    [SerializeField, Min(1)] private int progressionDirtAmount = 1;
    [SerializeField, Min(1)] private int progressionGrassAmount = 1;
    [SerializeField, Min(1)] private int progressionClayAmount = 1;
    [SerializeField, Min(1)] private int progressionFlintAmount = 1;

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
        float luck,
        int mergeProgress = 0
    )
    {
        var filtered = GetBlocksByConditions(location, timeState, normalWeather, specialWeather);

        if (filtered == null || filtered.Count == 0)
        {
            Debug.LogWarning($"[Block] No blocks match conditions: {location}, {timeState}, {normalWeather}, {specialWeather}");
            return null;
        }

        if (TryGetProgressionBlockFromDatabase(location, filtered, Mathf.Max(0, mergeProgress), out BlockUVEntry progressionFromDatabase))
            return progressionFromDatabase;

        if (TryGetProgressionBlockForPlain(location, filtered, Mathf.Max(0, mergeProgress), out BlockUVEntry progressionBlock))
            return progressionBlock;

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

    public List<ItemDropResult> GetDropResultsByName(
        string name,
        float luck,
        int mergeProgress = 0,
        BlockSpawnLocation location = BlockSpawnLocation.Any)
    {
        if (TryGetProgressionDropsFromDatabase(
                location,
                name,
                luck,
                Mathf.Max(0, mergeProgress),
                out List<ItemDropResult> progressionFromDatabase))
        {
            return progressionFromDatabase;
        }

        if (TryGetProgressionDropsForPlain(name, luck, Mathf.Max(0, mergeProgress), out List<ItemDropResult> progressionDrops))
            return progressionDrops;

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
        dropTemplatesByAddress = new Dictionary<string, ItemDrop>(StringComparer.OrdinalIgnoreCase);
        if (blocks == null) return;

        foreach (var block in blocks)
        {
            if (block == null || string.IsNullOrEmpty(block.blockName)) continue;
            if (!blocksByName.ContainsKey(block.blockName))
                blocksByName[block.blockName] = block;

            if (block.drops == null)
                continue;

            foreach (var drop in block.drops)
            {
                if (drop == null)
                    continue;

                string address = drop.GetItemAddress();
                if (string.IsNullOrWhiteSpace(address))
                    continue;

                if (!dropTemplatesByAddress.ContainsKey(address))
                    dropTemplatesByAddress[address] = drop;
            }
        }
    }

    private bool TryGetProgressionBlockForPlain(
        BlockSpawnLocation location,
        List<BlockUVEntry> filtered,
        int mergeProgress,
        out BlockUVEntry selectedBlock)
    {
        selectedBlock = null;

        if (!usePlainMergeProgression || location != BlockSpawnLocation.Plain || filtered == null || filtered.Count == 0)
            return false;

        if (mergeProgress < grassUnlockProgress)
        {
            selectedBlock = FindEntryByName(filtered, dirtBlockName);
            return selectedBlock != null;
        }

        if (mergeProgress < clayUnlockProgress)
        {
            bool spawnGrass = UnityEngine.Random.value < Mathf.Clamp01(stage1GrassSpawnChance);
            selectedBlock = spawnGrass
                ? FindEntryByName(filtered, grassBlockName)
                : FindEntryByName(filtered, dirtBlockName);

            if (selectedBlock == null)
                selectedBlock = FindEntryByName(filtered, dirtBlockName) ?? FindEntryByName(filtered, grassBlockName);

            return selectedBlock != null;
        }

        bool spawnClay = UnityEngine.Random.value < Mathf.Clamp01(stage3ClaySpawnChance);
        selectedBlock = spawnClay
            ? FindEntryByName(filtered, clayBlockName)
            : FindEntryByName(filtered, grassBlockName);

        if (selectedBlock == null)
            selectedBlock = FindEntryByName(filtered, clayBlockName)
                            ?? FindEntryByName(filtered, grassBlockName)
                            ?? FindEntryByName(filtered, dirtBlockName);

        return selectedBlock != null;
    }

    private bool TryGetProgressionBlockFromDatabase(
        BlockSpawnLocation location,
        List<BlockUVEntry> filtered,
        int mergeProgress,
        out BlockUVEntry selectedBlock)
    {
        selectedBlock = null;

        if (biomeProgressionDatabase == null)
            return false;
        if (location == BlockSpawnLocation.Any)
            return false;

        return BiomeProgressionService.TrySelectSpawnBlock(
                   biomeProgressionDatabase,
                   location,
                   mergeProgress,
                   filtered,
                   out selectedBlock)
               && selectedBlock != null;
    }

    private bool TryGetProgressionDropsFromDatabase(
        BlockSpawnLocation location,
        string blockName,
        float luck,
        int mergeProgress,
        out List<ItemDropResult> drops)
    {
        drops = null;

        if (biomeProgressionDatabase == null)
            return false;
        if (location == BlockSpawnLocation.Any)
            return false;
        if (string.IsNullOrWhiteSpace(blockName))
            return false;
        if (!BiomeProgressionService.TryGetActiveMilestone(
                biomeProgressionDatabase,
                location,
                mergeProgress,
                out BiomeMilestone milestone,
                out _))
        {
            return false;
        }

        if (milestone == null || milestone.ProgressionDrops == null || milestone.ProgressionDrops.Count == 0)
            return false;
        if (!IsBlockInSpawnTable(milestone, blockName))
            return false;

        drops = new List<ItemDropResult>(milestone.ProgressionDrops.Count);
        RollMilestoneDrops(milestone, luck, drops);
        return true;
    }

    private void RollMilestoneDrops(BiomeMilestone milestone, float luck, List<ItemDropResult> drops)
    {
        if (milestone == null || drops == null)
            return;

        switch (milestone.DropRollMode)
        {
            case BiomeProgressionDropRollMode.SinglePickByWeight:
                RollMilestoneDropsSinglePick(milestone, luck, drops);
                break;
            default:
                RollMilestoneDropsIndependent(milestone, luck, drops);
                break;
        }
    }

    private void RollMilestoneDropsIndependent(BiomeMilestone milestone, float luck, List<ItemDropResult> drops)
    {
        IReadOnlyList<BiomeProgressionDropEntry> entries = milestone.ProgressionDrops;
        if (entries == null || entries.Count == 0)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            BiomeProgressionDropEntry entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.ItemAddress))
                continue;

            TryRollTemplateDrop(
                entry.ItemAddress,
                entry.Chance,
                luck,
                drops,
                fixedAmount: -1,
                minAmountOverride: entry.MinAmount,
                maxAmountOverride: entry.MaxAmount);
        }
    }

    private void RollMilestoneDropsSinglePick(BiomeMilestone milestone, float luck, List<ItemDropResult> drops)
    {
        IReadOnlyList<BiomeProgressionDropEntry> entries = milestone.ProgressionDrops;
        if (entries == null || entries.Count == 0)
            return;

        float totalWeight = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            BiomeProgressionDropEntry entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.ItemAddress))
                continue;
            if (entry.Weight <= 0f)
                continue;

            totalWeight += entry.Weight;
        }

        if (totalWeight <= 0f)
            return;

        float roll = UnityEngine.Random.value * totalWeight;
        float cumulative = 0f;

        for (int i = 0; i < entries.Count; i++)
        {
            BiomeProgressionDropEntry entry = entries[i];
            if (string.IsNullOrWhiteSpace(entry.ItemAddress))
                continue;
            if (entry.Weight <= 0f)
                continue;

            cumulative += entry.Weight;
            if (roll > cumulative)
                continue;

            TryRollTemplateDrop(
                entry.ItemAddress,
                entry.Chance,
                luck,
                drops,
                fixedAmount: -1,
                minAmountOverride: entry.MinAmount,
                maxAmountOverride: entry.MaxAmount);
            return;
        }
    }

    private static bool IsBlockInSpawnTable(BiomeMilestone milestone, string blockName)
    {
        if (milestone == null || string.IsNullOrWhiteSpace(blockName))
            return false;

        IReadOnlyList<BiomeSpawnWeightEntry> spawnTable = milestone.SpawnTable;
        if (spawnTable == null || spawnTable.Count == 0)
            return false;

        for (int i = 0; i < spawnTable.Count; i++)
        {
            BiomeSpawnWeightEntry row = spawnTable[i];
            if (string.IsNullOrWhiteSpace(row.BlockName))
                continue;
            if (string.Equals(row.BlockName, blockName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private bool TryGetProgressionDropsForPlain(
        string blockName,
        float luck,
        int mergeProgress,
        out List<ItemDropResult> drops)
    {
        drops = null;

        if (!usePlainMergeProgression || string.IsNullOrWhiteSpace(blockName))
            return false;

        bool progressionBlock =
            string.Equals(blockName, dirtBlockName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(blockName, grassBlockName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(blockName, clayBlockName, StringComparison.OrdinalIgnoreCase);

        if (!progressionBlock)
            return false;

        drops = new List<ItemDropResult>(2);

        if (mergeProgress < grassUnlockProgress)
        {
            TryRollTemplateDrop(dirtDropAddress, 1f, luck, drops, progressionDirtAmount);
            return true;
        }

        if (mergeProgress < clayUnlockProgress)
        {
            TryRollTemplateDrop(dirtDropAddress, 1f, luck, drops, progressionDirtAmount);
            TryRollTemplateDrop(grassDropAddress, stage1GrassDropChance, luck, drops, progressionGrassAmount);
            return true;
        }

        RollStageThreeDropsExclusive(luck, drops);
        return true;
    }

    private void RollStageThreeDropsExclusive(float luck, List<ItemDropResult> drops)
    {
        if (drops == null)
            return;

        float clayWeight = Mathf.Max(0f, stage3ClayDropChance);
        float flintWeight = Mathf.Max(0f, stage3FlintDropChance);
        float totalWeight = clayWeight + flintWeight;

        if (totalWeight <= 0f)
        {
            TryRollTemplateDrop(clayDropAddress, 1f, luck, drops, progressionClayAmount);
            return;
        }

        float roll = UnityEngine.Random.value * totalWeight;
        bool dropped = roll < clayWeight
            ? TryRollTemplateDrop(clayDropAddress, 1f, luck, drops, progressionClayAmount)
            : TryRollTemplateDrop(flintDropAddress, 1f, luck, drops, progressionFlintAmount);

        if (!dropped)
        {
            if (!TryRollTemplateDrop(clayDropAddress, 1f, luck, drops, progressionClayAmount))
                TryRollTemplateDrop(flintDropAddress, 1f, luck, drops, progressionFlintAmount);
        }
    }

    private bool TryRollTemplateDrop(
        string itemAddress,
        float chance,
        float luck,
        List<ItemDropResult> drops,
        int fixedAmount = -1,
        int minAmountOverride = -1,
        int maxAmountOverride = -1)
    {
        if (drops == null || string.IsNullOrWhiteSpace(itemAddress))
            return false;

        if (!TryGetDropTemplate(itemAddress, out ItemDrop template) || template == null)
            return false;

        float finalChance = Mathf.Clamp01(chance);
        if (finalChance <= 0f)
            return false;

        if (luck > 0f && finalChance < 1f)
            finalChance = LuckMath.BoostChance(finalChance, luck);

        if (UnityEngine.Random.value > finalChance)
            return false;

        int amount;
        if (fixedAmount > 0)
        {
            amount = fixedAmount;
        }
        else
        {
            int minAmount = minAmountOverride > 0 ? minAmountOverride : template.minAmount;
            int rawMaxAmount = maxAmountOverride > 0 ? maxAmountOverride : template.maxAmount;
            int maxAmount = Mathf.Max(minAmount, rawMaxAmount);
            amount = UnityEngine.Random.Range(minAmount, maxAmount + 1);
        }
        amount = ApplyDropMultiplier(amount);
        if (amount <= 0)
            return false;

        drops.Add(new ItemDropResult(template, amount));
        return true;
    }

    private bool TryGetDropTemplate(string itemAddress, out ItemDrop template)
    {
        template = null;
        EnsureCache();
        if (dropTemplatesByAddress == null || string.IsNullOrWhiteSpace(itemAddress))
            return false;

        return dropTemplatesByAddress.TryGetValue(itemAddress, out template) && template != null;
    }

    private static BlockUVEntry FindEntryByName(IReadOnlyList<BlockUVEntry> entries, string blockName)
    {
        if (entries == null || string.IsNullOrWhiteSpace(blockName))
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            BlockUVEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.blockName))
                continue;

            if (string.Equals(entry.blockName, blockName, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    private static int ApplyDropMultiplier(int amount)
    {
        if (amount <= 0)
            return 0;

        float dropMultiplier = StatsManager.Ins != null
            ? StatsManager.Ins.Get(StatType.DropMultiplier)
            : 1f;
        if (dropMultiplier <= 0f)
            dropMultiplier = 1f;

        return Mathf.Max(0, Mathf.RoundToInt(amount * dropMultiplier));
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
