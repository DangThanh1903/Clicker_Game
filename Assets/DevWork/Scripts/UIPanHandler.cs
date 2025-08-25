using UnityEngine;
using UnityEngine.EventSystems;

public class UIPanHandler : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    private Vector2 lastPos;
    private RectTransform rect;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        lastPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - lastPos;
        rect.anchoredPosition += delta;
        lastPos = eventData.position;
    }
}
