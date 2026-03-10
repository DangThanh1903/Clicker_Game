using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using UnityEngine.UI;
using TMPro;

public enum CraftNodeState
{
    Locked,
    Unlocked,
    Finished
}

public class CraftNode : MonoBehaviour, IPointerClickHandler
{
    public string nodeName;
    public List<CraftNode> requiredNodes; // other nodes that must be finished first
    public List<Item> requiredItems;      // ingredients needed for this node
    public CraftNodeState State = CraftNodeState.Locked;
    public Image ReqUIImage;
    public Image LockedImage;
    public TMP_Text DoneText;
    private List<InventoryData> inventoryDependencies = new List<InventoryData>();
    private CompositeDisposable disposables = new CompositeDisposable();
    public Subject<Unit> OnNodeFinished = new Subject<Unit>();
    public System.Action<CraftNodeState, CraftNodeState> OnStateChanged;

    [Header("Show Recipe")]
    public CraftRecipePanel recipePanel;

    void Awake()
    {
        recipePanel = GetComponentInParent<CraftRecipePanel>();
    }

    void Start()
    {
        UpdateRequirementIcon();
    }

    public void Init(List<InventoryData> inventories)
    {
        inventoryDependencies = inventories ?? new List<InventoryData>();

        // Subscribe to item changes
        foreach (var inv in EnumerateValidInventories())
        {
            inv.InventoryChanged
               .Where(item => item != null && requiredItems != null && requiredItems.Contains(item.itemData))
               .ThrottleFrame(1) // reduce update frequency
               .Subscribe(_ => CheckState(false))
               .AddTo(disposables);
        }

        foreach (var reqNode in EnumerateValidRequiredNodes())
        {
            reqNode.OnNodeFinished
                .ThrottleFrame(1)
                .Subscribe(_ => CheckState(false))  // automatically recheck state
                .AddTo(disposables);
        }

        // Check state initially
        CheckState(true);
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (State == CraftNodeState.Locked)
            return;
        var targetItem = GetPrimaryRecipeItem();
        if (recipePanel && targetItem)
        {
            recipePanel.ShowForItem(targetItem);
        }
    }

    private bool AreRequiredNodesFinished()
    {
        foreach (var node in EnumerateValidRequiredNodes())
        {
            if (node.State != CraftNodeState.Finished)
                return false;
        }

        return true;
    }

    private bool AreRequiredItemsPresent()
    {
        foreach (var item in EnumerateRequiredItems())
        {
            bool found = false;
            foreach (var inv in EnumerateValidInventories())
            {
                if (inv.HasItem(item, 1))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                return false;
        }

        return true;
    }

    private void CheckState(bool isInitial)
    {
        if (State == CraftNodeState.Finished)
            return;

        CraftNodeState nextState;
        if (!AreRequiredNodesFinished())
            nextState = CraftNodeState.Locked;
        else
            nextState = CraftNodeState.Unlocked;

        if (nextState != State)
        {
            var previous = State;
            State = nextState;

            if (State == CraftNodeState.Unlocked && previous != CraftNodeState.Unlocked && !isInitial)
            {
                Item item = GetPrimaryRecipeItem();
                string displayName = item != null ? item.GetColoredName() : nodeName;
                if (!string.IsNullOrWhiteSpace(displayName))
                    GameDebugHandler.LogStaticAfter($"Unlocked {displayName}'s recipe!");
            }

            UpdateVisual();
            OnStateChanged?.Invoke(previous, State);
        }

        if (State == CraftNodeState.Unlocked && AreRequiredItemsPresent())
            TryFinishNode();
    }

    public void RecheckState(bool isInitial = false)
    {
        CheckState(isInitial);
    }

    public void FinishNode() // Called by player click
    {
        TryFinishNode();
    }

    private bool TryFinishNode()
    {
        if (State != CraftNodeState.Unlocked || !AreRequiredItemsPresent())
            return false;

        var previous = State;
        State = CraftNodeState.Finished;

        UpdateVisual();
        OnNodeFinished.OnNext(Unit.Default);
        OnStateChanged?.Invoke(previous, State);
        return true;
    }

    public void UpdateVisual()
    {
        switch (State)
        {
            case CraftNodeState.Locked:
                SetGraphicActive(LockedImage, true);
                SetGraphicActive(DoneText, false);
                break;
            case CraftNodeState.Unlocked:
                SetGraphicActive(LockedImage, false);
                SetGraphicActive(DoneText, false);
                break;
            case CraftNodeState.Finished:
                SetGraphicActive(LockedImage, false);
                SetGraphicActive(DoneText, true);
                break;
        }
    }

    private static void SetGraphicActive(Graphic graphic, bool active)
    {
        if (graphic == null)
            return;

        graphic.enabled = active;
        graphic.raycastTarget = active;
    }

    private IEnumerable<CraftNode> EnumerateValidRequiredNodes()
    {
        if (requiredNodes == null)
            yield break;

        foreach (var node in requiredNodes)
        {
            if (node == null || node == this)
                continue;

            if (!node.gameObject.scene.IsValid())
                continue;

            yield return node;
        }
    }

    private IEnumerable<InventoryData> EnumerateValidInventories()
    {
        if (inventoryDependencies == null)
            yield break;

        foreach (var inv in inventoryDependencies)
        {
            if (inv != null)
                yield return inv;
        }
    }

    private IEnumerable<Item> EnumerateRequiredItems()
    {
        if (requiredItems == null)
            yield break;

        foreach (var item in requiredItems)
        {
            if (item != null && item.Type != ItemType.None)
                yield return item;
        }
    }

    public Item GetPrimaryRecipeItem()
    {
        if (recipePanel != null && recipePanel.recipeDB != null && !string.IsNullOrWhiteSpace(nodeName))
        {
            Recipe recipe = recipePanel.recipeDB.FindFirstRecipeByResultName(nodeName);
            if (recipe != null && recipe.result != null && recipe.result.itemData != null && recipe.result.itemData.Type != ItemType.None)
                return recipe.result.itemData;
        }

        return GetPrimaryRequiredItem();
    }

    private Item GetPrimaryRequiredItem()
    {
        foreach (var item in EnumerateRequiredItems())
            return item;

        return null;
    }

    private void UpdateRequirementIcon()
    {
        if (ReqUIImage == null)
            return;

        var item = GetPrimaryRecipeItem();
        ReqUIImage.sprite = item != null ? item.icon : null;
        ReqUIImage.color = item != null ? Color.white : new Color(1f, 1f, 1f, 0f);
    }

    private void OnDestroy()
    {
        disposables.Dispose();
    }
}
