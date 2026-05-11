using System.Collections.Generic;
using UnityEngine;

public static class BiomeMilestoneRewardService
{
    private static bool loggedMissingRuntimeContext;
    private static bool loggedMissingProgressionDatabase;

    public static void TryGrantRewardsForProgressIncrease(
        BlockSpawnLocation biome,
        int previousMergeProgress,
        int currentMergeProgress)
    {
        if (currentMergeProgress <= previousMergeProgress)
            return;

        if (!TryResolveContext(out DataSaver saver, out InventoryController inventory, out BiomeProgressionDatabaseSO database))
            return;

        if (!BiomeProgressionService.TryGetActiveMilestone(
                database,
                biome,
                currentMergeProgress,
                out _,
                out int activeMilestoneIndex))
        {
            return;
        }

        int claimedMilestoneIndex = saver.GetClaimedMilestoneIndex(biome);
        if (activeMilestoneIndex <= claimedMilestoneIndex)
            return;

        for (int i = claimedMilestoneIndex + 1; i <= activeMilestoneIndex; i++)
        {
            if (!database.TryGetMilestoneAt(biome, i, out BiomeMilestone milestone) || milestone == null)
                continue;

            // Only reward milestones newly crossed by this merge step.
            if (milestone.RequiredMergeProgress <= previousMergeProgress)
            {
                saver.SetClaimedMilestoneIndex(biome, i, queueSave: false);
                continue;
            }

            List<DropGrantEntry> rewards = BuildRewardEntries(milestone);
            if (rewards.Count > 0)
            {
                if (!CanFullyGrantRewards(inventory, rewards))
                {
                    Toaster.Show("Inventory full. Free slots to claim milestone reward.");
                    DevLog.Log($"[BiomeMilestoneReward] Cannot grant milestone rewards for biome {biome} (milestone index {i}) due to inventory capacity.");
                    break;
                }

                DropGrantService.TryGrantDrops(
                    rewards,
                    out _,
                    null,
                    "[BiomeMilestoneReward]");

                string milestoneName = !string.IsNullOrWhiteSpace(milestone.DisplayName)
                    ? milestone.DisplayName
                    : (!string.IsNullOrWhiteSpace(milestone.Id) ? milestone.Id : $"Milestone {i + 1}");
                Toaster.Show($"Milestone unlocked: {milestoneName}");
            }

            saver.SetClaimedMilestoneIndex(biome, i, queueSave: false);
        }

        saver.SaveDataFn();
    }

    private static List<DropGrantEntry> BuildRewardEntries(BiomeMilestone milestone)
    {
        var rewards = new List<DropGrantEntry>();
        if (milestone == null || milestone.UnlockRewards == null)
            return rewards;

        for (int i = 0; i < milestone.UnlockRewards.Count; i++)
        {
            BiomeRewardEntry reward = milestone.UnlockRewards[i];
            if (reward.Item == null || reward.Item.Type == ItemType.None)
                continue;

            int amount = Mathf.Max(1, reward.Amount);
            rewards.Add(new DropGrantEntry(reward.Item, amount));
        }

        return rewards;
    }

    private static bool CanFullyGrantRewards(InventoryController inventory, List<DropGrantEntry> rewards)
    {
        if (inventory == null)
            return false;
        if (rewards == null || rewards.Count == 0)
            return true;

        var checkItems = new List<InventoryItem>(rewards.Count);
        for (int i = 0; i < rewards.Count; i++)
        {
            DropGrantEntry reward = rewards[i];
            if (reward.Item == null || reward.Amount <= 0)
                continue;

            checkItems.Add(new InventoryItem(reward.Item, reward.Amount));
        }

        return inventory.CanFullyAddItems(checkItems);
    }

    private static bool TryResolveContext(
        out DataSaver saver,
        out InventoryController inventory,
        out BiomeProgressionDatabaseSO progressionDatabase)
    {
        saver = DataSaver.Ins;
        inventory = InventoryController.Instance;
        progressionDatabase = null;

        if (saver == null || inventory == null || BlockManager.Ins == null || BlockManager.Ins.CurrentBlock == null)
        {
            if (!loggedMissingRuntimeContext)
            {
                DevLog.Log("[BiomeMilestoneReward] Missing runtime context. Reward grant skipped.");
                loggedMissingRuntimeContext = true;
            }
            return false;
        }

        loggedMissingRuntimeContext = false;
        BlockUVDatabase blockDatabase = BlockManager.Ins.CurrentBlock.blockUVDatabase;
        progressionDatabase = blockDatabase != null ? blockDatabase.BiomeProgressionDatabase : null;
        if (progressionDatabase == null)
        {
            if (!loggedMissingProgressionDatabase)
            {
                DevLog.Log("[BiomeMilestoneReward] BiomeProgressionDatabase is missing on BlockUVDatabase. Reward grant skipped.");
                loggedMissingProgressionDatabase = true;
            }
            return false;
        }

        loggedMissingProgressionDatabase = false;
        return true;
    }
}
