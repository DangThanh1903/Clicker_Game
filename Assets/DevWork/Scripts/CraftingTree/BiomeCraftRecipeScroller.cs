using System;
using System.Collections;
using System.Collections.Generic;
using EnhancedUI.EnhancedScroller;
using TMPro;
using UniRx;
using UnityEngine;

public class BiomeCraftRecipeScroller : MonoBehaviour, IEnhancedScrollerDelegate
{
    [Header("Scroller")]
    [SerializeField] private EnhancedScroller scroller;
    [SerializeField] private CraftRecipeListCellView cellViewPrefab;
    [SerializeField, Min(32f)] private float cellSize = 210f;

    [Header("Data")]
    [SerializeField] private RecipeDatabase recipeDatabase;
    [SerializeField] private CraftNodeManager nodeManagerOverride;
    [SerializeField] private bool autoBindCurrentBiome = true;
    [SerializeField] private bool showLockedRecipes = false;
    [SerializeField] private bool showFinishedRecipes = true;

    [Header("Actions")]
    [SerializeField] private CraftingController craftingController;
    [SerializeField] private InventoryUIManager inventoryUIManager;
    [SerializeField] private CraftRecipePanel detailPanel;
    [SerializeField] private TMP_Text emptyText;

    [Header("Legacy Tree Visual")]
    [Tooltip("Optional visual-only tree container to hide while this list is used. Do not assign an object that owns CraftNodeManager.")]
    [SerializeField] private GameObject legacyTreeVisualRoot;
    [SerializeField] private bool hideLegacyTreeVisual = true;

    private readonly List<CraftRecipeListEntry> entries = new List<CraftRecipeListEntry>();
    private readonly CompositeDisposable disposables = new CompositeDisposable();
    private CraftNodeManager boundNodeManager;
    private InventoryData boundInventory;
    private InventoryController boundInventoryController;
    private Coroutine pendingReloadRoutine;

    private void Awake()
    {
        if (scroller == null)
            scroller = GetComponentInChildren<EnhancedScroller>(true);
        if (cellViewPrefab == null)
            cellViewPrefab = GetComponentInChildren<CraftRecipeListCellView>(true);
    }

    private void OnEnable()
    {
        ResolveRefs();

        if (scroller != null)
            scroller.Delegate = this;

        if (hideLegacyTreeVisual && legacyTreeVisualRoot != null)
            legacyTreeVisualRoot.SetActive(false);

        BindLocationLoader();
        BindNodeManager(ResolveNodeManager());
        BindInventory(ResolveSourceInventory());
        Refresh();
    }

    private void OnDisable()
    {
        if (LocationLoader.Ins != null)
            LocationLoader.Ins.CurrentCraftNodeManagerChanged -= HandleCraftNodeManagerChanged;

        StopPendingReload();
        UnbindNodeManager();
        UnbindInventory();

        if (hideLegacyTreeVisual && legacyTreeVisualRoot != null)
            legacyTreeVisualRoot.SetActive(true);
    }

    public void Refresh()
    {
        ResolveRefs();

        if (boundNodeManager == null)
            BindNodeManager(ResolveNodeManager());
        if (boundInventory == null)
            BindInventory(ResolveSourceInventory());

        entries.Clear();

        CraftNodeManager manager = boundNodeManager;
        RecipeDatabase db = ResolveRecipeDatabase(manager);
        InventoryData inventory = boundInventory;

        if (manager != null && db != null)
        {
            for (int i = 0; i < manager.allNodes.Count; i++)
            {
                CraftNode node = manager.allNodes[i];
                if (node == null)
                    continue;

                if (!showLockedRecipes && node.State == CraftNodeState.Locked)
                    continue;
                if (!showFinishedRecipes && node.State == CraftNodeState.Finished)
                    continue;

                Recipe recipe = ResolveRecipe(db, node);
                if (recipe == null || recipe.result == null || recipe.result.itemData == null)
                    continue;

                entries.Add(BuildEntry(node, recipe, inventory));
            }
        }

        SortEntries();

        if (emptyText != null)
            emptyText.gameObject.SetActive(entries.Count == 0);

        ReloadScrollerSafely();
    }

    public int GetNumberOfCells(EnhancedScroller scroller)
    {
        return entries.Count;
    }

    public float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
    {
        return cellSize;
    }

