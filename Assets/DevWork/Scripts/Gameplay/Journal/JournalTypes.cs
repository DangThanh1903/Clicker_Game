using System;
using System.Collections.Generic;
using UnityEngine;

public enum JournalGoalType
{
    BreakBlock,
    CollectItem,
    CraftItem,
    KillBoss,
    DiscoverBlock,
    CompleteBiome
}

public enum JournalUnlockType
{
    Feature,
    Block,
    Recipe,
    Boss,
    Biome
}

[Serializable]
public sealed class JournalRewardData
{
    public Item item;
    public int amount;
    public int diamonds;
}

[Serializable]
public sealed class JournalUnlockData
{
    public JournalUnlockType type;
    public string targetId;
}

[Serializable]
public sealed class JournalStepData
{
    public string id;
    public string biomeId;
    public int order;
    public string title;
    [TextArea] public string description;
    public string completionToast;
    public JournalGoalType goalType;
    public string targetId;
    public int requiredAmount = 1;
    public List<JournalRewardData> rewards = new();
    public List<JournalUnlockData> unlocks = new();
}

[Serializable]
public sealed class JournalBiomeData
{
    public string biomeId;
    public string title;
    public int order;
    public List<JournalStepData> steps = new();
}

[Serializable]
public sealed class JournalStepProgressSave
{
    public string stepId;
    public int currentAmount;
    public bool completed;
    public bool rewardGranted;
    public bool unlocksApplied;
}

[Serializable]
public sealed class JournalBiomeProgressSave
{
    public string biomeId;
    public List<JournalStepProgressSave> steps = new();
}

[Serializable]
public sealed class JournalProgressSave
{
    public string currentBiomeId;
    public string currentJournalStepId;
    public List<JournalBiomeProgressSave> biomes = new();
    public List<string> unlockedFeatures = new();
    public List<string> unlockedBlocks = new();
    public List<string> unlockedRecipes = new();
    public List<string> unlockedBosses = new();
    public List<string> unlockedBiomes = new();
}

public readonly struct JournalIngredientProgressView
{
    public JournalIngredientProgressView(string label, int current, int required)
    {
        Label = label ?? string.Empty;
        Current = Mathf.Max(0, current);
        Required = Mathf.Max(0, required);
    }

    public string Label { get; }
    public int Current { get; }
    public int Required { get; }
    public bool Completed => Required <= 0 || Current >= Required;
}

public readonly struct JournalHudViewModel
{
    public JournalHudViewModel(
        string biomeTitle,
        int biomePercent,
        string stepTitle,
        string stepDescription,
        IReadOnlyList<JournalIngredientProgressView> lines)
    {
        BiomeTitle = biomeTitle ?? string.Empty;
        BiomePercent = Mathf.Clamp(biomePercent, 0, 100);
        StepTitle = stepTitle ?? string.Empty;
        StepDescription = stepDescription ?? string.Empty;
        Lines = lines ?? Array.Empty<JournalIngredientProgressView>();
    }

    public string BiomeTitle { get; }
    public int BiomePercent { get; }
    public string StepTitle { get; }
    public string StepDescription { get; }
    public IReadOnlyList<JournalIngredientProgressView> Lines { get; }
}

public readonly struct JournalStepViewModel
{
    public JournalStepViewModel(
        string stepId,
        string title,
        string description,
        bool isCompleted,
        bool isActive,
        bool isLocked,
        Sprite icon,
        IReadOnlyList<JournalIngredientProgressView> progressLines,
        string rewardPreview,
        string unlockPreview)
    {
        StepId = stepId ?? string.Empty;
        Title = title ?? string.Empty;
        Description = description ?? string.Empty;
        IsCompleted = isCompleted;
        IsActive = isActive;
        IsLocked = isLocked;
        Icon = icon;
        ProgressLines = progressLines ?? Array.Empty<JournalIngredientProgressView>();
        RewardPreview = rewardPreview ?? string.Empty;
        UnlockPreview = unlockPreview ?? string.Empty;
    }

    public string StepId { get; }
    public string Title { get; }
    public string Description { get; }
    public bool IsCompleted { get; }
    public bool IsActive { get; }
    public bool IsLocked { get; }
    public Sprite Icon { get; }
    public IReadOnlyList<JournalIngredientProgressView> ProgressLines { get; }
    public string RewardPreview { get; }
    public string UnlockPreview { get; }
}
