using UnityEngine;
using UnityEngine.EventSystems;

public class UIPanHandler : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    private Vector2 lastPos;
    private RectTransform rect;
    [SerializeField] private RectTransform maskRect;

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

        ClampToMask();
    }

    private void ClampToMask()
    {
        if (maskRect == null) return;

        // Get sizes
        Vector2 maskSize = maskRect.rect.size;
        Vector2 contentSize = rect.rect.size;

        Vector2 pos = rect.anchoredPosition;

        // Calculate the max offsets (half sizes matter because anchoredPosition is center-based)
        float maxX = Mathf.Max(0, (contentSize.x - maskSize.x) / 2f);
        float maxY = Mathf.Max(0, (contentSize.y - maskSize.y) / 2f);

        pos.x = Mathf.Clamp(pos.x, -maxX, maxX);
        pos.y = Mathf.Clamp(pos.y, -maxY, maxY);

        rect.anchoredPosition = pos;
    }
}
