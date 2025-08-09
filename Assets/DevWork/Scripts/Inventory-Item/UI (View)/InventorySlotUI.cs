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
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI quantityText;

    private CompositeDisposable disposable = new CompositeDisposable();

    public void Bind(InventoryData inventory, int index)
    {
        boundInventory = inventory;
        slotIndex = index;
    }

    public InventoryData GetBoundInventory() => boundInventory;
    public int GetSlotIndex() => slotIndex;

    public void UpdateSlotUI(InventoryItem item)
    {
        disposable.Clear();

        bool hasItem = item != null && item.itemData != null && item.itemData.Type != ItemType.None;

        icon.enabled = hasItem;
        icon.sprite = hasItem ? item.itemData.icon : null;

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
        Debug.Log("Got something dropped in");
        var droppedItem = eventData.pointerDrag.GetComponent<DragableItem>();
        if (droppedItem == null) return;

        var droppedSlot = droppedItem.originSlot.GetComponent<InventorySlotUI>();
        if (droppedSlot == null || acceptRule == null || droppedSlot.acceptRule == null)
        {
            Debug.Log("Missing something");
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
}
