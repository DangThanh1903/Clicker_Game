using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UniRx;
using DG.Tweening;

public class InventorySlotUI : MonoBehaviour, IDropHandler
{
    [Header("Slot Logic")]
    [SerializeField] private SlotAcceptRuleSO acceptRule;
    [SerializeField] private int slotIndex;
    private InventoryData boundInventory;

    [Header("UI References")]
    [SerializeField] private GameObject draggable;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Image slotBackgroundImage;
    [SerializeField] private Color equippedWeaponColor = new Color(1f, 0.9f, 0.45f, 1f);
    [Header("Drag Visual")]
    [SerializeField, Min(1f)] private float dragPulseScale = 1.06f;
    [SerializeField, Min(1f)] private float dragNearBaseScale = 1.14f;
    [SerializeField, Min(1f)] private float dragNearPulseScale = 1.22f;
    [SerializeField, Min(0.05f)] private float dragPulseDuration = 0.32f;
    private Image iconImage;
    private DragableItem dragItem;
    private GameObject cachedDraggable;
    private RectTransform draggableRect;
    private RectTransform slotRect;
    private Color defaultSlotColor = Color.white;
    private bool hasDefaultSlotColor;
    private Vector3 draggableBaseScale = Vector3.one;
    private bool hasDraggableBaseScale;
    private Tween dragPulseTween;
    private bool mergeCandidateVisualActive;
    private bool mergeCandidateNearState;

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
            ClearMergeCandidateVisual();
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
        dragPulseTween?.Kill(false);
        dragPulseTween = null;
        disposable.Dispose();
    }

    private void OnDisable()
    {
        ClearMergeCandidateVisual();
    }

    public bool IsMergeCandidateFor(Item draggedItem)
    {
        if (draggedItem == null || draggedItem.Type == ItemType.None)
            return false;
        if (boundInventory == null || boundInventory.inventoryType != InventoryType.Inventory)
            return false;

        InventoryItem slotItem = GetCurrentItem();
        if (slotItem == null || slotItem.itemData == null || slotItem.itemData.Type == ItemType.None)
            return false;
        if (slotItem.quantity == null || slotItem.quantity.Value <= 0)
            return false;

        return ReferenceEquals(slotItem.itemData, draggedItem);
    }

    public RectTransform GetDragVisualRect()
    {
        EnsureCache();
        return slotRect != null ? slotRect : draggableRect;
    }

    public void SetMergeCandidateVisual(bool isNearCandidate)
    {
        EnsureCache();
        if (draggableRect == null || !HasVisibleItem())
        {
            ClearMergeCandidateVisual();
            return;
        }

        if (!hasDraggableBaseScale)
        {
            draggableBaseScale = draggableRect.localScale;
            hasDraggableBaseScale = true;
        }

        if (mergeCandidateVisualActive && mergeCandidateNearState == isNearCandidate)
            return;

        mergeCandidateVisualActive = true;
        mergeCandidateNearState = isNearCandidate;
        StartPulseTween(isNearCandidate);
    }

    public void ClearMergeCandidateVisual()
    {
        mergeCandidateVisualActive = false;
        mergeCandidateNearState = false;
        dragPulseTween?.Kill(false);
        dragPulseTween = null;

        if (draggableRect != null && hasDraggableBaseScale)
            draggableRect.localScale = draggableBaseScale;
    }

    public void SetEquippedWeaponVisual(bool isEquippedWeapon)
    {
        if (slotBackgroundImage == null)
            return;

        if (!hasDefaultSlotColor)
        {
            defaultSlotColor = slotBackgroundImage.color;
            hasDefaultSlotColor = true;
        }

        slotBackgroundImage.color = isEquippedWeapon ? equippedWeaponColor : defaultSlotColor;
    }

    private void EnsureCache()
    {
        if (slotRect == null)
            slotRect = transform as RectTransform;

        if (slotBackgroundImage == null)
            slotBackgroundImage = GetComponent<Image>();

        if (draggable == null)
        {
            iconImage = null;
            dragItem = null;
            cachedDraggable = null;
            draggableRect = null;
            hasDraggableBaseScale = false;
            return;
        }

        if (cachedDraggable != draggable || iconImage == null || dragItem == null || draggableRect == null)
        {
            cachedDraggable = draggable;
            iconImage = draggable.GetComponent<Image>();
            dragItem = draggable.GetComponent<DragableItem>();
            draggableRect = draggable.GetComponent<RectTransform>();
            hasDraggableBaseScale = draggableRect != null;
            draggableBaseScale = hasDraggableBaseScale ? draggableRect.localScale : Vector3.one;
        }
    }

    private InventoryItem GetCurrentItem()
    {
        if (boundInventory == null || boundInventory.Items == null)
            return null;
        if (slotIndex < 0 || slotIndex >= boundInventory.Items.Count)
            return null;
        return boundInventory.Items[slotIndex];
    }

    private bool HasVisibleItem()
    {
        InventoryItem item = GetCurrentItem();
        if (item == null || item.itemData == null || item.itemData.Type == ItemType.None)
            return false;
        if (item.quantity == null || item.quantity.Value <= 0)
            return false;
        return true;
    }

    private void StartPulseTween(bool isNearCandidate)
    {
        if (draggableRect == null)
            return;

        float baseScaleMul = 1f;
        float pulseScaleMul = Mathf.Max(1f, dragPulseScale);
        if (isNearCandidate)
        {
            baseScaleMul = Mathf.Max(1f, dragNearBaseScale);
            pulseScaleMul = Mathf.Max(baseScaleMul, dragNearPulseScale);
        }

        Vector3 from = draggableBaseScale * baseScaleMul;
        Vector3 to = draggableBaseScale * pulseScaleMul;

        dragPulseTween?.Kill(false);
        draggableRect.localScale = from;
        dragPulseTween = draggableRect
            .DOScale(to, Mathf.Max(0.05f, dragPulseDuration))
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }
}
