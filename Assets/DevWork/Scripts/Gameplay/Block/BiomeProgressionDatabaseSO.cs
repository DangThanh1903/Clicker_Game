using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BiomeProgressionDatabase", menuName = "Block/Biome Progression Database")]
public class BiomeProgressionDatabaseSO : ScriptableObject
{
    [SerializeField] private List<BiomeProgressionEntry> entries = new List<BiomeProgressionEntry>();

    public bool TryGetEntry(BlockSpawnLocation biome, out BiomeProgressionEntry entry)
    {
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var candidate = entries[i];
                if (candidate == null || candidate.Biome != biome)
                    continue;

                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }

    public bool TryResolveBiomeForEssence(Item item, out BlockSpawnLocation biome)
    {
        if (item != null && entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.EssenceItem == null)
                    continue;

                if (ReferenceEquals(entry.EssenceItem, item) || entry.EssenceItem.name == item.name)
                {
                    biome = entry.Biome;
                    return true;
                }
            }
        }

        biome = BlockSpawnLocation.Any;
        return false;
    }
}

[Serializable]
public class BiomeProgressionEntry
{
    [SerializeField] private BlockSpawnLocation biome = BlockSpawnLocation.Plain;
    [SerializeField] private Item essenceItem;
    [SerializeField] private List<BiomeProgressionMilestone> milestones = new List<BiomeProgressionMilestone>();

    public BlockSpawnLocation Biome => biome;
    public Item EssenceItem => essenceItem;
    public IReadOnlyList<BiomeProgressionMilestone> Milestones => milestones;
}

[Serializable]
public class BiomeProgressionMilestone
{
    [SerializeField] private string displayName;
    [SerializeField, Min(1)] private int requiredEssenceEarned = 1;
    [SerializeField] private List<BiomeProgressionRewardItem> rewardItems = new List<BiomeProgressionRewardItem>();

    public string DisplayName => displayName;
    public int RequiredEssenceEarned => Mathf.Max(1, requiredEssenceEarned);
    public IReadOnlyList<BiomeProgressionRewardItem> RewardItems => rewardItems;
}

[Serializable]
public class BiomeProgressionRewardItem
{
    [SerializeField] private Item item;
    [SerializeField, Min(1)] private int amount = 1;

    public Item Item => item;
    public int Amount => Mathf.Max(1, amount);
}
