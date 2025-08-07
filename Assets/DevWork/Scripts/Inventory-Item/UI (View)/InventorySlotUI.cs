using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UniRx;

public class InventorySlotUI : MonoBehaviour, IDropHandler
{
    [Header("Slot Logic")]
    public SlotAcceptRuleSO acceptRule;
    public int slotIndex;

    [Header("UI References")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI quantityText;

    private InventoryData boundInventory;
    private CompositeDisposable disposables = new CompositeDisposable();

    public void Bind(InventoryData inventoryData, int index)
    {
        disposables.Clear();
        slotIndex = index;
        boundInventory = inventoryData;

        UpdateSlotUI(boundInventory.Items[slotIndex]);

        boundInventory.Items
            .ObserveReplace()
            .Where(x => x.Index == slotIndex)
            .Subscribe(x => UpdateSlotUI(x.NewValue))
            .AddTo(disposables);

        boundInventory.Items
            .ObserveReset()
            .Subscribe(_ => UpdateSlotUI(boundInventory.Items[slotIndex]))
            .AddTo(disposables);
    }


    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log("Got something dropped in");
        var droppedItem = eventData.pointerDrag.GetComponent<DragableItem>();
        if (droppedItem == null)
            return;
        var droppedSlot = droppedItem.originSlot.GetComponent<InventorySlotUI>();
        if (droppedSlot == null || acceptRule == null || droppedSlot.acceptRule == null)
        {
            Debug.Log("Missing something");
            return;
        }

        InventoryController.Instance.TrySwap(
            droppedSlot.boundInventory, droppedSlot.slotIndex,
            this.boundInventory, this.slotIndex,
            droppedSlot.acceptRule, this.acceptRule);
    }

    protected void UpdateSlotUI(InventoryItem item)
    {
        bool hasItem = item != null && item.itemData != null && item.itemData.Type != ItemType.None;

        icon.enabled = hasItem;
        icon.sprite = hasItem ? item.itemData.icon : null;

        if (quantityText == null)
            return;

        if (hasItem && item.quantity.Value > 1)
            quantityText.text = item.quantity.Value.ToString();
        else
            quantityText.text = "";
    }

    private void OnDestroy()
    {
        disposables.Dispose();
    }
}
