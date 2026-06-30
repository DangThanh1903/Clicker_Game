using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using Lean.Pool;
using UnityEngine;
using UnityEngine.UI;

public class PopupController : MonoBehaviour
{
    // Allowed global owner: modal popup stack and backdrop.
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
    bool isReady;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        EnsureReady();
    }

    // Show by spawning from pool into popupRoot
    public async Task<PopupView> Show(PopupView popupPrefab)
    {
        EnsureReady();
        if (popupPrefab == null) return null;
        if (popupRoot == null)
            return null;

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
        EnsureReady();
        if (popupPrefab == null) return null;
        if (popupRoot == null)
            return null;

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
        EnsureReady();
        if (!backdrop) return;
        var cg = EnsureBackdropCanvasGroup();

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

    void SetupBackdrop()
    {
        if (!backdrop)
        {
            Debug.LogWarning("[PopupController] Backdrop is not assigned. Popups will open without a dim background.", this);
            return;
        }

        var cg = EnsureBackdropCanvasGroup();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        if (closeOnBackdropClick)
            EnsureBackdropButton();

        backdrop.transform.SetAsFirstSibling();
        backdrop.gameObject.SetActive(false);
    }

    void EnsureReady()
    {
        if (isReady)
            return;

        if (popupRoot == null)
            popupRoot = transform;

        SetupBackdrop();
        isReady = true;
    }

    CanvasGroup EnsureBackdropCanvasGroup()
    {
        var cg = backdrop.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = backdrop.gameObject.AddComponent<CanvasGroup>();

        return cg;
    }

    void EnsureBackdropButton()
    {
        var btn = backdrop.GetComponent<Button>();
        if (btn == null)
            btn = backdrop.gameObject.AddComponent<Button>();

        btn.targetGraphic = backdrop;
        btn.transition = Selectable.Transition.None;
        btn.onClick.RemoveListener(CloseTop);
        btn.onClick.AddListener(CloseTop);
    }

    public bool IsAnyPopupOpen()
    {
        EnsureReady();
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

        var cg = EnsureBackdropCanvasGroup();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        if (backdrop.gameObject.activeSelf)
            backdrop.gameObject.SetActive(false);
    }

    private void OnValidate()
    {
        if (popupRoot == null)
            popupRoot = transform;
    }
}
