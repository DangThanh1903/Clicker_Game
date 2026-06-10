using System.Collections.Generic;
using UnityEngine;

public readonly struct RecipeIngredientStatus
{
    public readonly Item item;
    public readonly int required;
    public readonly int available;

    public bool HasEnough => item != null && available >= required;

    public RecipeIngredientStatus(Item item, int required, int available)
    {
        this.item = item;
        this.required = Mathf.Max(0, required);
        this.available = Mathf.Max(0, available);
    }
}

public static class RecipeInventoryUtility
{
    public static Dictionary<Item, int> BuildRequiredCounts(Recipe recipe, int times = 1)
    {
        var required = new Dictionary<Item, int>();
        if (recipe == null)
            return required;

        int safeTimes = Mathf.Max(1, times);
        var normalized = RecipeDatabase.NormalizeIngredients(recipe.ingredients);
        for (int i = 0; i < normalized.Count; i++)
        {
            var ingredient = normalized[i];
            Item item = ingredient != null ? ingredient.itemData : null;
            int quantity = ingredient != null && ingredient.quantity != null
                ? Mathf.Max(0, ingredient.quantity.Value) * safeTimes
                : 0;

            if (item == null || item.Type == ItemType.None || quantity <= 0)
                continue;

            if (required.TryGetValue(item, out int current))
                required[item] = current + quantity;
            else
                required[item] = quantity;
        }

        return required;
    }

    public static Dictionary<Item, int> BuildAvailableCounts(InventoryData inventory)
    {
        var available = new Dictionary<Item, int>();
        if (inventory == null)
            return available;

        var items = inventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            var slot = items[i];
            Item item = slot != null ? slot.itemData : null;
            int quantity = slot != null && slot.quantity != null ? Mathf.Max(0, slot.quantity.Value) : 0;

            if (item == null || item.Type == ItemType.None || quantity <= 0)
                continue;

            if (available.TryGetValue(item, out int current))
                available[item] = current + quantity;
            else
                available[item] = quantity;
        }

        return available;
    }

    public static List<RecipeIngredientStatus> BuildIngredientStatuses(
        Recipe recipe,
        InventoryData inventory,
        int times = 1)
    {
        var result = new List<RecipeIngredientStatus>();
        var required = BuildRequiredCounts(recipe, times);
        var available = BuildAvailableCounts(inventory);

        foreach (var pair in required)
        {
            available.TryGetValue(pair.Key, out int count);
            result.Add(new RecipeIngredientStatus(pair.Key, pair.Value, count));
        }

        result.Sort((a, b) =>
        {
            string aName = a.item != null ? a.item.itemName : string.Empty;
            string bName = b.item != null ? b.item.itemName : string.Empty;
            return string.Compare(aName, bName, System.StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    public static bool HasIngredients(Recipe recipe, InventoryData inventory, int times = 1)
    {
        var required = BuildRequiredCounts(recipe, times);
        var available = BuildAvailableCounts(inventory);

        foreach (var pair in required)
        {
            if (!available.TryGetValue(pair.Key, out int count) || count < pair.Value)
                return false;
        }

        return true;
    }
}
