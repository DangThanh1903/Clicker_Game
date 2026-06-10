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
                HandleOutputClaimByPlayerAction(change);
            })
            .AddTo(disposables);
    }

    private void HandleOutputClaimByPlayerAction((int index, InventoryItem newItem) change)
    {
        bool claimedOut =
            change.newItem == null ||
            change.newItem.itemData == null ||
            change.newItem.itemData.Type == ItemType.None;

        if (!claimedOut)
            return;

        if (currentRecipe == null || matchedVariant == null)
            return;

        TrackCraftQuestForCurrentRecipe();
        RemoveIngredients();
    }

    private void TrackCraftQuestForCurrentRecipe()
    {
        TrackCraftQuest(currentRecipe);
    }

    private void TrackCraftQuest(Recipe recipe, int craftedAmountOverride = -1)
    {
        // If no recipe, ignore
        if (recipe == null || recipe.result == null)
            return;

        Item craftedItem = recipe.result.itemData;
        int craftedAmount = craftedAmountOverride >= 0
            ? craftedAmountOverride
            : (recipe.result.quantity != null ? Mathf.Max(0, recipe.result.quantity.Value) : 0);

        if (craftedItem == null || craftedAmount <= 0)
            return;

        // The item ID used in QuestStepDef.targetId
        string craftedId = craftedItem.itemName;

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

    public bool TryClaimCraftOutput()
    {
        if (outputInventoryData == null || outputInventoryData.inventoryData == null)
            return false;

        var outputData = outputInventoryData.inventoryData;
        var outputItem = outputData.GetItem(0);
        if (outputItem == null || outputItem.itemData == null || outputItem.itemData.Type == ItemType.None)
            return false;

        if (InventoryController.Instance == null)
            return false;

        int outputQty = Mathf.Max(0, outputItem.quantity.Value);
        if (outputQty <= 0)
            return false;

        // Use existing inventory add path as requested.
        var toAdd = new InventoryItem(outputItem.itemData, outputQty)
        {
            prefix = outputItem.prefix
        };

        bool fullyAdded = InventoryController.Instance.TryAddItemToInventory(toAdd, requireFullAdd: true);
        if (!fullyAdded)
            return false;

        TrackCraftQuestForCurrentRecipe();
        RemoveIngredients();
        UpdateCraftingOutput();
        return true;
    }

    public bool TryCraftRecipe(Recipe recipe, InventoryData sourceInventory = null, int times = 1)
    {
        if (recipe == null || recipe.result == null)
            return false;

        Item resultItem = recipe.result.itemData;
        int resultQuantity = recipe.result.quantity != null ? Mathf.Max(0, recipe.result.quantity.Value) : 0;
        int safeTimes = Mathf.Max(1, times);
        int totalResultQuantity = resultQuantity * safeTimes;

        if (resultItem == null || resultItem.Type == ItemType.None || totalResultQuantity <= 0)
            return false;

        sourceInventory ??= ResolveMainInventoryData();
        if (sourceInventory == null || InventoryController.Instance == null)
            return false;

        if (!RecipeInventoryUtility.HasIngredients(recipe, sourceInventory, safeTimes))
            return false;

        if (!InventoryController.Instance.CanFullyAddItem(resultItem, totalResultQuantity))
            return false;

        RemoveIngredientsFromInventory(sourceInventory, RecipeInventoryUtility.BuildRequiredCounts(recipe, safeTimes));

        var crafted = new InventoryItem(resultItem, totalResultQuantity);
        if (!InventoryController.Instance.TryAddItemToInventory(crafted, requireFullAdd: true))
        {
            Debug.LogWarning("CraftingController: failed to add crafted item after ingredient check.");
            return false;
        }

        TrackCraftQuest(recipe, totalResultQuantity);
        DataSaver.Ins?.SaveDataFn();
        UpdateCraftingOutput();
        return true;
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

    private void RemoveIngredientsFromInventory(InventoryData inventory, Dictionary<Item, int> requiredCounts)
    {
        if (inventory == null || requiredCounts == null || requiredCounts.Count == 0)
            return;

        foreach (var pair in requiredCounts)
        {
            Item requiredItem = pair.Key;
            int remaining = Mathf.Max(0, pair.Value);
            if (requiredItem == null || remaining <= 0)
                continue;

            var items = inventory.Items;
            for (int i = 0; i < items.Count && remaining > 0; i++)
            {
                var slot = items[i];
                if (slot == null || slot.itemData != requiredItem || slot.quantity == null)
                    continue;

                int take = Mathf.Min(slot.quantity.Value, remaining);
                int newQuantity = slot.quantity.Value - take;
                remaining -= take;

                if (newQuantity <= 0)
                {
                    inventory.SetItem(i, null);
                }
                else
                {
                    inventory.SetItem(i, new InventoryItem(slot.itemData, newQuantity)
                    {
                        prefix = slot.prefix
                    });
                }
            }
        }
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

    private InventoryData ResolveMainInventoryData()
    {
        if (InventoryController.Instance == null || InventoryController.Instance.InventoryUIManager == null)
            return null;

        return InventoryController.Instance.InventoryUIManager.GetInventoryData(InventoryType.Inventory);
    }
}
