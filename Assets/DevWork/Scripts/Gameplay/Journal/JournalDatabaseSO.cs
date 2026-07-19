using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "JournalDatabase", menuName = "Journal/Database")]
public sealed class JournalDatabaseSO : ScriptableObject
{
    public List<JournalBiomeData> biomes = new();

    public JournalBiomeData GetBiome(string biomeId)
    {
        if (string.IsNullOrWhiteSpace(biomeId) || biomes == null)
            return null;

        for (int i = 0; i < biomes.Count; i++)
        {
            JournalBiomeData biome = biomes[i];
            if (biome == null || string.IsNullOrWhiteSpace(biome.biomeId))
                continue;

            if (string.Equals(biome.biomeId, biomeId, StringComparison.OrdinalIgnoreCase))
                return biome;
        }

        return null;
    }

    public JournalStepData GetStep(string stepId)
    {
        if (string.IsNullOrWhiteSpace(stepId) || biomes == null)
            return null;

        for (int i = 0; i < biomes.Count; i++)
        {
            JournalBiomeData biome = biomes[i];
            if (biome?.steps == null)
                continue;

            for (int j = 0; j < biome.steps.Count; j++)
            {
                JournalStepData step = biome.steps[j];
                if (step == null || string.IsNullOrWhiteSpace(step.id))
                    continue;

                if (string.Equals(step.id, stepId, StringComparison.OrdinalIgnoreCase))
                    return step;
            }
        }

        return null;
    }

    public IEnumerable<JournalBiomeData> GetSortedBiomes()
    {
        return (biomes ?? new List<JournalBiomeData>())
            .Where(biome => biome != null && !string.IsNullOrWhiteSpace(biome.biomeId))
            .OrderBy(biome => biome.order);
    }
}
