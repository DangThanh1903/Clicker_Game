using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

public class CraftingController : MonoBehaviour
{
    [SerializeField] private InventorySection inputInventoryData; // 3–4 slots
    [SerializeField] private InventorySection outputInventoryData; // 1 slot
    [SerializeField] private RecipeDatabase recipeDatabase; // Your SO with all recipes
    private List<InventoryItem> lastMatchedVariant;
    private RecipeDatabase.Recipe lastMatchedRecipe;
    private InventoryItem previousOutputItem;


    private CompositeDisposable disposables = new CompositeDisposable();

    private void Start()
    {
        CreateAllCraftingSlots();
        SubscribeToInputChanges();
        SubscribeToOutputChange();
    }

    private void OnDestroy()
    {
        disposables.Dispose();
    }

    public void CreateAllCraftingSlots()
    {
        InventorySlotFactory.CreateSlots(inputInventoryData);
        InventorySlotFactory.CreateSlots(outputInventoryData);
    }

    private void SubscribeToInputChanges()
    {
        var inventory = inputInventoryData.inventoryData;
        inventory.Items.ObserveReplace()
            .Subscribe(_ => CheckAndUpdateOutput())
            .AddTo(disposables);
        
        inventory.Items.ObserveReset()
            .Subscribe(_ => CheckAndUpdateOutput())
            .AddTo(disposables);

        // Initial check
        CheckAndUpdateOutput();
    }

    private void CheckAndUpdateOutput()
    {
        var currentInputItems = inputInventoryData.inventoryData.Items.ToList();

        var (recipe, matchedVariant) = recipeDatabase.GetRecipeWithMatchedVariant(currentInputItems);

        lastMatchedVariant = matchedVariant;
        lastMatchedRecipe = recipe;


        if (recipe != null)
        {
            var craftedItem = new InventoryItem(recipe.result.itemData, recipe.result.quantity.Value);
            outputInventoryData.inventoryData.SetItem(0, craftedItem);
        }
        else
        {
            outputInventoryData.inventoryData.SetItem(0, null);
        }
    }

    private void SubscribeToOutputChange()
    {
        var outputInventory = outputInventoryData.inventoryData;

        outputInventory.Items.ObserveReplace()
            .Subscribe(change =>
            {
                if (change.Index != 0) return;

                var currentItem = outputInventory.Items.Count > 0 ? outputInventory.Items[0] : null;
                var prevItem = previousOutputItem;

                bool wasNotEmptyBefore = prevItem != null &&
                                        prevItem.itemData != null &&
                                        prevItem.itemData.Type != ItemType.None &&
                                        prevItem.quantity.Value > 0;

                bool nowEmpty = currentItem == null ||
                                currentItem.itemData == null ||
                                currentItem.itemData.Type == ItemType.None ||
                                currentItem.quantity.Value == 0;

                bool nowNotEmpty = !nowEmpty;

                bool recipeChanged = false;
                bool quantityDecreased = false;

                if (wasNotEmptyBefore && nowNotEmpty && prevItem != null && currentItem != null)
                {
                    // Check if crafted item changed (different itemData)
                    recipeChanged = prevItem.itemData != currentItem.itemData;

                    // Check if quantity decreased (player took some)
                    quantityDecreased = currentItem.quantity.Value < prevItem.quantity.Value;
                }

                Debug.Log($"Output slot changed. Was not empty before? {wasNotEmptyBefore}, Now empty? {nowEmpty}, Recipe changed? {recipeChanged}, Quantity decreased? {quantityDecreased}");

                if ((wasNotEmptyBefore && nowEmpty) || recipeChanged || quantityDecreased)
                {
                    Debug.Log("Detected crafted item taken or changed! Removing ingredients...");
                    RemoveRecipeIngredients();
                }

                // Update previousOutputItem
                previousOutputItem = currentItem != null ? new InventoryItem(currentItem.itemData, currentItem.quantity.Value) : null;
            })
            .AddTo(disposables);
    }


    // TODO: Fix crafting logic

    public void RemoveRecipeIngredients()
    {
        if (lastMatchedVariant == null || lastMatchedRecipe == null)
        {
            Debug.LogWarning("No cached recipe or variant found for removing ingredients.");
            return;
        }

        var inventory = inputInventoryData.inventoryData;

        int count = Mathf.Min(inventory.Items.Count, lastMatchedRecipe.ingredients.Count);

        // 1. Cache needed data first to avoid modification during iteration
        var itemsToRemove = new List<(int slotIndex, Item itemData, int quantityNeeded)>();

        for (int i = 0; i < count; i++)
        {
            var recipeItem = lastMatchedRecipe.ingredients[i];

            if (recipeItem == null || recipeItem.itemData == null || recipeItem.itemData.Type == ItemType.None)
                continue;

            itemsToRemove.Add((i, recipeItem.itemData, recipeItem.quantity.Value));
        }

        // 2. Now safely modify inventory based on cached data
        foreach (var (slotIndex, itemData, quantityNeeded) in itemsToRemove)
        {
            var slotItem = inventory.Items[slotIndex];

            if (slotItem != null && slotItem.itemData == itemData)
            {
                int newQty = slotItem.quantity.Value - quantityNeeded;

                if (newQty <= 0)
                    inventory.SetItem(slotIndex, new InventoryItem(null, 0));
                else
                    inventory.SetItem(slotIndex, new InventoryItem(slotItem.itemData, newQty));
            }
        }

        // 3. Clear cache after removal
        lastMatchedVariant = null;
        lastMatchedRecipe = null;
    }

}
