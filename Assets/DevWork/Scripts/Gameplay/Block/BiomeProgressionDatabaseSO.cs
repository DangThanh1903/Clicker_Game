using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BiomeProgressionDatabase",
    menuName = "Gameplay/Progression/Biome Progression Database")]
public class BiomeProgressionDatabaseSO : ScriptableObject
{
    [SerializeField] private List<BiomeProgressionTrack> tracks = new List<BiomeProgressionTrack>();

    public IReadOnlyList<BiomeProgressionTrack> Tracks => tracks;

    public bool TryGetTrack(BlockSpawnLocation biome, out BiomeProgressionTrack track)
    {
        track = null;
        if (tracks == null)
            return false;

        for (int i = 0; i < tracks.Count; i++)
        {
            BiomeProgressionTrack candidate = tracks[i];
            if (candidate == null || candidate.Biome != biome)
                continue;

            track = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetMilestone(BlockSpawnLocation biome, int mergeProgress, out BiomeMilestone milestone, out int milestoneIndex)
    {
        milestone = null;
        milestoneIndex = -1;

        if (!TryGetTrack(biome, out BiomeProgressionTrack track) || track == null)
            return false;

        return track.TryGetMilestone(mergeProgress, out milestone, out milestoneIndex);
    }

    public bool TryGetMilestoneAt(BlockSpawnLocation biome, int milestoneIndex, out BiomeMilestone milestone)
    {
        milestone = null;

        if (!TryGetTrack(biome, out BiomeProgressionTrack track) || track == null)
            return false;

        return track.TryGetMilestoneAt(milestoneIndex, out milestone);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (tracks == null)
            return;

        HashSet<BlockSpawnLocation> seenBiomes = new HashSet<BlockSpawnLocation>();
        for (int i = 0; i < tracks.Count; i++)
        {
            BiomeProgressionTrack track = tracks[i];
            if (track == null)
                continue;

            track.Normalize();

            if (!seenBiomes.Add(track.Biome))
            {
                Debug.LogWarning(
                    $"[BiomeProgressionDatabaseSO] Duplicate biome track detected: {track.Biome}. " +
                    "Only the first matching track will be used at runtime.",
                    this);
            }
        }
    }
#endif
}

[Serializable]
public class BiomeProgressionTrack
{
    [SerializeField] private BlockSpawnLocation biome = BlockSpawnLocation.Plain;
    [SerializeField] private List<BiomeMilestone> milestones = new List<BiomeMilestone>();

    public BlockSpawnLocation Biome => biome;
    public IReadOnlyList<BiomeMilestone> Milestones => milestones;

    public bool TryGetMilestone(int mergeProgress, out BiomeMilestone milestone, out int milestoneIndex)
    {
        milestone = null;
        milestoneIndex = -1;

        if (milestones == null || milestones.Count == 0)
            return false;

        int safeProgress = Mathf.Max(0, mergeProgress);

        for (int i = 0; i < milestones.Count; i++)
        {
            BiomeMilestone current = milestones[i];
            if (current == null)
                continue;

            if (safeProgress < current.RequiredMergeProgress)
                break;

            milestone = current;
            milestoneIndex = i;
        }

        return milestone != null;
    }

    public bool TryGetMilestoneAt(int milestoneIndex, out BiomeMilestone milestone)
    {
        milestone = null;

        if (milestones == null || milestoneIndex < 0 || milestoneIndex >= milestones.Count)
            return false;

        milestone = milestones[milestoneIndex];
        return milestone != null;
    }

#if UNITY_EDITOR
    public void Normalize()
    {
        if (milestones == null)
            return;

        milestones.Sort(CompareMilestones);

        HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < milestones.Count; i++)
        {
            BiomeMilestone milestone = milestones[i];
            if (milestone == null)
                continue;

            milestone.Normalize();

            if (string.IsNullOrWhiteSpace(milestone.Id))
                continue;

            if (!seenIds.Add(milestone.Id))
            {
                Debug.LogWarning(
                    $"[BiomeProgressionTrack] Duplicate milestone id '{milestone.Id}' in biome '{biome}'.",
                    null);
            }
        }
    }

    private static int CompareMilestones(BiomeMilestone a, BiomeMilestone b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        return a.RequiredMergeProgress.CompareTo(b.RequiredMergeProgress);
    }
#endif
}

[Serializable]
public class BiomeMilestone
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, Min(0)] private int requiredMergeProgress;
    [SerializeField] private List<BiomeSpawnWeightEntry> spawnTable = new List<BiomeSpawnWeightEntry>();
    [SerializeField] private BiomeProgressionDropRollMode dropRollMode = BiomeProgressionDropRollMode.Independent;
    [SerializeField] private List<BiomeProgressionDropEntry> progressionDrops = new List<BiomeProgressionDropEntry>();
    [SerializeField] private List<BiomeRewardEntry> unlockRewards = new List<BiomeRewardEntry>();

    public string Id => id;
    public string DisplayName => displayName;
    public int RequiredMergeProgress => Mathf.Max(0, requiredMergeProgress);
    public BiomeProgressionDropRollMode DropRollMode => dropRollMode;
    public IReadOnlyList<BiomeSpawnWeightEntry> SpawnTable => spawnTable;
    public IReadOnlyList<BiomeProgressionDropEntry> ProgressionDrops => progressionDrops;
    public IReadOnlyList<BiomeRewardEntry> UnlockRewards => unlockRewards;

