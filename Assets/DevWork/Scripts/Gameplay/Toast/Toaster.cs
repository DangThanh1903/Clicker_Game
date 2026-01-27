using UnityEngine;
using Lean.Pool;

public class Toaster : MonoBehaviour
{
    public static Toaster Ins { get; private set; }

    [Header("Setup")]
    public Canvas canvas;         // 👈 changed from RectTransform to Canvas
    [SerializeField] private Toast toastPrefab;
    [SerializeField] private int preloadCount = 4;
    [SerializeField] private float randomPadding = 60f;

    void Awake()
    {
        if (Ins != null && Ins != this) { Destroy(gameObject); return; }
        Ins = this;

        if (toastPrefab && preloadCount > 0)
        {
            for (int i = 0; i < preloadCount; i++)
            {
                var t = LeanPool.Spawn(toastPrefab, canvas.transform);
                LeanPool.Despawn(t);
            }
        }
    }

    public static void Show(string message, Sprite icon = null, float duration = 1.8f, Vector2? anchoredPos = null, bool rainbow = false)
    {
        if (Ins == null)
        {
            Debug.LogWarning("[Toaster] No Toaster in scene.");
            return;
        }
        Ins.InternalShow(message, icon, duration, anchoredPos, rainbow);
    }

    public static Vector2 GetRandomAnchoredPosition(float paddingOverride = -1f)
    {
        if (Ins == null || Ins.canvas == null) return Vector2.zero;
        return Ins.InternalGetRandomAnchoredPosition(paddingOverride);
    }

    private void InternalShow(string message, Sprite icon, float duration, Vector2? anchoredPos, bool rainbow)
    {
        if (!toastPrefab || !canvas)
        {
            Debug.LogWarning("[Toaster] Missing prefab or canvas.");
            return;
        }

        Toast inst = LeanPool.Spawn(toastPrefab, canvas.transform);
        var rt = (RectTransform)inst.transform;
        rt.localScale = Vector3.one;

        if (anchoredPos.HasValue)
            rt.anchoredPosition = anchoredPos.Value;
        else
            rt.anchoredPosition3D = Vector3.zero;

        inst.Play(message, icon, duration, rainbow);
    }

    private Vector2 InternalGetRandomAnchoredPosition(float paddingOverride)
    {
        var rt = canvas.transform as RectTransform;
        if (rt == null) return Vector2.zero;

        float padding = paddingOverride >= 0f ? paddingOverride : randomPadding;
        padding = Mathf.Max(0f, padding);

        var rect = rt.rect;
        float minX = rect.xMin + padding;
        float maxX = rect.xMax - padding;
        if (minX > maxX) { minX = rect.xMin; maxX = rect.xMax; }

        float minY = rect.yMin + padding;
        float maxY = rect.yMax - padding;
        if (minY > maxY) { minY = rect.yMin; maxY = rect.yMax; }

        return new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
    }

    public Canvas Canvas => canvas; // expose if needed for conversions
}
