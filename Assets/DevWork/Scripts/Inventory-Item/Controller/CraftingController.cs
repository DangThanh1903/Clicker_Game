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
    private Recipe currentRecipe;

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
        AnalyticsManager.Ins?.TrackCraftComplete(craftedId, craftedAmount);
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

    public bool CheckRecipeIngredients(Recipe recipe, InventoryData sourceInventory, out List<int> missingSlots)
    {
        missingSlots = new List<int>();
        if (recipe == null || sourceInventory == null)
            return false;

        var normalized = RecipeDatabase.NormalizeIngredients(recipe.ingredients);
        var available = BuildAvailableCounts(sourceInventory);

        for (int i = 0; i < normalized.Count; i++)
        {
            var req = normalized[i];
            var item = req?.itemData;
            int qty = req?.quantity?.Value ?? 0;
            if (item == null || item.Type == ItemType.None || qty <= 0)
                continue;

            if (!available.TryGetValue(item, out int have) || have < qty)
            {
                missingSlots.Add(i);
                available[item] = 0;
                continue;
            }

            available[item] = have - qty;
        }

        return missingSlots.Count == 0;
    }

    public bool TryAutoFillRecipe(Recipe recipe, InventoryData sourceInventory, out List<int> missingSlots)
    {
        if (!CheckRecipeIngredients(recipe, sourceInventory, out missingSlots))
            return false;

        ReturnItemsToInventory();

        if (HasAnyItems(inputInventoryData.inventoryData))
        {
            Debug.LogWarning("CraftingController: crafting slots still occupied, auto-fill aborted.");
            return false;
        }

        var normalized = RecipeDatabase.NormalizeIngredients(recipe.ingredients);
        var inputData = inputInventoryData.inventoryData;

        for (int i = 0; i < normalized.Count; i++)
        {
            var req = normalized[i];
            var item = req?.itemData;
            int qty = req?.quantity?.Value ?? 0;

            if (item == null || item.Type == ItemType.None || qty <= 0)
            {
                inputData.SetItem(i, null);
                continue;
            }

            int taken = TakeFromInventory(sourceInventory, item, qty);
            if (taken < qty)
            {
                inputData.SetItem(i, null);
                missingSlots.Add(i);
                continue;
            }

            inputData.SetItem(i, new InventoryItem(item, qty));
        }

        return missingSlots.Count == 0;
    }

    public void ReturnItemsToInventory()
    {
        if (inputInventoryData == null || inputInventoryData.inventoryData == null)
            return;

        if (InventoryController.Instance == null)
        {
            Debug.LogWarning("InventoryController.Instance is null, cannot return crafting items.");
            return;
        }

        var inputItems = inputInventoryData.inventoryData.Items;

        for (int i = 0; i < inputItems.Count; i++)
        {
            var item = inputItems[i];
            if (item == null || item.itemData == null || item.itemData.Type == ItemType.None || item.quantity.Value <= 0)
                continue;

            var returning = new InventoryItem(item.itemData, item.quantity.Value)
            {
                prefix = item.prefix
            };

            bool fullyAdded = InventoryController.Instance.TryAddItemToInventory(returning);

            if (fullyAdded || returning.quantity.Value <= 0)
            {
                inputInventoryData.inventoryData.SetItem(i, null);
            }
            else
            {
                inputInventoryData.inventoryData.SetItem(i, new InventoryItem(returning.itemData, returning.quantity.Value)
                {
                    prefix = returning.prefix
                });
                Debug.LogWarning("Inventory full, cannot return all crafting items.");
            }
        }
    }

    private Dictionary<Item, int> BuildAvailableCounts(InventoryData sourceInventory)
    {
        var available = new Dictionary<Item, int>();
        foreach (var it in sourceInventory.Items)
        {
            if (it == null || it.itemData == null || it.itemData.Type == ItemType.None)
                continue;

            if (!available.ContainsKey(it.itemData))
                available[it.itemData] = 0;
            available[it.itemData] += Mathf.Max(0, it.quantity.Value);
        }
        return available;
    }

    private int TakeFromInventory(InventoryData sourceInventory, Item item, int amount)
    {
        int remaining = amount;
        var items = sourceInventory.Items;

        for (int i = 0; i < items.Count && remaining > 0; i++)
        {
            var slot = items[i];
            if (slot == null || slot.itemData != item)
                continue;

            int canTake = Mathf.Min(slot.quantity.Value, remaining);
            slot.quantity.Value -= canTake;
            remaining -= canTake;

            if (slot.quantity.Value <= 0)
                sourceInventory.SetItem(i, null);
        }

        return amount - remaining;
    }

    private bool HasAnyItems(InventoryData data)
    {
        if (data == null) return false;
        foreach (var it in data.Items)
        {
            if (it != null && it.itemData != null && it.itemData.Type != ItemType.None && it.quantity.Value > 0)
                return true;
        }
        return false;
    }
}
