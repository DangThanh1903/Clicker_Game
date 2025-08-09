using System.Collections.Generic;
using System.Text;
using UnityEngine;

[CreateAssetMenu(fileName = "RecipeDatabase", menuName = "Crafting/Recipe Database")]
public class RecipeDatabase : ScriptableObject
{
    [System.Serializable]
    public class Recipe
    {
        public List<InventoryItem> ingredients;
        public InventoryItem result;
    }

    [SerializeField] private List<Recipe> recipes = new();

    // Key: item-only key (ignores quantities)
    // Value: list of recipes that share this item layout
    private Dictionary<string, List<Recipe>> recipeLookupByItems;

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
    }

    // Generates key ignoring quantity (only item names & positions)
    private string GenerateKeyIgnoringQuantity(List<InventoryItem> ingredients)
    {
        StringBuilder sb = new();
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

    // Normalize to 4 slots for 2x2 crafting grid
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

    // Flips and rotations (for 2x2)
    private List<InventoryItem> FlipHorizontal(List<InventoryItem> ingredients)
    {
        if (ingredients.Count != 4) return ingredients;
        return new List<InventoryItem> { ingredients[1], ingredients[0], ingredients[3], ingredients[2] };
    }
    private List<InventoryItem> FlipVertical(List<InventoryItem> ingredients)
    {
        if (ingredients.Count != 4) return ingredients;
        return new List<InventoryItem> { ingredients[2], ingredients[3], ingredients[0], ingredients[1] };
    }
    private List<InventoryItem> FlipHorizontalVertical(List<InventoryItem> ingredients)
    {
        if (ingredients.Count != 4) return ingredients;
        return new List<InventoryItem> { ingredients[3], ingredients[2], ingredients[1], ingredients[0] };
    }

    private IEnumerable<List<InventoryItem>> GenerateAllVariants(List<InventoryItem> ingredients)
    {
        var normalized = NormalizeIngredients(ingredients);
        yield return normalized;
        yield return FlipHorizontal(normalized);
        yield return FlipVertical(normalized);
        yield return FlipHorizontalVertical(normalized);
        // You can add rotations if needed
    }

    // Returns the first matching recipe where input quantities are enough
    public (Recipe recipe, List<InventoryItem> matchedVariant) GetRecipeWithMatchedVariant(List<InventoryItem> inputIngredients)
    {
        if (recipeLookupByItems == null)
            Initialize();

        var normalizedInput = NormalizeIngredients(inputIngredients);

        foreach (var variant in GenerateAllVariants(normalizedInput))
        {
            string keyIgnoringQuantity = GenerateKeyIgnoringQuantity(variant);

            if (recipeLookupByItems.TryGetValue(keyIgnoringQuantity, out var candidateRecipes))
            {
                foreach (var recipe in candidateRecipes)
                {
                    if (IsQuantityEnough(variant, recipe.ingredients))
                    {
                        return (recipe, variant);
                    }
                }
            }
        }

        return (null, null);
    }


    // Checks if input quantities cover recipe quantities (input >= recipe)
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
