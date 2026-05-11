using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

public class DragableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [HideInInspector] public Transform originSlot;
    [Header("Drag Visual")]
    [SerializeField, Min(1f)] private float dragLiftScale = 1.1f;
    [SerializeField, Min(1f)] private float dragLiftPulseScale = 1.15f;
    [SerializeField, Min(0.01f)] private float dragLiftDuration = 0.08f;
    [SerializeField, Range(0.1f, 1f)] private float nearCandidateSlotSizeMultiplier = 1f;

    private Image image;
    private RectTransform rectTransform;
    private float lastClickTime = 0f;
    private InventoryItem inventoryItem;
    private int currenIndex;
    private InventoryData currentData;
    private Tween dragLiftTween;
    private Camera dragEventCamera;
    private bool isDraggingActive;
    private Vector3 baseScale = Vector3.one;
    private readonly List<InventorySlotUI> mergeCandidates = new List<InventorySlotUI>(32);
    private readonly Vector3[] slotWorldCorners = new Vector3[4];
    private const float doubleClickThreshold = 0.3f;

    void Awake()
    {
        image = GetComponent<Image>();
        rectTransform = transform as RectTransform;
        if (rectTransform != null)
            baseScale = rectTransform.localScale;

        var tmp = GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.raycastTarget = false;
        }

    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanStartDrag())
            return;

        isDraggingActive = true;
        originSlot = transform.parent;
        dragEventCamera = eventData != null ? eventData.pressEventCamera : null;

        transform.SetParent(transform.root, worldPositionStays: true);
        transform.SetAsLastSibling();

        if (image != null)
            image.raycastTarget = false;

        PlayDragLiftVisual();
        CollectMergeCandidates();
        Vector2 pointerPosition = eventData != null ? eventData.position : (Vector2)Input.mousePosition;
        UpdateMergeCandidateVisuals(pointerPosition);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDraggingActive)
            return;

        Vector2 pointerPosition = eventData != null ? eventData.position : (Vector2)Input.mousePosition;
        transform.position = pointerPosition;
        UpdateMergeCandidateVisuals(pointerPosition);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDraggingActive)
            return;

        isDraggingActive = false;
        ClearMergeCandidateVisuals();
        PlayDragDropBackVisual();

        if (originSlot != null)
        {
            transform.SetParent(originSlot);
            transform.localPosition = Vector3.zero;
        }

        if (image != null)
            image.raycastTarget = true;

        dragEventCamera = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        float timeSinceLastClick = Time.unscaledTime - lastClickTime;

        if (timeSinceLastClick <= doubleClickThreshold)
        {
            OnDoubleTap();
            lastClickTime = 0f;
        }
        else
        {
            lastClickTime = Time.unscaledTime;
            OnSingleTap();
        }
    }
    public void SetInventoryItem(InventoryItem it, int index, InventoryData inventoryData)
    {
        inventoryItem = it;
        currenIndex = index;
        currentData = inventoryData;
    }

    private void OnSingleTap()
    {
        if (currentData != null && currentData.inventoryType == InventoryType.CraftingOut)
        {
            var crafting = InventoryController.Instance != null
                ? InventoryController.Instance.CraftingController
                : null;
            crafting?.TryClaimCraftOutput();
            return;
        }

        InventoryController.Instance.GoToStatsPage();
        InventoryController.Instance.SetItemDescription(inventoryItem);
        if (inventoryItem != null && inventoryItem.itemData != null && inventoryItem.itemData.Type != ItemType.None)
            InventoryController.Instance.SetUseButton(inventoryItem.itemData, currenIndex, currentData);
    }

    private void OnDoubleTap()
    {
        // Your double tap logic here
    }

    private void OnDisable()
    {
        AbortDragVisualState();
    }

    private void OnDestroy()
    {
        AbortDragVisualState();
    }

    private bool CanStartDrag()
    {
        if (inventoryItem == null || inventoryItem.itemData == null)
            return false;
        if (inventoryItem.itemData.Type == ItemType.None)
            return false;
        if (inventoryItem.quantity == null || inventoryItem.quantity.Value <= 0)
            return false;
        return true;
    }

    private void PlayDragLiftVisual()
    {
        if (rectTransform == null)
            return;

        float liftScale = Mathf.Max(1f, dragLiftScale);
        float liftPulseScale = Mathf.Max(liftScale, dragLiftPulseScale);

        dragLiftTween?.Kill(false);
        rectTransform.localScale = baseScale * liftScale;
        dragLiftTween = rectTransform
            .DOScale(baseScale * liftPulseScale, Mathf.Max(0.01f, dragLiftDuration))
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void PlayDragDropBackVisual()
    {
        if (rectTransform == null)
            return;

        dragLiftTween?.Kill(false);
        dragLiftTween = rectTransform
            .DOScale(baseScale, Mathf.Max(0.01f, dragLiftDuration))
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private void CollectMergeCandidates()
    {
        mergeCandidates.Clear();

        if (currentData == null || currentData.inventoryType != InventoryType.Inventory)
            return;
        if (inventoryItem == null || inventoryItem.itemData == null || inventoryItem.itemData is not Pickaxe)
            return;

        InventoryUIManager uiManager = InventoryController.Instance != null
            ? InventoryController.Instance.InventoryUIManager
            : null;
        if (uiManager == null || uiManager.inventorySections == null)
            return;

        for (int s = 0; s < uiManager.inventorySections.Length; s++)
        {
            InventorySection section = uiManager.inventorySections[s];
            if (section == null || section.inventoryData == null || section.slotUIs == null)
                continue;
            if (section.inventoryData.inventoryType != InventoryType.Inventory)
                continue;

            for (int i = 0; i < section.slotUIs.Count; i++)
            {
                InventorySlotUI slot = section.slotUIs[i];
                if (slot == null)
                    continue;
                if (originSlot != null && ReferenceEquals(slot.transform, originSlot))
                    continue;
                if (!slot.IsMergeCandidateFor(inventoryItem.itemData))
                    continue;

                mergeCandidates.Add(slot);
            }
        }
    }

    private void UpdateMergeCandidateVisuals(Vector2 pointerPosition)
    {
        if (mergeCandidates.Count == 0)
            return;

        InventorySlotUI nearCandidate = ResolveNearCandidate(pointerPosition);
        for (int i = 0; i < mergeCandidates.Count; i++)
        {
            InventorySlotUI slot = mergeCandidates[i];
            if (slot == null)
                continue;
            slot.SetMergeCandidateVisual(ReferenceEquals(slot, nearCandidate));
        }
    }

    private InventorySlotUI ResolveNearCandidate(Vector2 pointerPosition)
    {
        float bestScore = float.MaxValue;
        InventorySlotUI bestSlot = null;

        for (int i = 0; i < mergeCandidates.Count; i++)
        {
            InventorySlotUI slot = mergeCandidates[i];
            if (slot == null)
                continue;

            RectTransform targetRect = slot.GetDragVisualRect();
            if (targetRect == null)
                continue;

            Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(dragEventCamera, targetRect.position);
            float nearDistancePx = GetNearDistanceBySlotSize(targetRect);
            if (nearDistancePx <= 0f)
                continue;

            float distanceSq = (slotScreenPos - pointerPosition).sqrMagnitude;
            float nearDistanceSq = nearDistancePx * nearDistancePx;
            if (distanceSq > nearDistanceSq)
                continue;

            float score = distanceSq / nearDistanceSq;
            if (score >= bestScore)
                continue;

            bestScore = score;
            bestSlot = slot;
        }

        return bestSlot;
    }

    private float GetNearDistanceBySlotSize(RectTransform targetRect)
    {
        if (targetRect == null)
            return 0f;

        targetRect.GetWorldCorners(slotWorldCorners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(dragEventCamera, slotWorldCorners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(dragEventCamera, slotWorldCorners[2]);

        float width = Mathf.Abs(topRight.x - bottomLeft.x);
        float height = Mathf.Abs(topRight.y - bottomLeft.y);
        float slotSizePx = Mathf.Min(width, height);
        if (slotSizePx <= 0.001f)
            return 0f;

        float nearScale = Mathf.Clamp(nearCandidateSlotSizeMultiplier, 0.1f, 1f);
        float nearRadiusPx = slotSizePx * 0.5f * nearScale;
        return Mathf.Max(4f, nearRadiusPx);
    }

    private void ClearMergeCandidateVisuals()
    {
        for (int i = 0; i < mergeCandidates.Count; i++)
        {
            InventorySlotUI slot = mergeCandidates[i];
            if (slot != null)
                slot.ClearMergeCandidateVisual();
        }

        mergeCandidates.Clear();
    }

    private void AbortDragVisualState()
    {
        isDraggingActive = false;
        dragEventCamera = null;
        ClearMergeCandidateVisuals();

        dragLiftTween?.Kill(false);
        dragLiftTween = null;
        if (rectTransform != null)
            rectTransform.localScale = baseScale;

        if (image != null)
            image.raycastTarget = true;

        if (originSlot != null && transform.parent != originSlot)
        {
            transform.SetParent(originSlot);
            transform.localPosition = Vector3.zero;
        }
    }
}

