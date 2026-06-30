using System;
using System.Linq;
using UnityEngine;

public readonly struct QuestRuntimeEntry
{
    public QuestRuntimeEntry(QuestType type, QuestDef def, QuestTracker tracker)
    {
        Type = type;
        Def = def;
        Tracker = tracker;
    }

    public QuestType Type { get; }
    public QuestDef Def { get; }
    public QuestTracker Tracker { get; }

    public string Id => Def?.id ?? string.Empty;
    public bool IsAchievement => Def != null && Def.IsAchievement;
    public bool IsCompleted => Tracker != null && Tracker.Completed.Value;
    public bool RewardClaimed => Tracker != null && Tracker.RewardClaimed.Value;
    public bool IsUnlocked => Tracker == null || Tracker.Unlocked.Value;
    public bool CanClaim => IsUnlocked && IsCompleted && !RewardClaimed;

    public QuestRuntimeView ToView()
    {
        return QuestRuntimeViewBuilder.Build(this);
    }
}

public readonly struct QuestRuntimeView
{
    public QuestRuntimeView(
        string questId,
        QuestType type,
        string title,
        string description,
        int currentAmount,
        int requiredAmount,
        bool completed,
        bool rewardClaimed,
        bool unlocked,
        bool canClaim,
        Sprite rewardIcon,
        bool useGemRewardIcon)
    {
        QuestId = questId;
        Type = type;
        Title = title;
        Description = description;
        CurrentAmount = currentAmount;
        RequiredAmount = requiredAmount;
        Completed = completed;
        RewardClaimed = rewardClaimed;
        Unlocked = unlocked;
        CanClaim = canClaim;
        RewardIcon = rewardIcon;
        UseGemRewardIcon = useGemRewardIcon;
    }

    public string QuestId { get; }
    public QuestType Type { get; }
    public string Title { get; }
    public string Description { get; }
    public int CurrentAmount { get; }
    public int RequiredAmount { get; }
    public bool Completed { get; }
    public bool RewardClaimed { get; }
    public bool Unlocked { get; }
    public bool CanClaim { get; }
    public Sprite RewardIcon { get; }
    public bool UseGemRewardIcon { get; }

    public float Progress01
    {
        get
        {
            if (RequiredAmount <= 0)
                return Completed ? 1f : 0f;

            return Mathf.Clamp01((float)CurrentAmount / RequiredAmount);
        }
    }
}

public static class QuestRuntimeViewBuilder
{
    public static QuestRuntimeView Build(QuestRuntimeEntry entry)
    {
        QuestDef def = entry.Def;
        QuestTracker tracker = entry.Tracker;

        int currentAmount = 0;
        int requiredAmount = 0;
        if (tracker != null)
        {
            for (int i = 0; i < tracker.Steps.Count; i++)
            {
                currentAmount += tracker.Steps[i].Current.Value;
                requiredAmount += tracker.Steps[i].Required.Value;
            }
        }

        Sprite rewardIcon = ResolveRewardIcon(def, out bool useGemRewardIcon);
        string title = !string.IsNullOrWhiteSpace(def?.title) ? def.title : entry.Id;
        string description = BuildCurrentStepDescription(def, tracker);
        if (string.IsNullOrWhiteSpace(description))
            description = def?.description ?? string.Empty;

        return new QuestRuntimeView(
            entry.Id,
            entry.Type,
            title,
            description,
            currentAmount,
            requiredAmount,
            entry.IsCompleted,
            entry.RewardClaimed,
            entry.IsUnlocked,
            entry.CanClaim,
            rewardIcon,
            useGemRewardIcon);
    }

    private static string BuildCurrentStepDescription(QuestDef def, QuestTracker tracker)
    {
        if (def?.steps == null || def.steps.Count == 0)
            return string.Empty;

        int totalSteps = def.steps.Count;
        int activeIndex = totalSteps - 1;

        if (tracker != null)
        {
            for (int i = 0; i < tracker.Steps.Count; i++)
            {
                if (!tracker.Steps[i].Completed.Value)
                {
                    activeIndex = i;
                    break;
                }
            }
        }

        activeIndex = Mathf.Clamp(activeIndex, 0, totalSteps - 1);
        QuestStepDef stepDef = def.steps[activeIndex];
        string stepText = BuildStepDescription(stepDef);
        string stepProgress = $"{activeIndex + 1}/{totalSteps}";

        if (string.IsNullOrWhiteSpace(stepText))
            return $"Step ({stepProgress})";

        return $"{stepText} ({stepProgress})";
    }

    private static string BuildStepDescription(QuestStepDef step)
    {
        if (step == null)
            return string.Empty;

        string target = StripBiomeSuffix(step.targetId);
        if (!string.IsNullOrWhiteSpace(target))
            target = char.ToUpperInvariant(target[0]) + (target.Length > 1 ? target.Substring(1) : string.Empty);

        int amount = Mathf.Max(1, step.requiredAmount);

        switch (step.goalType)
        {
            case GoalType.BreakBlock:
                return $"Break {amount} {target}";
            case GoalType.CollectItem:
                return $"Collect {amount} {target}";
            case GoalType.CraftItem:
                return $"Craft {amount} {target}";
            case GoalType.ReachStat:
                return $"Reach {target} >= {amount}";
            case GoalType.Custom:
            default:
                return string.Empty;
        }
    }

    private static string StripBiomeSuffix(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return string.Empty;

        int atIndex = targetId.IndexOf('@');
        return atIndex >= 0 ? targetId.Substring(0, atIndex) : targetId;
    }

    private static Sprite ResolveRewardIcon(QuestDef def, out bool useGemRewardIcon)
    {
        useGemRewardIcon = false;
        if (def?.rewards == null)
            return null;

        for (int i = 0; i < def.rewards.Count; i++)
        {
            RewardDef reward = def.rewards[i];
            if (reward?.items == null)
                continue;

            for (int j = 0; j < reward.items.Count; j++)
            {
                InventoryItem item = reward.items[j];
                if (item == null || item.itemData == null || item.quantity == null || item.quantity.Value <= 0)
                    continue;

                return item.itemData.icon;
            }
        }

        useGemRewardIcon = def.rewards.Any(reward => reward != null && reward.gemAmount > 0);
        return null;
    }
}
