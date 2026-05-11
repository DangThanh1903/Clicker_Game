using System;
using System.Collections.Generic;
using UnityEngine;

public static class BiomeProgressionService
{
    public static bool TryGetActiveMilestone(
        BiomeProgressionDatabaseSO database,
        BlockSpawnLocation biome,
        int mergeProgress,
        out BiomeMilestone milestone,
        out int milestoneIndex)
    {
        milestone = null;
        milestoneIndex = -1;

        if (database == null)
            return false;

        return database.TryGetMilestone(biome, Mathf.Max(0, mergeProgress), out milestone, out milestoneIndex)
               && milestone != null;
    }

    public static bool TrySelectSpawnBlock(
        BiomeProgressionDatabaseSO database,
        BlockSpawnLocation biome,
        int mergeProgress,
        IReadOnlyList<BlockUVEntry> filteredEntries,
        out BlockUVEntry selectedBlock)
    {
        selectedBlock = null;

        if (!TryGetActiveMilestone(database, biome, mergeProgress, out BiomeMilestone milestone, out _))
            return false;

        IReadOnlyList<BiomeSpawnWeightEntry> spawnTable = milestone.SpawnTable;
        if (spawnTable == null || spawnTable.Count == 0)
            return false;
        if (filteredEntries == null || filteredEntries.Count == 0)
            return false;

        float totalWeight = 0f;
        for (int i = 0; i < spawnTable.Count; i++)
        {
            BiomeSpawnWeightEntry row = spawnTable[i];
            if (row.Weight <= 0f || string.IsNullOrWhiteSpace(row.BlockName))
                continue;
            if (FindEntryByName(filteredEntries, row.BlockName) == null)
                continue;

            totalWeight += row.Weight;
        }

        if (totalWeight <= 0f)
            return false;

        float roll = UnityEngine.Random.value * totalWeight;
        float cumulative = 0f;
        BlockUVEntry lastValid = null;

        for (int i = 0; i < spawnTable.Count; i++)
        {
            BiomeSpawnWeightEntry row = spawnTable[i];
            if (row.Weight <= 0f || string.IsNullOrWhiteSpace(row.BlockName))
                continue;

            BlockUVEntry entry = FindEntryByName(filteredEntries, row.BlockName);
            if (entry == null)
                continue;

            cumulative += row.Weight;
            lastValid = entry;
            if (roll <= cumulative)
            {
                selectedBlock = entry;
                return true;
            }
        }

        selectedBlock = lastValid;
        return selectedBlock != null;
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
}