    public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
    {
        var cellView = scroller.GetCellView(cellViewPrefab) as CraftRecipeListCellView;
        if (cellView == null)
            return null;

        cellView.gameObject.SetActive(true);
        cellView.name = $"Recipe Cell {dataIndex}";
        cellView.SetData(entries[dataIndex], HandleCraftClicked, HandleRecipeSelected);
        return cellView;
    }

    private CraftRecipeListEntry BuildEntry(CraftNode node, Recipe recipe, InventoryData inventory)
    {
        Item resultItem = recipe.result.itemData;
        int resultQuantity = recipe.result.quantity != null ? Mathf.Max(0, recipe.result.quantity.Value) : 0;
        bool hasIngredients = inventory != null && RecipeInventoryUtility.HasIngredients(recipe, inventory);
        bool canAddResult = InventoryController.Instance != null &&
                            resultItem != null &&
                            InventoryController.Instance.CanFullyAddItem(resultItem, resultQuantity);

        return new CraftRecipeListEntry
        {
            node = node,
            recipe = recipe,
            resultItem = resultItem,
            resultQuantity = resultQuantity,
            statLine = BuildStatLine(resultItem),
            ingredients = RecipeInventoryUtility.BuildIngredientStatuses(recipe, inventory),
            canCraft = node.State != CraftNodeState.Locked && hasIngredients,
            canAddResult = canAddResult
        };
    }

    private void HandleRecipeSelected(CraftRecipeListEntry entry)
    {
        if (entry == null || detailPanel == null)
            return;

        detailPanel.ShowRecipe(entry.recipe);
    }

    private void HandleCraftClicked(CraftRecipeListEntry entry)
    {
        if (entry == null || entry.recipe == null)
            return;

        ResolveRefs();
        InventoryData inventory = boundInventory ?? ResolveSourceInventory();
        if (craftingController == null || inventory == null)
            return;

        if (craftingController.TryCraftRecipe(entry.recipe, inventory))
            Refresh();
    }

    private void BindLocationLoader()
    {
        if (!autoBindCurrentBiome || LocationLoader.Ins == null)
            return;

        LocationLoader.Ins.CurrentCraftNodeManagerChanged -= HandleCraftNodeManagerChanged;
        LocationLoader.Ins.CurrentCraftNodeManagerChanged += HandleCraftNodeManagerChanged;
    }

    private void HandleCraftNodeManagerChanged(CraftNodeManager manager)
    {
        BindNodeManager(manager);
        Refresh();
    }

    private void BindNodeManager(CraftNodeManager manager)
    {
        if (boundNodeManager == manager)
            return;

        UnbindNodeManager();
        boundNodeManager = manager;

        if (boundNodeManager == null)
            return;

        boundNodeManager.OnNodeUnlocked += HandleNodeStateChanged;
        boundNodeManager.OnNodeFinished += HandleNodeStateChanged;
    }

    private void UnbindNodeManager()
    {
        if (boundNodeManager == null)
            return;

        boundNodeManager.OnNodeUnlocked -= HandleNodeStateChanged;
        boundNodeManager.OnNodeFinished -= HandleNodeStateChanged;
        boundNodeManager = null;
    }

    private void HandleNodeStateChanged(CraftNode _)
    {
        Refresh();
    }

    private void BindInventory(InventoryData inventory)
    {
        if (boundInventory == inventory)
            return;

        UnbindInventory();
        boundInventory = inventory;

        boundInventoryController = InventoryController.Instance;
        if (boundInventoryController != null)
        {
            boundInventoryController.OnMainInventoryItemAdded += HandleInventoryChanged;
        }

        if (boundInventory == null)
            return;

        boundInventory.InventoryChanged
            .ThrottleFrame(1)
            .Subscribe(_ => Refresh())
            .AddTo(disposables);

        boundInventory.Items.ObserveReplace()
            .ThrottleFrame(1)
            .Subscribe(_ => Refresh())
            .AddTo(disposables);
    }

    private void UnbindInventory()
    {
        disposables.Clear();

        if (boundInventoryController != null)
            boundInventoryController.OnMainInventoryItemAdded -= HandleInventoryChanged;

        boundInventoryController = null;
        boundInventory = null;
    }

    private void HandleInventoryChanged(Item _, int __)
    {
        Refresh();
    }

    private CraftNodeManager ResolveNodeManager()
    {
        if (nodeManagerOverride != null)
            return nodeManagerOverride;

        if (autoBindCurrentBiome && LocationLoader.Ins != null)
            return LocationLoader.Ins.CurrentCraftNodeManager;

        return FindFirstObjectByType<CraftNodeManager>(FindObjectsInactive.Include);
    }

