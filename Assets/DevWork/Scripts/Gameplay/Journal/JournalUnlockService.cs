using System;
using System.Collections.Generic;

public sealed class JournalUnlockService
{
    private readonly HashSet<string> controlledFeatures = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> controlledBlocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> controlledRecipes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> controlledBosses = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> controlledBiomes = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> unlockedFeatures = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unlockedBlocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unlockedRecipes = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unlockedBosses = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unlockedBiomes = new(StringComparer.OrdinalIgnoreCase);

    public void Initialize(JournalDatabaseSO database, JournalProgressSave save)
    {
        controlledFeatures.Clear();
        controlledBlocks.Clear();
        controlledRecipes.Clear();
        controlledBosses.Clear();
        controlledBiomes.Clear();

        unlockedFeatures.Clear();
        unlockedBlocks.Clear();
        unlockedRecipes.Clear();
        unlockedBosses.Clear();
        unlockedBiomes.Clear();

        if (database?.biomes != null)
        {
            for (int i = 0; i < database.biomes.Count; i++)
            {
                JournalBiomeData biome = database.biomes[i];
                if (biome?.steps == null)
                    continue;

                for (int j = 0; j < biome.steps.Count; j++)
                {
                    JournalStepData step = biome.steps[j];
                    if (step?.unlocks == null)
                        continue;

                    for (int k = 0; k < step.unlocks.Count; k++)
                    {
                        JournalUnlockData unlock = step.unlocks[k];
                        if (unlock == null || string.IsNullOrWhiteSpace(unlock.targetId))
                            continue;

                        GetControlledSet(unlock.type)?.Add(unlock.targetId);
                    }
                }
            }
        }

        if (save == null)
            return;

        CopyList(save.unlockedFeatures, unlockedFeatures);
        CopyList(save.unlockedBlocks, unlockedBlocks);
        CopyList(save.unlockedRecipes, unlockedRecipes);
        CopyList(save.unlockedBosses, unlockedBosses);
        CopyList(save.unlockedBiomes, unlockedBiomes);

        if (unlockedBiomes.Count == 0 && save.biomes != null)
        {
            for (int i = 0; i < save.biomes.Count; i++)
            {
                JournalBiomeProgressSave biome = save.biomes[i];
                if (biome == null || string.IsNullOrWhiteSpace(biome.biomeId))
                    continue;

                if (IsBiomeComplete(biome))
                    unlockedBiomes.Add(biome.biomeId);
            }
        }
    }

    public bool ApplyUnlocks(JournalStepData step, JournalProgressSave save)
    {
        if (step?.unlocks == null || save == null)
            return false;

        bool changed = false;

        for (int i = 0; i < step.unlocks.Count; i++)
        {
            JournalUnlockData unlock = step.unlocks[i];
            if (unlock == null || string.IsNullOrWhiteSpace(unlock.targetId))
                continue;

            HashSet<string> runtimeSet = GetRuntimeSet(unlock.type);
            if (runtimeSet != null && runtimeSet.Add(unlock.targetId))
                changed = true;

            List<string> saveList = GetSaveList(unlock.type, save);
            if (saveList != null && !ContainsIgnoreCase(saveList, unlock.targetId))
            {
                saveList.Add(unlock.targetId);
                changed = true;
            }
        }

        return changed;
    }

    public bool IsFeatureUnlocked(string featureId) => IsUnlocked(featureId, controlledFeatures, unlockedFeatures);
    public bool IsBlockUnlocked(string blockId) => IsUnlocked(blockId, controlledBlocks, unlockedBlocks);
    public bool IsRecipeUnlocked(string recipeId) => IsUnlocked(recipeId, controlledRecipes, unlockedRecipes);
    public bool IsBossUnlocked(string bossId) => IsUnlocked(bossId, controlledBosses, unlockedBosses);
    public bool IsBiomeUnlocked(string biomeId) => IsUnlocked(biomeId, controlledBiomes, unlockedBiomes);
    public bool IsBiomeControlled(string biomeId) => controlledBiomes.Contains(biomeId ?? string.Empty);

    public IReadOnlyCollection<string> ControlledRecipes => controlledRecipes;
    public IReadOnlyCollection<string> UnlockedRecipes => unlockedRecipes;
    public IReadOnlyCollection<string> UnlockedBiomes => unlockedBiomes;

    private static bool IsBiomeComplete(JournalBiomeProgressSave biome)
    {
        if (biome?.steps == null || biome.steps.Count == 0)
            return false;

        for (int i = 0; i < biome.steps.Count; i++)
        {
            JournalStepProgressSave step = biome.steps[i];
            if (step == null || !step.completed)
                return false;
        }

        return true;
    }

    private static void CopyList(List<string> source, HashSet<string> target)
    {
        if (source == null || target == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            string value = source[i];
            if (!string.IsNullOrWhiteSpace(value))
                target.Add(value);
        }
    }

    private static bool ContainsIgnoreCase(List<string> source, string value)
    {
        if (source == null || string.IsNullOrWhiteSpace(value))
            return false;

        for (int i = 0; i < source.Count; i++)
        {
            if (string.Equals(source[i], value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsUnlocked(string targetId, HashSet<string> controlledSet, HashSet<string> unlockedSet)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return true;

        return controlledSet == null || !controlledSet.Contains(targetId) || (unlockedSet != null && unlockedSet.Contains(targetId));
    }

    private HashSet<string> GetControlledSet(JournalUnlockType type)
    {
        return type switch
        {
            JournalUnlockType.Feature => controlledFeatures,
            JournalUnlockType.Block => controlledBlocks,
            JournalUnlockType.Recipe => controlledRecipes,
            JournalUnlockType.Boss => controlledBosses,
            JournalUnlockType.Biome => controlledBiomes,
            _ => null
        };
    }

    private HashSet<string> GetRuntimeSet(JournalUnlockType type)
    {
        return type switch
        {
            JournalUnlockType.Feature => unlockedFeatures,
            JournalUnlockType.Block => unlockedBlocks,
            JournalUnlockType.Recipe => unlockedRecipes,
            JournalUnlockType.Boss => unlockedBosses,
            JournalUnlockType.Biome => unlockedBiomes,
            _ => null
        };
    }

    private static List<string> GetSaveList(JournalUnlockType type, JournalProgressSave save)
    {
        return type switch
        {
            JournalUnlockType.Feature => save.unlockedFeatures,
            JournalUnlockType.Block => save.unlockedBlocks,
            JournalUnlockType.Recipe => save.unlockedRecipes,
            JournalUnlockType.Boss => save.unlockedBosses,
            JournalUnlockType.Biome => save.unlockedBiomes,
            _ => null
        };
    }
}
