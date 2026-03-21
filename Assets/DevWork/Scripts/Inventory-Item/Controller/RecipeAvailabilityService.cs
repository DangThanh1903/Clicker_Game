using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

[DisallowMultipleComponent]
public class RecipeAvailabilityService : MonoBehaviour
{
    public enum NearMetric
    {
        MissingIngredientTypes = 0,
        MissingTotalQuantity = 1
    }

    [Serializable]
    public sealed class RecipeAvailabilityEntry
    {
        public Recipe recipe;
        public Item resultItem;
        public int resultQuantity;
        public int missingIngredientTypes;
        public int missingTotalQuantity;
    }

    [Header("Data Source")]
    [SerializeField] private RecipeDatabase recipeDatabase;
    [SerializeField] private InventoryData sourceInventory;
    [SerializeField] private bool autoResolveMainInventory = true;

    [Header("Near Rule")]
    [SerializeField, Min(1)] private int nearThreshold = 1;
    [SerializeField] private NearMetric nearMetric = NearMetric.MissingIngredientTypes;

    private readonly List<RecipeAvailabilityEntry> craftableRecipes = new List<RecipeAvailabilityEntry>();
    private readonly List<RecipeAvailabilityEntry> nearRecipes = new List<RecipeAvailabilityEntry>();
    private readonly Subject<Unit> availabilityChanged = new Subject<Unit>();
    private readonly Subject<Unit> refreshRequests = new Subject<Unit>();

    private readonly CompositeDisposable rootDisposables = new CompositeDisposable();
    private readonly CompositeDisposable inventoryDisposables = new CompositeDisposable();
    private readonly CompositeDisposable quantityDisposables = new CompositeDisposable();

    private bool refreshStreamBound;

    public IReadOnlyList<RecipeAvailabilityEntry> CraftableRecipes => craftableRecipes;
    public IReadOnlyList<RecipeAvailabilityEntry> NearRecipes => nearRecipes;
    public IObservable<Unit> OnAvailabilityChanged => availabilityChanged;

    private void Awake()
    {
        if (autoResolveMainInventory && sourceInventory == null)
            TryResolveMainInventory();
    }

    private void Start()
    {
        EnsureRefreshStream();
        RebindInventorySource();
        RequestRefresh();
    }

    private void OnDestroy()
    {
        quantityDisposables.Dispose();
        inventoryDisposables.Dispose();
        rootDisposables.Dispose();
        refreshRequests.Dispose();
        availabilityChanged.Dispose();
    }

    public void SetSourceInventory(InventoryData inventoryData)
    {
        if (sourceInventory == inventoryData)
            return;

        sourceInventory = inventoryData;
        RebindInventorySource();
        RequestRefresh();
    }

    public void SetNearThreshold(int threshold)
    {
        int next = Mathf.Max(1, threshold);
        if (nearThreshold == next)
            return;

        nearThreshold = next;
        RequestRefresh();
    }

    public void SetNearMetric(NearMetric metric)
    {
        if (nearMetric == metric)
            return;

        nearMetric = metric;
        RequestRefresh();
    }

    public void ForceRefresh()
    {
        RecomputeAvailability();
    }

    private void EnsureRefreshStream()
    {
        if (refreshStreamBound)
            return;

        refreshStreamBound = true;
        refreshRequests
            .ThrottleFrame(1)
            .Subscribe(_ => RecomputeAvailability())
            .AddTo(rootDisposables);
    }

    private void RebindInventorySource()
    {
        inventoryDisposables.Clear();
        quantityDisposables.Clear();

        if (sourceInventory == null && autoResolveMainInventory)
            TryResolveMainInventory();

        if (sourceInventory == null)
            return;

        var items = sourceInventory.Items;
        items.ObserveReplace()
            .Subscribe(_ =>
            {
                RebindQuantitySubscriptions();
                RequestRefresh();
            })
            .AddTo(inventoryDisposables);

        items.ObserveReset()
            .Subscribe(_ =>
            {
                RebindQuantitySubscriptions();
                RequestRefresh();
            })
            .AddTo(inventoryDisposables);

        RebindQuantitySubscriptions();
    }

    private void RebindQuantitySubscriptions()
    {
        quantityDisposables.Clear();

        if (sourceInventory == null)
            return;

        var items = sourceInventory.Items;
        for (int i = 0; i < items.Count; i++)
        {
            InventoryItem slotItem = items[i];
            if (slotItem == null || slotItem.quantity == null)
                continue;

            slotItem.quantity
                .Skip(1)
                .Subscribe(_ => RequestRefresh())
                .AddTo(quantityDisposables);
        }
    }

