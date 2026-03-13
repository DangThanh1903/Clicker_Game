using System.Collections.Generic;
using UnityEngine;

public static class DropRollService
{
    public static List<(Item item, int amount)> RollResolvedItemDrops(
        IReadOnlyList<ItemDrop> drops,
        float luck,
        bool requireValidItem = true,
        bool requirePositiveAmount = true)
    {
        var rolledDrops = new List<(Item item, int amount)>();
        if (drops == null || drops.Count == 0)
            return rolledDrops;

        for (int i = 0; i < drops.Count; i++)
        {
            ItemDrop drop = drops[i];
            if (drop == null)
                continue;

            if (requireValidItem && (drop.item == null || drop.item.Type == ItemType.None))
                continue;

            if (!TryRollAmount(drop, luck, out int amount))
                continue;

            if (requirePositiveAmount && amount <= 0)
                continue;

            rolledDrops.Add((drop.item, amount));
        }

        return rolledDrops;
    }

    public static List<ItemDropResult> RollDropResults(
        IReadOnlyList<ItemDrop> drops,
        float luck,
        bool requirePositiveAmount = false)
    {
        var rolledDrops = new List<ItemDropResult>();
        if (drops == null || drops.Count == 0)
            return rolledDrops;

        for (int i = 0; i < drops.Count; i++)
        {
            ItemDrop drop = drops[i];
            if (drop == null)
                continue;

            if (!TryRollAmount(drop, luck, out int amount))
                continue;

            if (requirePositiveAmount && amount <= 0)
                continue;

            rolledDrops.Add(new ItemDropResult(drop, amount));
        }

        return rolledDrops;
    }

    private static bool TryRollAmount(ItemDrop drop, float luck, out int amount)
    {
        amount = 0;
        if (drop == null)
            return false;

        float chance = drop.dropChance;
        if (luck > 0f && chance < 1f)
            chance = LuckMath.BoostChance(chance, luck);

        if (Random.value > chance)
            return false;

        int minAmount = drop.minAmount;
        int maxAmount = Mathf.Max(minAmount, drop.maxAmount);
        amount = Random.Range(minAmount, maxAmount + 1);
        return true;
    }
}
