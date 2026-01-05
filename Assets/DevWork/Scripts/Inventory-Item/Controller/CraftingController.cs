using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

public class CraftingController : MonoBehaviour
{
    [SerializeField] private InventorySection inputInventoryData;  
    [SerializeField] private InventorySection outputInventoryData;
    [SerializeField] private RecipeDatabase recipeDatabase;

    private List<InventoryItem> matchedVariant;
    private RecipeDatabase.Recipe currentRecipe;

    private CompositeDisposable disposables = new CompositeDisposable();

    private void Start()
    {
        InventorySlotFactory.CreateSlots(inputInventoryData);
        InventorySlotFactory.CreateSlots(outputInventoryData);
        SubscribeToInventoryChanges();
    }

    private void OnDestroy()
    {
        disposables.Dispose();
    }

    private void SubscribeToInventoryChanges()
    {
        var inputItems = inputInventoryData.inventoryData.Items;
        var outputData = outputInventoryData.inventoryData;

        // Update output recipe when input changes
        inputItems.ObserveReplace()
            .Subscribe(_ => UpdateCraftingOutput())
            .AddTo(disposables);

        inputItems.ObserveReset()
            .Subscribe(_ => UpdateCraftingOutput())
            .AddTo(disposables);

        // Subscribe to player-driven SetItem calls on output slot 0 only
        outputData.OnPlayerSetItem
            .Where(change => change.index == 0)
            .Subscribe(change =>
            {
                HandleCraftQuest(change);
                RemoveIngredients();
            })
            .AddTo(disposables);
    }

    private void HandleCraftQuest((int index, InventoryItem newItem) change)
    {
        // If no recipe, ignore
        if (currentRecipe == null || currentRecipe.result == null)
            return;

        // The crafted output item
        InventoryItem crafted = change.newItem;
        if (crafted == null || crafted.itemData == null)
            return;

        // The item ID used in QuestStepDef.targetId
        string craftedId = crafted.itemData.itemName;

        // Quantity crafted
        int craftedAmount = crafted.quantity.Value;

        // Emit quest signal
        QuestSignals.CraftItem(craftedId, craftedAmount);
    }

    private void UpdateCraftingOutput()
    {
        var inputItems = inputInventoryData.inventoryData.Items.ToList();

        // Call updated database method returning flipped variant too
        var match = recipeDatabase.GetRecipeWithFlippedRecipeIngredients(inputItems);
        currentRecipe = match.recipe;
        matchedVariant = match.matchedVariant;

        if (currentRecipe != null)
        {
            var crafted = new InventoryItem(currentRecipe.result.itemData, currentRecipe.result.quantity.Value);
            outputInventoryData.inventoryData.SetItem(0, crafted);
        }
        else
        {
            outputInventoryData.inventoryData.SetItem(0, null);
        }
    }

    private void RemoveIngredients()
    {
        if (currentRecipe == null || matchedVariant == null) return;

        var inputItems = inputInventoryData.inventoryData.Items;

        // Cache removal info: (slotIndex, InventoryItem with new qty)
        var itemsToSet = new List<(int index, InventoryItem newItem)>();

        for (int i = 0; i < matchedVariant.Count; i++)
        {
            var recipeIngredient = matchedVariant[i];
            if (recipeIngredient == null || recipeIngredient.itemData == null || recipeIngredient.quantity.Value <= 0)
                continue;

            if (i >= inputItems.Count) continue;

            var slotItem = inputItems[i];
            if (slotItem != null && slotItem.itemData == recipeIngredient.itemData)
            {
                int newQty = slotItem.quantity.Value - recipeIngredient.quantity.Value;

                InventoryItem newItem;
                if (newQty <= 0)
                    newItem = new InventoryItem(null, 0);
                else
                    newItem = new InventoryItem(slotItem.itemData, newQty);

                itemsToSet.Add((i, newItem));
            }
        }

        // Apply all removals at once
        foreach (var (index, newItem) in itemsToSet)
        {
            inputInventoryData.inventoryData.SetItem(index, newItem);
        }

        DataSaver.Ins.SaveDataFn();
    }

}
