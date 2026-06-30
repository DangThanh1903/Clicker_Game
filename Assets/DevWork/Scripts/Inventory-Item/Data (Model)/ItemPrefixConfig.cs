using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum ItemPrefix
{
    None = 0,
    Lucky = 1,
    Sharp = 3,
    Cursed = 4
}

public static class ItemPrefixConfig
{
    public static bool TryGetDisplayName(ItemPrefix prefix, out string displayName)
    {
        switch (prefix)
        {
            case ItemPrefix.Lucky:
            case ItemPrefix.Sharp:
            case ItemPrefix.Cursed:
                displayName = prefix.ToString();
                return true;
            default:
                displayName = string.Empty;
                return false;
        }
    }

    public static IReadOnlyList<StatModifier> GetFlatMods(ItemPrefix prefix)
    {
        switch (prefix)
        {
            case ItemPrefix.Lucky:
                return new[]
                {
                    new StatModifier { statType = StatType.Lucky, value = 5f },
                };

            case ItemPrefix.Sharp:
                return new[]
                {
                    new StatModifier { statType = StatType.NormalPower, value = 3f },
                };

            case ItemPrefix.Cursed:
                return new[]
                {
                    new StatModifier { statType = StatType.Lucky, value = -3f },
                };

            default:
                return Array.Empty<StatModifier>();
        }
    }

    // Optional: roll rules based on item type.
    public static ItemPrefix GetRandomFor(Item item)
    {
        if (item == null) return ItemPrefix.None;

        // Example pools (adjust)
        if (item.Type == ItemType.Pickaxe)
            return RandomFrom(ItemPrefix.None, ItemPrefix.Lucky, ItemPrefix.Sharp, ItemPrefix.Cursed);

        if (item.Type == ItemType.Accessory)
            return RandomFrom(ItemPrefix.None, ItemPrefix.Lucky, ItemPrefix.Cursed);

        return ItemPrefix.None;
    }

    private static ItemPrefix RandomFrom(params ItemPrefix[] pool)
    {
        int i = UnityEngine.Random.Range(0, pool.Length);
        return pool[i];
    }
}
