using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DragableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public Transform originSlot;
    public InventoryItem inventoryItem;
    private Image image;

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
        Transform grandgrandparent = transform.parent.parent.parent;
        if (grandgrandparent != null)
        {
            transform.SetParent(grandgrandparent, worldPositionStays: true);
        }
        else
        {
            transform.SetParent(transform.root, worldPositionStays: true);
        }
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
}

