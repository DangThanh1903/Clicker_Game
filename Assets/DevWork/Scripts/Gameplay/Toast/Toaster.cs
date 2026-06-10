using UnityEngine;
using Lean.Pool;
using System.Collections;
using System.Collections.Generic;

public class Toaster : MonoBehaviour
{
    public static Toaster Ins { get; private set; }

    [Header("Setup")]
    public Canvas canvas;         // 👈 changed from RectTransform to Canvas
    [SerializeField] private Toast toastPrefab;
    [SerializeField] private int preloadCount = 4;
    [SerializeField] private float randomPadding = 60f;
    [SerializeField] [Range(0.1f, 1f)] private float toastScale = 0.8f;
    [SerializeField] [Range(0.1f, 1f)] private float spawnAreaScale = 1f;

    [Header("Pickup Toast")]
    [SerializeField] private RectTransform pickupAnchor;
    [SerializeField] private int pickupNavButtonIndex = 0;
    [SerializeField] private Vector2 pickupFallbackNormalized = new Vector2(0.12f, 0.1f);
    [SerializeField] private Vector2 pickupOffset = new Vector2(0f, 82f);
    [SerializeField] [Range(0.1f, 3f)] private float pickupToastScale = 1.8f;
    [SerializeField, Min(0.15f)] private float pickupDuration = 0.85f;
    [SerializeField, Min(0f)] private float pickupRiseDistance = 86f;
    [SerializeField, Min(0f)] private float pickupSpawnInterval = 0.25f;
    [SerializeField, Min(0f)] private float pickupStackSpacing = 10f;

    private int pickupSequence;
    private readonly Queue<Sprite> pickupQueue = new Queue<Sprite>();
    private Coroutine pickupQueueRoutine;

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

    public static void ShowPickupItems(Sprite icon, int amount)
    {
        if (Ins == null)
        {
            Debug.LogWarning("[Toaster] No Toaster in scene.");
            return;
        }

        Ins.InternalShowPickupItems(icon, amount);
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
        rt.localScale = Vector3.one * Mathf.Max(0.1f, toastScale);

        if (anchoredPos.HasValue)
            rt.anchoredPosition = anchoredPos.Value;
        else
            rt.anchoredPosition3D = Vector3.zero;

        inst.Play(message, icon, duration, rainbow);
    }

    private void InternalShowPickupItems(Sprite icon, int amount)
    {
        if (!toastPrefab || !canvas)
        {
            Debug.LogWarning("[Toaster] Missing prefab or canvas.");
            return;
        }

        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
            return;

        for (int i = 0; i < safeAmount; i++)
            pickupQueue.Enqueue(icon);

        if (pickupQueueRoutine == null)
            pickupQueueRoutine = StartCoroutine(ProcessPickupQueue_Co());
    }

    private IEnumerator ProcessPickupQueue_Co()
    {
        while (pickupQueue.Count > 0)
        {
            SpawnPickupIcon(pickupQueue.Dequeue());

            if (pickupQueue.Count <= 0)
                break;

            if (pickupSpawnInterval > 0f)
                yield return new WaitForSeconds(pickupSpawnInterval);
            else
                yield return null;
        }

        pickupQueueRoutine = null;
    }

    private void SpawnPickupIcon(Sprite icon)
    {
        if (!toastPrefab || !canvas)
        {
            Debug.LogWarning("[Toaster] Missing prefab or canvas.");
            return;
        }

        Toast inst = LeanPool.Spawn(toastPrefab, canvas.transform);
        var rt = (RectTransform)inst.transform;
        rt.localScale = Vector3.one * Mathf.Max(0.1f, pickupToastScale);
        rt.anchoredPosition = ResolvePickupAnchoredPosition();

        inst.PlayPickupIcon(icon, pickupDuration, pickupRiseDistance);
    }

    private Vector2 ResolvePickupAnchoredPosition()
    {
        Vector2 basePosition = ResolvePickupBaseAnchoredPosition();
        const int slotCount = 5;
        int stackIndex = pickupSequence++ % slotCount;
        float vertical = stackIndex * pickupStackSpacing;
        return basePosition + new Vector2(0f, vertical);
    }

    private Vector2 ResolvePickupBaseAnchoredPosition()
    {
        var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (canvasRect == null)
            return pickupOffset;

        RectTransform anchor = ResolvePickupAnchor();
        if (anchor != null)
        {
            Camera cam = ResolveCanvasCamera();
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, anchor.position);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cam, out Vector2 localPoint))
                return localPoint + pickupOffset;
        }

        Rect rect = canvasRect.rect;
        Vector2 normalized = new Vector2(
            Mathf.Clamp01(pickupFallbackNormalized.x),
            Mathf.Clamp01(pickupFallbackNormalized.y));
        Vector2 fallback = new Vector2(
            rect.xMin + rect.width * normalized.x,
            rect.yMin + rect.height * normalized.y);
        return fallback + pickupOffset;
    }

    private RectTransform ResolvePickupAnchor()
    {
        if (pickupAnchor != null && pickupAnchor.gameObject.activeInHierarchy)
            return pickupAnchor;

        if (UIManager.Ins == null)
            return null;

        var button = UIManager.Ins.GetNavButton(pickupNavButtonIndex);
        return button != null ? button.transform as RectTransform : null;
    }

    private Camera ResolveCanvasCamera()
    {
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
    }

    private Vector2 InternalGetRandomAnchoredPosition(float paddingOverride)
    {
        var rt = canvas.transform as RectTransform;
        if (rt == null) return Vector2.zero;

        float padding = paddingOverride >= 0f ? paddingOverride : randomPadding;
        padding = Mathf.Max(0f, padding);

        var rect = rt.rect;
        float scale = Mathf.Clamp01(spawnAreaScale);
        if (scale < 1f)
        {
            Vector2 size = rect.size * scale;
            Vector2 center = rect.center;
            rect = new Rect(center - size * 0.5f, size);
        }

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

