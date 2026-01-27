using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CraftingTreeCuller : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform container;
    [SerializeField] private CraftNodeManager nodeManager;

    [Header("Behavior")]
    [SerializeField] private bool includeLines = true;
    [SerializeField] private bool disableGraphics = true;
    [SerializeField] private bool useCanvasCull = true;
    [SerializeField] private float extraMargin = 50f;
    [SerializeField] private float checkInterval = 0.1f;

    private readonly List<Entry> entries = new List<Entry>(256);
    private float nextCheckTime;

    private struct Entry
    {
        public RectTransform rect;
        public Graphic[] graphics;
        public bool visible;
    }

    private void Awake()
    {
        if (container == null)
            container = transform as RectTransform;
        Rebuild();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        entries.Clear();

        if (nodeManager != null)
        {
            foreach (var node in nodeManager.allNodes)
            {
                if (node == null) continue;
                var rt = node.transform as RectTransform;
                if (rt == null) continue;

                var graphics = node.GetComponentsInChildren<Graphic>(true);
                entries.Add(new Entry
                {
                    rect = rt,
                    graphics = graphics,
                    visible = true
                });
            }
        }

        if (includeLines && container != null)
        {
            var graphics = container.GetComponentsInChildren<Graphic>(true);
            foreach (var g in graphics)
            {
                if (g == null) continue;
                if (g.GetComponentInParent<CraftNode>() != null) continue;

                var rt = g.transform as RectTransform;
                if (rt == null) continue;

                entries.Add(new Entry
                {
                    rect = rt,
                    graphics = new[] { g },
                    visible = true
                });
            }
        }

        nextCheckTime = 0f;
        UpdateCull();
    }

    private void LateUpdate()
    {
        if (viewport == null || container == null)
            return;

        if (Time.unscaledTime < nextCheckTime)
            return;

        nextCheckTime = Time.unscaledTime + Mathf.Max(0.02f, checkInterval);
        UpdateCull();
    }

    private void UpdateCull()
    {
        if (viewport == null)
            return;

        Rect viewRect = GetWorldRect(viewport);
        viewRect.xMin -= extraMargin;
        viewRect.yMin -= extraMargin;
        viewRect.xMax += extraMargin;
        viewRect.yMax += extraMargin;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.rect == null || e.graphics == null) continue;

            Rect r = GetWorldRect(e.rect);
            bool visible = r.Overlaps(viewRect, true);
            if (visible == e.visible)
                continue;

            if (disableGraphics)
            {
                for (int g = 0; g < e.graphics.Length; g++)
                {
                    var graphic = e.graphics[g];
                    if (graphic == null) continue;

                    if (useCanvasCull)
                    {
                        var renderer = graphic.canvasRenderer;
                        if (renderer != null)
                            renderer.cull = !visible;
                    }
                    else
                    {
                        graphic.enabled = visible;
                    }
                }
            }

            e.visible = visible;
            entries[i] = e;
        }
    }

    private static Rect GetWorldRect(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float minX = corners[0].x;
        float minY = corners[0].y;
        float maxX = corners[2].x;
        float maxY = corners[2].y;
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }
}
