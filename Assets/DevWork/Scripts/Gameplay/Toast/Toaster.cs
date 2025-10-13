using UnityEngine;
using Lean.Pool;

public class Toaster : MonoBehaviour
{
    public static Toaster Ins { get; private set; }

    [Header("Setup")]
    public Canvas canvas;         // 👈 changed from RectTransform to Canvas
    [SerializeField] private Toast toastPrefab;
    [SerializeField] private int preloadCount = 4;

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

    public static void Show(string message, Sprite icon = null, float duration = 1.8f, Vector2? anchoredPos = null)
    {
        if (Ins == null)
        {
            Debug.LogWarning("[Toaster] No Toaster in scene.");
            return;
        }
        Ins.InternalShow(message, icon, duration, anchoredPos);
    }

    private void InternalShow(string message, Sprite icon, float duration, Vector2? anchoredPos)
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

        inst.Play(message, icon, duration);
    }

    public Canvas Canvas => canvas; // expose if needed for conversions
}