#if UNITY_EDITOR
    public void Normalize()
    {
        requiredMergeProgress = Mathf.Max(0, requiredMergeProgress);

        if (id != null)
            id = id.Trim();
        if (displayName != null)
            displayName = displayName.Trim();

        NormalizeSpawnTable();
        NormalizeProgressionDrops();
        NormalizeRewards();
    }

    private void NormalizeSpawnTable()
    {
        if (spawnTable == null)
            return;

        for (int i = 0; i < spawnTable.Count; i++)
        {
            BiomeSpawnWeightEntry entry = spawnTable[i];
            entry.Normalize();
            spawnTable[i] = entry;
        }
    }

    private void NormalizeRewards()
    {
        if (unlockRewards == null)
            return;

        for (int i = 0; i < unlockRewards.Count; i++)
        {
            BiomeRewardEntry entry = unlockRewards[i];
            entry.Normalize();
            unlockRewards[i] = entry;
        }
    }

    private void NormalizeProgressionDrops()
    {
        if (progressionDrops == null)
            return;

        for (int i = 0; i < progressionDrops.Count; i++)
        {
            BiomeProgressionDropEntry entry = progressionDrops[i];
            entry.Normalize();
            progressionDrops[i] = entry;
        }
    }
#endif
}

[Serializable]
public struct BiomeSpawnWeightEntry
{
    [SerializeField] private string blockName;
    [SerializeField, Min(0f)] private float weight;

    public string BlockName => blockName;
    public float Weight => Mathf.Max(0f, weight);

#if UNITY_EDITOR
    public void Normalize()
    {
        if (blockName != null)
            blockName = blockName.Trim();
        weight = Mathf.Max(0f, weight);
    }
#endif
}

[Serializable]
public struct BiomeRewardEntry
{
    [SerializeField] private Item item;
    [SerializeField, Min(1)] private int amount;

    public Item Item => item;
    public int Amount => Mathf.Max(1, amount);

#if UNITY_EDITOR
    public void Normalize()
    {
        amount = Mathf.Max(1, amount);
    }
#endif
}

public enum BiomeProgressionDropRollMode
{
    Independent = 0,
    SinglePickByWeight = 1
}

[Serializable]
public struct BiomeProgressionDropEntry
{
    [SerializeField] private Item item;
    [SerializeField] private string itemAddress;
    [SerializeField, Range(0f, 1f)] private float chance;
    [SerializeField, Min(0f)] private float weight;
    [SerializeField, Min(1)] private int minAmount;
    [SerializeField, Min(1)] private int maxAmount;

    public string ItemAddress
    {
        get
        {
            if (!string.IsNullOrEmpty(itemAddress))
                return itemAddress;
            if (item == null)
                return string.Empty;
            return string.IsNullOrEmpty(item.itemName) ? item.name : item.itemName;
        }
    }
    public float Chance => Mathf.Clamp01(chance);
    public float Weight => Mathf.Max(0f, weight);
    public int MinAmount => Mathf.Max(1, minAmount);
    public int MaxAmount => Mathf.Max(MinAmount, maxAmount);

#if UNITY_EDITOR
    public void Normalize()
    {
        if (itemAddress != null)
            itemAddress = itemAddress.Trim();
        if (string.IsNullOrEmpty(itemAddress) && item != null)
            itemAddress = string.IsNullOrEmpty(item.itemName) ? item.name : item.itemName;
        chance = Mathf.Clamp01(chance);
        weight = Mathf.Max(0f, weight);
        minAmount = Mathf.Max(1, minAmount);
        maxAmount = Mathf.Max(minAmount, maxAmount);
    }
#endif
}
