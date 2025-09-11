using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Items/Lootbox")]
public class Lootbox : Item
{
    public override ItemType Type => ItemType.Lootbox;
    public override int MaxStack => 1;

    [Header("Loot Options (6–7 items recommended)")]
    public List<LootboxOption> options = new List<LootboxOption>();

    [Header("Rarity → Default Weight (rarer ⇒ lower)")]
    [Min(0f)] public float commonWeight     = 100f;
    [Min(0f)] public float uncommonWeight   = 45f;
    [Min(0f)] public float rareWeight       = 16f;
    [Min(0f)] public float epicWeight       = 5f;
    [Min(0f)] public float legendaryWeight  = 1f;

    // Map a rarity to its default weight
    public float GetDefaultWeight(Rarity r) => r switch
    {
        Rarity.Common    => commonWeight,
        Rarity.Uncommon  => uncommonWeight,
        Rarity.Rare      => rareWeight,
        Rarity.Epic      => epicWeight,
        Rarity.Legendary => legendaryWeight,
        _                => 1f
    };

    /// <summary>
    /// Roll one reward using weighted random. Returns (Item, amount).
    /// </summary>
    public (Item item, int amount) RollOne(System.Random rng = null)
    {
        if (options == null || options.Count == 0)
        {
            Debug.LogWarning($"Lootbox '{itemName}' has no options.");
            return (null, 0);
        }

        // Sum weights
        float total = 0f;
        var weights = new float[options.Count];
        for (int i = 0; i < options.Count; i++)
        {
            float w = Mathf.Max(0f, options[i].GetWeight(GetDefaultWeight));
            weights[i] = w;
            total += w;
        }
        if (total <= 0f)
        {
            // Fallback: pick first valid option
            var first = options[0];
            int amt = RandomAmount(first, rng);
            return (first.item, amt);
        }

        // Draw
        float r = Next01(rng) * total;
        for (int i = 0; i < options.Count; i++)
        {
            float w = weights[i];
            if (r < w)
            {
                var opt = options[i];
                int amt = RandomAmount(opt, rng);
                return (opt.item, amt);
            }
            r -= w;
        }

        // Fallback to last
        var lastOpt = options[^1];
        return (lastOpt.item, RandomAmount(lastOpt, rng));
    }

    private static int RandomAmount(LootboxOption opt, System.Random rng)
    {
        int min = Mathf.Max(1, opt.minAmount);
        int max = Mathf.Max(min, opt.maxAmount);
        if (min == max) return min;

        // deterministic-friendly RNG if provided
        if (rng != null)
            return rng.Next(min, max + 1);

        // Unity RNG fallback
        return UnityEngine.Random.Range(min, max + 1);
    }

    private static float Next01(System.Random rng)
    {
        if (rng != null) return (float)rng.NextDouble();
        return UnityEngine.Random.value;
    }
}

[Serializable]
public class LootboxOption
{
    public Item item;
    [Min(1)] public int minAmount = 1;
    [Min(1)] public int maxAmount = 1;

    [Tooltip("Leave < 0 to use default rarity weight. Otherwise use this value directly.")]
    public float customWeight = -1f;

    public float GetWeight(Func<Rarity, float> rarityWeightGetter)
    {
        if (customWeight >= 0f) return customWeight;
        return rarityWeightGetter != null ? rarityWeightGetter(item.rarity) : 1f;
    }
}
