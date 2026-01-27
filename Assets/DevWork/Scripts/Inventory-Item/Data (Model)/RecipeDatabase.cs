using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[CreateAssetMenu(fileName = "RecipeDatabase", menuName = "Crafting/Recipe Database")]
public class RecipeDatabase : ScriptableObject
{
    [SerializeField] private List<Recipe> recipes = new();

    // Lookup dictionary for quick recipe search by item layout key (ignores quantities)
    private Dictionary<string, List<Recipe>> recipeLookupByItems;
    private Dictionary<Item, List<Recipe>> recipeLookupByResult;

    public void Initialize()
    {
        recipeLookupByItems = new Dictionary<string, List<Recipe>>();

        foreach (var recipe in recipes)
        {
            foreach (var variant in GenerateAllVariants(recipe.ingredients))
            {
                string keyIgnoringQuantity = GenerateKeyIgnoringQuantity(variant);
                if (!recipeLookupByItems.TryGetValue(keyIgnoringQuantity, out var list))
                {
                    list = new List<Recipe>();
                    recipeLookupByItems[keyIgnoringQuantity] = list;
                }
                if (!list.Contains(recipe))
                    list.Add(recipe);
            }
        }

        recipeLookupByResult = new Dictionary<Item, List<Recipe>>();
        foreach (var r in recipes)
        {
            var item = r?.result?.itemData;
            if (item == null) continue;
            if (!recipeLookupByResult.TryGetValue(item, out var list))
            {
                list = new List<Recipe>();
                recipeLookupByResult[item] = list;
            }
            list.Add(r);
        }
    }

    public List<Recipe> GetRecipesByResultItem(Item itemData)
    {
        if (recipeLookupByResult == null) Initialize();
        if (itemData == null) return new List<Recipe>();
        return recipeLookupByResult.TryGetValue(itemData, out var list)
            ? list
            : new List<Recipe>();
    }

    // Generate a unique key string based on item types and their positions, ignoring quantities
    private string GenerateKeyIgnoringQuantity(List<InventoryItem> ingredients)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < ingredients.Count; i++)
        {
            var ingredient = ingredients[i];
            if (ingredient != null && ingredient.itemData != null && ingredient.itemData.Type != ItemType.None)
                sb.Append($"{i}:{ingredient.itemData.name}_");
            else
                sb.Append($"{i}:None_");
        }
        return sb.ToString();
    }

    // Normalize ingredients list to exactly 4 slots (2x2)
    public static List<InventoryItem> NormalizeIngredients(List<InventoryItem> originalIngredients)
    {
        var normalized = new List<InventoryItem>(4);

        for (int i = 0; i < 4; i++)
        {
            if (i < originalIngredients.Count && originalIngredients[i] != null)
                normalized.Add(originalIngredients[i]);
            else
                normalized.Add(new InventoryItem(null, 0));
        }

        return normalized;
    }

    // Generate all unique permutations of the normalized ingredients
    private IEnumerable<List<InventoryItem>> GenerateAllVariants(List<InventoryItem> ingredients)
    {
        var normalized = NormalizeIngredients(ingredients);
        var indices = Enumerable.Range(0, normalized.Count).ToList();
        var seen = new HashSet<string>();

        foreach (var perm in GetPermutations(indices))
        {
            var permList = perm.Select(i => normalized[i]).ToList();

            // Avoid duplicates when same item appears in multiple slots
            string key = string.Join(",", permList.Select(x => x?.itemData?.name ?? "None"));
            if (seen.Add(key))
                yield return permList;
        }
    }

    private IEnumerable<List<int>> GetPermutations(List<int> items)
    {
        if (items.Count == 1)
        {
            yield return new List<int> { items[0] };
            yield break;
        }

        for (int i = 0; i < items.Count; i++)
        {
            int current = items[i];
            var remaining = new List<int>(items);
            remaining.RemoveAt(i);

            foreach (var perm in GetPermutations(remaining))
            {
                perm.Insert(0, current);
                yield return perm;
            }
        }
    }

    // Try to find the first recipe matching the input ingredients layout and sufficient quantities
    public (Recipe recipe, List<InventoryItem> matchedVariant) GetRecipeWithFlippedRecipeIngredients(List<InventoryItem> inputIngredients)
    {
        if (recipeLookupByItems == null)
            Initialize();

        var normalizedInput = NormalizeIngredients(inputIngredients);
        string inputKey = GenerateKeyIgnoringQuantity(normalizedInput);

        if (recipeLookupByItems.TryGetValue(inputKey, out var possibleRecipes))
        {
            foreach (var recipe in possibleRecipes)
            {
                foreach (var variant in GenerateAllVariants(recipe.ingredients))
                {
                    if (GenerateKeyIgnoringQuantity(variant) == inputKey &&
                        IsQuantityEnough(normalizedInput, variant))
                    {
                        return (recipe, variant);
                    }
                }
            }
        }

        return (null, null);
    }

    private bool IsQuantityEnough(List<InventoryItem> input, List<InventoryItem> recipe)
    {
        for (int i = 0; i < recipe.Count; i++)
        {
            int recipeQty = recipe[i]?.quantity?.Value ?? 0;
            int inputQty = input[i]?.quantity?.Value ?? 0;
            if (inputQty < recipeQty)
                return false;
        }
        return true;
    }
}