    private void RequestRefresh()
    {
        refreshRequests.OnNext(Unit.Default);
    }

    private void RecomputeAvailability()
    {
        craftableRecipes.Clear();
        nearRecipes.Clear();

        if (sourceInventory == null && autoResolveMainInventory)
        {
            TryResolveMainInventory();
            if (sourceInventory != null)
                RebindInventorySource();
        }

        if (recipeDatabase == null || sourceInventory == null)
        {
            availabilityChanged.OnNext(Unit.Default);
            return;
        }

        var recipes = recipeDatabase.Recipes;
        if (recipes == null || recipes.Count == 0)
        {
            availabilityChanged.OnNext(Unit.Default);
            return;
        }

        var availableCounts = BuildAvailableCounts(sourceInventory);

        for (int i = 0; i < recipes.Count; i++)
        {
            Recipe recipe = recipes[i];
            if (!TryBuildEntry(recipe, availableCounts, out var entry))
                continue;

            if (entry.missingTotalQuantity == 0)
            {
                craftableRecipes.Add(entry);
                continue;
            }

            int score = nearMetric == NearMetric.MissingTotalQuantity
                ? entry.missingTotalQuantity
                : entry.missingIngredientTypes;

            if (score > 0 && score <= nearThreshold)
                nearRecipes.Add(entry);
        }

        SortEntries(craftableRecipes);
        SortEntries(nearRecipes);
        availabilityChanged.OnNext(Unit.Default);
    }

    private static void SortEntries(List<RecipeAvailabilityEntry> entries)
    {
        entries.Sort((a, b) =>
        {
            int miss = a.missingTotalQuantity.CompareTo(b.missingTotalQuantity);
            if (miss != 0)
                return miss;

            string aName = a.resultItem != null ? a.resultItem.itemName : string.Empty;
            string bName = b.resultItem != null ? b.resultItem.itemName : string.Empty;
            return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static Dictionary<Item, int> BuildAvailableCounts(InventoryData inventory)
    {
        var available = new Dictionary<Item, int>();
        if (inventory == null)
            return available;

        var slots = inventory.Items;
        for (int i = 0; i < slots.Count; i++)
        {
            InventoryItem slot = slots[i];
            if (slot == null || slot.itemData == null || slot.itemData.Type == ItemType.None)
                continue;

            int qty = Mathf.Max(0, slot.quantity != null ? slot.quantity.Value : 0);
            if (qty <= 0)
                continue;

            if (available.TryGetValue(slot.itemData, out int current))
                available[slot.itemData] = current + qty;
            else
                available[slot.itemData] = qty;
        }

        return available;
    }

    private static bool TryBuildEntry(
        Recipe recipe,
        Dictionary<Item, int> availableCounts,
        out RecipeAvailabilityEntry entry)
    {
        entry = null;
        if (recipe == null || recipe.result == null)
            return false;

        Item resultItem = recipe.result.itemData;
        if (resultItem == null || resultItem.Type == ItemType.None)
            return false;

        var requiredCounts = new Dictionary<Item, int>();
        var normalized = RecipeDatabase.NormalizeIngredients(recipe.ingredients);
        for (int i = 0; i < normalized.Count; i++)
        {
            InventoryItem req = normalized[i];
            Item reqItem = req != null ? req.itemData : null;
            int reqQty = req != null && req.quantity != null ? Mathf.Max(0, req.quantity.Value) : 0;
            if (reqItem == null || reqItem.Type == ItemType.None || reqQty <= 0)
                continue;

            if (requiredCounts.TryGetValue(reqItem, out int current))
                requiredCounts[reqItem] = current + reqQty;
            else
                requiredCounts[reqItem] = reqQty;
        }

        int missingTypes = 0;
        int missingQuantity = 0;
        foreach (var pair in requiredCounts)
        {
            int have = 0;
            availableCounts.TryGetValue(pair.Key, out have);
            int deficit = Mathf.Max(0, pair.Value - have);
            if (deficit <= 0)
                continue;

            missingTypes++;
            missingQuantity += deficit;
        }

        entry = new RecipeAvailabilityEntry
        {
            recipe = recipe,
            resultItem = resultItem,
            resultQuantity = Mathf.Max(0, recipe.result.quantity != null ? recipe.result.quantity.Value : 0),
            missingIngredientTypes = missingTypes,
            missingTotalQuantity = missingQuantity
        };
        return true;
    }

    private void TryResolveMainInventory()
    {
        if (InventoryController.Instance == null || InventoryController.Instance.InventoryUIManager == null)
            return;

        sourceInventory = InventoryController.Instance.InventoryUIManager.GetInventoryData(InventoryType.Inventory);
    }
}
