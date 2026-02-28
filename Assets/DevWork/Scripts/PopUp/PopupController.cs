using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using Lean.Pool;
using UnityEngine;
using UnityEngine.UI;

public class PopupController : MonoBehaviour
{
    public static PopupController Instance { get; private set; }

    [Header("Popup Root (Canvas or Panel)")]
    [SerializeField] private Transform popupRoot;

    [Header("Backdrop")]
    [SerializeField] private Image backdrop;
    [SerializeField] private float backdropFade = 0.18f;
    [SerializeField] private bool closeOnBackdropClick = true;

    readonly Stack<PopupView> stack = new Stack<PopupView>();
    Tween backdropTween;
    bool isClosingTop;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Optional across scenes:
        // DontDestroyOnLoad(gameObject);

        if (!popupRoot)
            Debug.LogWarning("PopupController: popupRoot is not assigned. Assign your popup Canvas/Panel.");

        // Backdrop setup
        if (backdrop)
        {
            var cg = GetOrAddCanvasGroup(backdrop.gameObject);
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            backdrop.gameObject.SetActive(true);

            if (closeOnBackdropClick)
            {
                var btn = backdrop.GetComponent<Button>();
                if (!btn) btn = backdrop.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() => CloseTop());
            }
        }
    }

    // Show by spawning from pool into popupRoot
    public async Task<PopupView> Show(PopupView popupPrefab)
    {
        if (popupPrefab == null) return null;

        var go = LeanPool.Spawn(popupPrefab.gameObject, popupRoot);
        var popup = go.GetComponent<PopupView>();

        go.transform.SetAsLastSibling();
        if (go.transform is RectTransform rt)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        stack.Push(popup);

        await FadeBackdropTo(1f, enableRaycast: true);
        await popup.OpenAsync();
        return popup;
    }

    // Show with pre-open initialization (avoids visible content pop-in)
    public async Task<PopupView> Show(PopupView popupPrefab, System.Action<PopupView> initBeforeOpen)
    {
        if (popupPrefab == null) return null;

        var go = LeanPool.Spawn(popupPrefab.gameObject, popupRoot);
        var popup = go.GetComponent<PopupView>();

        go.transform.SetAsLastSibling();
        if (go.transform is RectTransform rt)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        initBeforeOpen?.Invoke(popup);

        stack.Push(popup);

        await FadeBackdropTo(1f, enableRaycast: true);
        await popup.OpenAsync();
        return popup;
    }

    public void CloseTop()
    {
        _ = CloseTopAsync();
    }

    public async Task CloseTopAsync()
    {
        if (isClosingTop) return;
        if (stack.Count == 0) return;
        isClosingTop = true;

        try
        {
            var top = stack.Pop();
            await top.CloseAsync();              // wait for close animation
            LeanPool.Despawn(top.gameObject);    // then return to pool

            if (stack.Count == 0)
                await FadeBackdropTo(0f, enableRaycast: false);
            else
                await FadeBackdropTo(1f, enableRaycast: true);
        }
        finally
        {
            isClosingTop = false;
        }
    }

    public async Task CloseAll()
    {
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            await p.CloseAsync();
            LeanPool.Despawn(p.gameObject);
        }
        await FadeBackdropTo(0f, enableRaycast: false);
    }

    async Task FadeBackdropTo(float targetAlpha, bool enableRaycast)
    {
        if (!backdrop) return;

        var cg = GetOrAddCanvasGroup(backdrop.gameObject);
        cg.blocksRaycasts = enableRaycast;

        backdropTween?.Kill(false);
        backdropTween = cg.DOFade(targetAlpha, backdropFade).SetUpdate(true);
        await backdropTween.AsyncWaitForCompletion();
        backdropTween = null;
    }

    static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }
    public bool IsAnyPopupOpen()
    {
        PruneStack();
        return stack.Count > 0;
    }

    void PruneStack()
    {
        // Safety: if a popup was deactivated/destroyed without CloseTop,
        // remove it so queues don't get stuck waiting.
        while (stack.Count > 0)
        {
            var top = stack.Peek();
            if (top == null || !top.gameObject.activeInHierarchy)
                stack.Pop();
            else
                break;
        }
    }
}
