using System;
using System.Collections.Generic;
using UnityEngine;

public interface IJournalStorage
{
    JournalProgressSave Load();
    void Save(JournalProgressSave save);
}

public sealed class JsonJournalStorage : IJournalStorage
{
    private const string FileName = "journal_save.json";
    private readonly SaveCoordinator saveCoordinator = SaveCoordinator.Ins;

    public JournalProgressSave Load()
    {
        if (!saveCoordinator.TryLoadJson(FileName, out JournalProgressSave data, "Journal"))
            data = new JournalProgressSave();

        Normalize(data);
        return data;
    }

    public void Save(JournalProgressSave save)
    {
        save ??= new JournalProgressSave();
        Normalize(save);
        saveCoordinator.TrySaveJson(FileName, save, "Journal");
    }

    private static void Normalize(JournalProgressSave save)
    {
        if (save == null)
            return;

        save.currentBiomeId ??= string.Empty;
        save.currentJournalStepId ??= string.Empty;
        save.biomes ??= new List<JournalBiomeProgressSave>();
        save.unlockedFeatures ??= new List<string>();
        save.unlockedBlocks ??= new List<string>();
        save.unlockedRecipes ??= new List<string>();
        save.unlockedBosses ??= new List<string>();
        save.unlockedBiomes ??= new List<string>();

        for (int i = 0; i < save.biomes.Count; i++)
        {
            JournalBiomeProgressSave biome = save.biomes[i];
            if (biome == null)
            {
                save.biomes[i] = new JournalBiomeProgressSave();
                biome = save.biomes[i];
            }

            biome.biomeId ??= string.Empty;
            biome.steps ??= new List<JournalStepProgressSave>();

            for (int j = 0; j < biome.steps.Count; j++)
            {
                JournalStepProgressSave step = biome.steps[j];
                if (step == null)
                {
                    save.biomes[i].steps[j] = new JournalStepProgressSave();
                    step = save.biomes[i].steps[j];
                }

                step.stepId ??= string.Empty;
                step.currentAmount = Mathf.Max(0, step.currentAmount);
            }
        }
    }
}