    private InventoryData ResolveSourceInventory()
    {
        ResolveRefs();
        return inventoryUIManager != null
            ? inventoryUIManager.GetInventoryData(InventoryType.Inventory)
            : null;
    }

    private RecipeDatabase ResolveRecipeDatabase(CraftNodeManager manager)
    {
        if (recipeDatabase != null)
            return recipeDatabase;
        if (detailPanel != null && detailPanel.recipeDB != null)
            return detailPanel.recipeDB;

        if (manager != null)
        {
            for (int i = 0; i < manager.allNodes.Count; i++)
            {
                CraftNode node = manager.allNodes[i];
                if (node != null && node.recipePanel != null && node.recipePanel.recipeDB != null)
                    return node.recipePanel.recipeDB;
            }
        }

        return null;
    }

    private Recipe ResolveRecipe(RecipeDatabase db, CraftNode node)
    {
        if (db == null || node == null)
            return null;

        Item targetItem = node.GetPrimaryRecipeItem();
        if (targetItem != null)
        {
            var recipes = db.GetRecipesByResultItem(targetItem);
            if (recipes.Count > 0)
                return recipes[0];
        }

        return db.FindFirstRecipeByResultName(node.nodeName);
    }

    private void SortEntries()
    {
        entries.Sort((a, b) =>
        {
            int craftable = b.canCraft.CompareTo(a.canCraft);
            if (craftable != 0)
                return craftable;

            int locked = a.IsLocked.CompareTo(b.IsLocked);
            if (locked != 0)
                return locked;

            string aName = a.resultItem != null ? a.resultItem.itemName : string.Empty;
            string bName = b.resultItem != null ? b.resultItem.itemName : string.Empty;
            return string.Compare(aName, bName, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void ResolveRefs()
    {
        if (craftingController == null && InventoryController.Instance != null)
            craftingController = InventoryController.Instance.CraftingController;
        if (inventoryUIManager == null && InventoryController.Instance != null)
            inventoryUIManager = InventoryController.Instance.InventoryUIManager;
    }

    private void ReloadScrollerSafely()
    {
        if (scroller == null)
            return;

        if (IsScrollerReady())
        {
            StopPendingReload();
            scroller.ReloadData();
            return;
        }

        if (pendingReloadRoutine == null && isActiveAndEnabled)
            pendingReloadRoutine = StartCoroutine(ReloadScrollerWhenReady_Co());
    }

    private bool IsScrollerReady()
    {
        return scroller != null &&
               scroller.ScrollRect != null &&
               scroller.Container != null;
    }

    private IEnumerator ReloadScrollerWhenReady_Co()
    {
        const int maxFrames = 8;

        for (int i = 0; i < maxFrames; i++)
        {
            yield return null;

            if (!isActiveAndEnabled)
            {
                pendingReloadRoutine = null;
                yield break;
            }

            if (!IsScrollerReady())
                continue;

            pendingReloadRoutine = null;
            scroller.ReloadData();
            yield break;
        }

        pendingReloadRoutine = null;
        Debug.LogWarning("[BiomeCraftRecipeScroller] EnhancedScroller was not ready to reload.", this);
    }

    private void StopPendingReload()
    {
        if (pendingReloadRoutine == null)
            return;

        StopCoroutine(pendingReloadRoutine);
        pendingReloadRoutine = null;
    }

    private static string BuildStatLine(Item item)
    {
        if (item == null)
            return string.Empty;

        if (item is IStatProvider provider)
        {
            foreach (var modifier in provider.GetStatModifiers())
                return FormatShortModifier(modifier);
        }

        if (!string.IsNullOrWhiteSpace(item.description))
            return FirstLine(item.description);

        return item.Type.ToString();
    }

    private static string FormatShortModifier(StatModifier modifier)
    {
        string statName = modifier.statType switch
        {
            StatType.NormalPower => "Damage",
            StatType.HoldPower => "Damage",
            StatType.IdlePower => "Damage",
            _ => modifier.statType.ToString()
        };

        if (modifier.mode == StatModifierMode.Multiply)
            return $"x{modifier.value:0.###} {statName}";

        string sign = modifier.value >= 0f ? "+" : "-";
        return $"{sign}{Mathf.Abs(modifier.value):0.###} {statName}";
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        using (var reader = new System.IO.StringReader(text.Trim()))
            return reader.ReadLine();
    }
}
