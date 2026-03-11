using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UniRx;

public class InventorySlotUI : MonoBehaviour, IDropHandler
{
    [Header("Slot Logic")]
    [SerializeField] private SlotAcceptRuleSO acceptRule;
    [SerializeField] private int slotIndex;
    private InventoryData boundInventory;

    [Header("UI References")]
    [SerializeField] private GameObject draggable;
    [SerializeField] private TextMeshProUGUI quantityText;
    private Image iconImage;
    private DragableItem dragItem;
    private GameObject cachedDraggable;

    private CompositeDisposable disposable = new CompositeDisposable();

    private void Awake()
    {
        EnsureCache();
    }

    public void Bind(InventoryData inventory, int index)
    {
        boundInventory = inventory;
        slotIndex = index;
    }

    public InventoryData GetBoundInventory() => boundInventory;
    public int GetSlotIndex() => slotIndex;
    public SlotAcceptRuleSO GetAcceptRule() => acceptRule;
    public void SetAcceptRule(SlotAcceptRuleSO rule) => acceptRule = rule;

    public void UpdateSlotUI(InventoryItem item)
    {
        disposable.Clear();
        EnsureCache();

        bool hasItem = item != null && item.itemData != null && item.itemData.Type != ItemType.None;
        if (iconImage != null)
        {
            iconImage.enabled = hasItem;
            iconImage.sprite = hasItem ? item.itemData.icon : null;
        }
        dragItem?.SetInventoryItem(item, slotIndex, boundInventory);

        if (hasItem)
        {
            item.quantity
                .Subscribe(qty =>
                {
                    if (quantityText != null)
                    {
                        quantityText.text = qty > 1 ? qty.ToString() : "";
                    }
                })
                .AddTo(disposable);
        }
        else
        {
            if (quantityText != null)
                quantityText.text = "";
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        var droppedItem = eventData.pointerDrag.GetComponent<DragableItem>();
        if (droppedItem == null) return;

        var droppedSlot = droppedItem.originSlot.GetComponent<InventorySlotUI>();
        if (droppedSlot == null || acceptRule == null || droppedSlot.acceptRule == null)
        {
            return;
        }

        InventoryController.Instance.TrySwap(
            fromData: droppedSlot.GetBoundInventory(),
            fromIndex: droppedSlot.slotIndex,
            toData: this.GetBoundInventory(),
            toIndex: this.slotIndex,
            fromRule: droppedSlot.acceptRule,
            toRule: this.acceptRule
        );

    }

    private void OnDestroy()
    {
        disposable.Dispose();
    }

    private void EnsureCache()
    {
        if (draggable == null)
        {
            iconImage = null;
            dragItem = null;
            cachedDraggable = null;
            return;
        }

        if (cachedDraggable != draggable || iconImage == null || dragItem == null)
        {
            cachedDraggable = draggable;
            iconImage = draggable.GetComponent<Image>();
            dragItem = draggable.GetComponent<DragableItem>();
        }
    }
}
