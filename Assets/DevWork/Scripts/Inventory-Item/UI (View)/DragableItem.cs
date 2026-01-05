using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DragableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [HideInInspector] public Transform originSlot;
    private Image image;
    private float lastClickTime = 0f;
    private InventoryItem inventoryItem;
    private int currenIndex;
    private InventoryData currentData;
    private const float doubleClickThreshold = 0.3f;

    void Awake()
    {
        image = GetComponent<Image>();
        var tmp = GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.raycastTarget = false;
        }

    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        originSlot = transform.parent;
        transform.SetParent(transform.root, worldPositionStays: true);
        transform.SetAsLastSibling();
        image.raycastTarget = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        transform.position = Input.mousePosition;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(originSlot);
        transform.localPosition = Vector3.zero;
        image.raycastTarget = true;
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
        Debug.Log("Single tap detected");
        InventoryController.Instance.SetDescription(ItemTextFormatter.GetFormattedDescription(inventoryItem));
        InventoryController.Instance.SetUseButton(inventoryItem.itemData, currenIndex, currentData);
    }

    private void OnDoubleTap()
    {
        Debug.Log("Double tap detected!");
        // Your double tap logic here
    }
}

