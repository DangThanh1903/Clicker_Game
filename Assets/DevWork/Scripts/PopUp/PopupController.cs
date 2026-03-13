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
    bool hasLoggedMissingBackdropCanvasGroup;
    bool hasLoggedMissingBackdropButton;

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
            if (TryGetBackdropCanvasGroup(out var cg))
            {
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }
            backdrop.gameObject.SetActive(false);

            if (closeOnBackdropClick)
            {
                var btn = backdrop.GetComponent<Button>();
                if (btn == null)
                {
                    LogMissingBackdropButton();
                }
                else
                {
                    btn.onClick.AddListener(() => CloseTop());
                }
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
        if (!TryGetBackdropCanvasGroup(out var cg))
            return;

        if (targetAlpha > 0f && !backdrop.gameObject.activeSelf)
            backdrop.gameObject.SetActive(true);

        cg.blocksRaycasts = enableRaycast;
        cg.interactable = enableRaycast;

        backdropTween?.Kill(false);

        if (backdropFade <= 0f)
        {
            cg.alpha = targetAlpha;
        }
        else
        {
            backdropTween = cg.DOFade(targetAlpha, backdropFade).SetUpdate(true);
            await backdropTween.AsyncWaitForCompletion();
            backdropTween = null;
        }

        if (!enableRaycast && targetAlpha <= 0f && backdrop.gameObject.activeSelf)
            backdrop.gameObject.SetActive(false);
    }

    bool TryGetBackdropCanvasGroup(out CanvasGroup canvasGroup)
    {
        canvasGroup = null;
        if (!backdrop)
            return false;

        canvasGroup = backdrop.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            return true;

        if (!hasLoggedMissingBackdropCanvasGroup)
        {
            hasLoggedMissingBackdropCanvasGroup = true;
            Debug.LogError("[PopupController] Backdrop requires a CanvasGroup component.", backdrop);
        }

        return false;
    }

    void LogMissingBackdropButton()
    {
        if (hasLoggedMissingBackdropButton)
            return;

        hasLoggedMissingBackdropButton = true;
        Debug.LogError("[PopupController] closeOnBackdropClick is enabled, but backdrop has no Button component.", backdrop);
    }

    public bool IsAnyPopupOpen()
    {
        PruneStack();

        if (stack.Count == 0 && !isClosingTop)
            HideBackdropImmediate();

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

    void HideBackdropImmediate()
    {
        if (!backdrop)
            return;

        backdropTween?.Kill(false);
        backdropTween = null;

        if (TryGetBackdropCanvasGroup(out var cg))
        {
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        if (backdrop.gameObject.activeSelf)
            backdrop.gameObject.SetActive(false);
    }
}
