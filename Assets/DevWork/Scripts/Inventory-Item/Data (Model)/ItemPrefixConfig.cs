using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum ItemPrefix
{
    None,
    Lucky,      // +Luck
    Tanky,      // +HP +Def
    Sharp,      // +Damage
    Cursed      // -Luck or -something
}

public static class ItemPrefixConfig
{
    public static IReadOnlyList<StatModifier> GetFlatMods(ItemPrefix prefix)
    {
        switch (prefix)
        {
            case ItemPrefix.Lucky:
                return new[]
                {
                    new StatModifier { statType = StatType.Lucky, value = 5f },
                };

            case ItemPrefix.Tanky:
                return new[]
                {
                    new StatModifier { statType = StatType.HP,  value = 20f },
                    new StatModifier { statType = StatType.Def, value = 5f  },
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
        if (item.Type == ItemType.Weapon)
            return RandomFrom(ItemPrefix.None, ItemPrefix.Lucky, ItemPrefix.Sharp, ItemPrefix.Cursed);

        if (item.Type == ItemType.Accessory)
            return RandomFrom(ItemPrefix.None, ItemPrefix.Lucky, ItemPrefix.Tanky, ItemPrefix.Cursed);

        return ItemPrefix.None;
    }

    private static ItemPrefix RandomFrom(params ItemPrefix[] pool)
    {
        int i = UnityEngine.Random.Range(0, pool.Length);
        return pool[i];
    }
}
