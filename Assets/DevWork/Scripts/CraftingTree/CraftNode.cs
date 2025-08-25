using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;
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

    [Header("Show Recipe")]
    public CraftRecipePanel recipePanel;

    void Awake()
    {
        recipePanel = GetComponentInParent<CraftRecipePanel>();
    }
    void Start()
    {
        ReqUIImage.sprite = requiredItems[0].icon;
    }

    public void Init(List<InventoryData> inventories)
    {
        inventoryDependencies = inventories;

        // Subscribe to item changes
        foreach (var inv in inventoryDependencies)
        {
            inv.InventoryChanged
               .Where(item => requiredItems.Contains(item.itemData))
               .ThrottleFrame(1) // reduce update frequency
               .Subscribe(_ => CheckState())
               .AddTo(disposables);
        }

        foreach (var reqNode in requiredNodes)
        {
            reqNode.OnNodeFinished
                .ThrottleFrame(1)
                .Subscribe(_ => CheckState())  // automatically recheck state
                .AddTo(disposables);
        }

        // Check state initially
        CheckState();
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (State == CraftNodeState.Locked)
            return;
        var targetItem = (requiredItems != null && requiredItems.Count > 0) ? requiredItems[0] : null;
        if (recipePanel && targetItem)
        {
            recipePanel.ShowForItem(targetItem);
        }
    }

    private bool AreRequiredNodesFinished()
    {
        return requiredNodes.All(n => n.State == CraftNodeState.Finished);
    }

    private bool AreRequiredItemsPresent()
    {
        return requiredItems.All(item =>
            inventoryDependencies.Any(inv => inv.HasItem(item, 1))
        );
    }

    private void CheckState()
    {
        if (State == CraftNodeState.Finished)
            return;

        if (!AreRequiredNodesFinished())
        {
            State = CraftNodeState.Locked;
        }
        else if (AreRequiredItemsPresent())
        {
            State = CraftNodeState.Finished;
            OnNodeFinished.OnNext(Unit.Default);
        }
        else
        {
            State = CraftNodeState.Unlocked;
        }

        UpdateVisual();
    }

    public void FinishNode() // Called by player click
    {
        if (State == CraftNodeState.Unlocked && AreRequiredItemsPresent())
        {
            State = CraftNodeState.Finished;

            // Remove items from inventories
            foreach (var item in requiredItems)
            {
                foreach (var inv in inventoryDependencies)
                {
                    if (inv.HasItem(item, 1))
                    {
                        for (int i = 0; i < inv.GetSize(); i++)
                        {
                            if (inv.GetItem(i).itemData == item)
                            {
                                inv.SubtractQuantity(i, 1, true);
                                break;
                            }
                        }
                        break;
                    }
                }
            }

            UpdateVisual();
        }
    }

    public void UpdateVisual()
    {
        var Image = GetComponent<Image>();

        switch (State)
        {
            case CraftNodeState.Locked:
                LockedImage.gameObject.SetActive(true);
                DoneText.gameObject.SetActive(false);
                break;
            case CraftNodeState.Unlocked:
                LockedImage.gameObject.SetActive(false);
                DoneText.gameObject.SetActive(false);
                break;
            case CraftNodeState.Finished:
                LockedImage.gameObject.SetActive(false);
                DoneText.gameObject.SetActive(true);
                break;
        }
    }

    private void OnDestroy()
    {
        disposables.Dispose();
    }
}
