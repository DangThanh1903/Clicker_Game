using System.Collections.Generic;
using UnityEngine;

public class BiomeProgressionService : MonoBehaviour
{
    public static BiomeProgressionService Instance { get; private set; }

    [SerializeField] private BiomeProgressionDatabaseSO database;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void NotifyItemEarned(Item item, int amount)
    {
        if (Instance == null)
            return;

        Instance.RecordItemEarned(item, amount);
    }

    public void RecordItemEarned(Item item, int amount)
    {
        if (database == null || item == null || amount <= 0)
            return;

        if (!database.TryResolveBiomeForEssence(item, out BlockSpawnLocation biome))
            return;

        if (DataSaver.Ins == null)
            return;

        DataSaver.Ins.AddBiomeEssenceEarned(biome, amount, queueSave: false);
        TryGrantUnlockedMilestones(biome);
        DataSaver.Ins.SaveDataFn();
    }

    public int GetEarnedEssence(BlockSpawnLocation biome)
    {
        return DataSaver.Ins != null ? DataSaver.Ins.GetBiomeEssenceEarned(biome) : 0;
    }

    public float GetProgress01(BlockSpawnLocation biome)
    {
        if (database == null || !database.TryGetEntry(biome, out var entry))
            return 0f;

        int nextIndex = DataSaver.Ins != null ? DataSaver.Ins.GetBiomeProgressClaimedLevel(biome) + 1 : 0;
        var milestones = entry.Milestones;
        if (milestones == null || nextIndex < 0 || nextIndex >= milestones.Count)
            return 1f;

        int earned = GetEarnedEssence(biome);
        int required = milestones[nextIndex].RequiredEssenceEarned;
        return required > 0 ? Mathf.Clamp01((float)earned / required) : 1f;
    }

    private void TryGrantUnlockedMilestones(BlockSpawnLocation biome)
    {
        if (database == null || !database.TryGetEntry(biome, out var entry))
            return;

        var milestones = entry.Milestones;
        if (milestones == null || milestones.Count == 0)
            return;

        int earned = DataSaver.Ins.GetBiomeEssenceEarned(biome);
        int claimedLevel = DataSaver.Ins.GetBiomeProgressClaimedLevel(biome);

        for (int i = claimedLevel + 1; i < milestones.Count; i++)
        {
            var milestone = milestones[i];
            if (milestone == null || earned < milestone.RequiredEssenceEarned)
                break;

            DataSaver.Ins.SetBiomeProgressClaimedLevel(biome, i, queueSave: false);
            GrantMilestoneReward(biome, i, milestone);
        }
    }

    private static void GrantMilestoneReward(BlockSpawnLocation biome, int milestoneIndex, BiomeProgressionMilestone milestone)
    {
        var rewards = milestone.RewardItems;
        if (rewards == null || rewards.Count == 0)
            return;

        var grantEntries = new List<DropGrantEntry>(rewards.Count);
        for (int i = 0; i < rewards.Count; i++)
        {
            var reward = rewards[i];
            if (reward == null || reward.Item == null)
                continue;

            grantEntries.Add(new DropGrantEntry(reward.Item, reward.Amount));
        }

        DropGrantService.TryGrantDrops(
            grantEntries,
            out _,
            logContext: $"[BiomeProgression:{biome}:{milestoneIndex}]");
    }
}
