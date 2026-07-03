using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    // Legacy global path: keep current scene wiring, but prefer feature presenters over new direct global access.
    public static UIManager Ins { get; private set; }

    [Header("Nav")]
    [SerializeField] private List<Button> buttons;
    [SerializeField] private List<RectTransform> panels;
    [SerializeField] private RectTransform uIPanel;
    [SerializeField] private RectTransform viewport;        // Viewport (mask)
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private Ease ease = Ease.OutCubic;
    [SerializeField] private bool autoPageWidth = true;
    [SerializeField] private float fallbackPageWidth = 720f;
    [SerializeField] private float pageWidth = 0f;
    [SerializeField] private bool keepPanelsActive = true;
    [SerializeField] private float hiddenAlpha = 0f;
    [SerializeField] private bool disableButtonInteractableWhileSliding = false;

    [Header("Bottom button anim")]
    [SerializeField] private Vector3 selectedIconScale = new Vector3(1.2f, 1.2f, 1f);
    [SerializeField] private float iconRiseHeight = 20f;
    [SerializeField] private bool animateSelectedButtonWidth = true;
    [SerializeField, Min(1f)] private float selectedButtonWidthMultiplier = 1.3f;
    [SerializeField, Min(0.01f)] private float buttonWidthDuration = 0.25f;
    [SerializeField] private Ease buttonWidthEase = Ease.OutCubic;

    private readonly int startIndex = 2;     // start page index
    private int currentIndex = -1;
    private Tween moveTween;
    private bool isTweening;
    private bool navigationLocked;
    public Action<int, int> OnPageChanged;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private readonly List<CanvasGroup> panelGroups = new List<CanvasGroup>();
    private readonly List<LayoutElement> navButtonLayouts = new List<LayoutElement>();
    private readonly List<float> navButtonBaseWidths = new List<float>();
    private bool initialized;
    private bool isInitializing;
    public int CurrentIndex => currentIndex;

    [Header("Inventory Camera Focus")]
    [SerializeField] private int inventoryPageIndex = 0;
    [SerializeField] private InventoryChestCameraFocus inventoryCameraFocus;

    [Header("Location")]
    [SerializeField] private Image[] locationBackground;
    [SerializeField] private Sprite[] locationTexture2D;

    void Awake()
    {
        if (Ins != null && Ins != this) { Destroy(gameObject); return; }
        Ins = this;

        // Cull fully transparent graphics to reduce overdraw
        foreach (var g in GetComponentsInChildren<Graphic>(true))
            g.canvasRenderer.cullTransparentMesh = true;
    }

    void Start()
    {
        EnsureInitialized();
    }

    void SetupButtons()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            int index = i;

            buttons[index].onClick.RemoveAllListeners();

            var keep = buttons[index].targetGraphic;
            foreach (var g in buttons[index].GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = (g == keep);

            buttons[index].onClick.AddListener(() =>
            {
                TryOpenPage(index, useInventoryCameraFocus: true);
            });
        }
    }

    private void TryOpenPage(int targetIndex, bool useInventoryCameraFocus)
    {
        EnsureInitialized();

        if (navigationLocked)
            return;

        targetIndex = Mathf.Clamp(targetIndex, 0, panels.Count - 1);
        if (isTweening || currentIndex == targetIndex)
            return;

        if (useInventoryCameraFocus &&
            targetIndex == inventoryPageIndex &&
            inventoryCameraFocus != null &&
            inventoryCameraFocus.CanFocus)
        {
            BeginInventoryCameraFocusThenSlide(targetIndex);
            return;
        }

        SlideTo(targetIndex);
        BottomButtonAnim(targetIndex);
    }

    private void BeginInventoryCameraFocusThenSlide(int targetIndex)
    {
        isTweening = true;
        if (disableButtonInteractableWhileSliding)
            SetButtonsInteractable(false);

        inventoryCameraFocus.PlayFocusThen(() =>
        {
            if (this == null || !isActiveAndEnabled)
                return;

            isTweening = false;
            if (disableButtonInteractableWhileSliding)
                SetButtonsInteractable(true);

            if (currentIndex == targetIndex)
                return;

            SlideTo(targetIndex);
            BottomButtonAnim(targetIndex);
        });
    }

    void SlideTo(int target)
    {
        EnsureInitialized();

        if (navigationLocked) return;
        RefreshLayout(snapToCurrent: false);
        target = Mathf.Clamp(target, 0, panels.Count - 1);

        if (currentIndex == inventoryPageIndex &&
            target != inventoryPageIndex &&
            inventoryCameraFocus != null &&
            inventoryCameraFocus.RestoreOnExit)
        {
            inventoryCameraFocus.RestoreGameplayView();
        }

        int previousIndex = currentIndex;
        if (previousIndex == target)
            return;

        OnPageChanged?.Invoke(previousIndex, target);

        int a = Mathf.Min(currentIndex < 0 ? target : currentIndex, target);
        int b = Mathf.Max(currentIndex < 0 ? target : currentIndex, target);

        if (keepPanelsActive)
        {
            SetPanelVisibilityRange(a, b, allowInteract: false);
        }
        else
        {
            for (int i = 0; i < panels.Count; i++)
                panels[i].gameObject.SetActive(i >= a && i <= b);
        }

        if (disableButtonInteractableWhileSliding)
            SetButtonsInteractable(false);
        isTweening = true;

        float targetX = -target * pageWidth;

        moveTween?.Kill(true);
        moveTween = uIPanel.DOAnchorPosX(targetX, duration)
                           .SetEase(ease)
                           .SetUpdate(true)
                           .OnComplete(() =>
                           {
                               ActivateOnly(target, snap: false);
                               if (disableButtonInteractableWhileSliding)
                                   SetButtonsInteractable(true);
                               isTweening = false;
                           });
    }

    public void MoveToMain()
    {
        TryOpenPage(startIndex, useInventoryCameraFocus: true);
    }

    public void GoToPage(int index)
    {
        GoToPage(index, useInventoryCameraFocus: true);
    }

    public void GoToPage(int index, bool useInventoryCameraFocus)
    {
        TryOpenPage(index, useInventoryCameraFocus);
    }

    void ActivateOnly(int idx, bool snap)
    {
        EnsureInitialized();

        if (keepPanelsActive)
        {
            SetPanelVisibilityRange(idx, idx, allowInteract: true);
        }
        else
        {
            for (int i = 0; i < panels.Count; i++)
                panels[i].gameObject.SetActive(i == idx);
        }

        if (snap)
            uIPanel.anchoredPosition = new Vector2(-idx * pageWidth, uIPanel.anchoredPosition.y);

        currentIndex = idx;
    }

    private void SetPanelVisibilityRange(int from, int to, bool allowInteract)
    {
        if (panelGroups.Count != panels.Count)
            InitPanelGroups();

        for (int i = 0; i < panels.Count; i++)
        {
            var panel = panels[i];
            if (panel == null) continue;
            var cg = panelGroups.Count > i ? panelGroups[i] : null;
            if (cg == null)
            {
                panel.gameObject.SetActive(i >= from && i <= to);
                continue;
            }

            bool visible = i >= from && i <= to;
            bool interact = allowInteract && i == to;
            cg.alpha = visible ? 1f : hiddenAlpha;
            cg.interactable = interact;
            cg.blocksRaycasts = interact;
        }
    }

    private void InitPanelGroups()
    {
        panelGroups.Clear();
        if (!keepPanelsActive)
            return;

        for (int i = 0; i < panels.Count; i++)
        {
            var panel = panels[i];
            if (panel == null)
            {
                panelGroups.Add(null);
                continue;
            }

            if (!panel.gameObject.activeSelf)
                panel.gameObject.SetActive(true);

            var cg = panel.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                Debug.LogError($"[UIManager] keepPanelsActive requires CanvasGroup on panel '{panel.name}'.", panel);
                panelGroups.Add(null);
                continue;
            }

            panelGroups.Add(cg);
        }
    }

    void LateUpdate()
    {
        EnsureInitialized();

        if (!autoPageWidth)
            return;

        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            RefreshLayout(snapToCurrent: true);
            RefreshBottomButtonWidthBase();
            BottomButtonAnim(currentIndex < 0 ? startIndex : currentIndex);
        }
    }

    private void RefreshLayout(bool snapToCurrent)
    {
        if (uIPanel == null)
            return;

        if (viewport == null)
            viewport = uIPanel.parent as RectTransform;

        if (autoPageWidth || pageWidth <= 0f)
            pageWidth = viewport ? viewport.rect.width : fallbackPageWidth;

        if (pageWidth <= 0f)
            pageWidth = fallbackPageWidth;

        for (int i = 0; i < panels.Count; i++)
        {
            var panel = panels[i];
            if (panel == null)
                continue;

            panel.anchoredPosition = new Vector2(i * pageWidth, panel.anchoredPosition.y);
        }

        if (snapToCurrent)
        {
            int idx = currentIndex < 0 ? startIndex : currentIndex;
            uIPanel.anchoredPosition = new Vector2(-idx * pageWidth, uIPanel.anchoredPosition.y);
            currentIndex = idx;
        }
    }

    public void SetButtonsInteractable(bool on)
    {
        EnsureInitialized();
        foreach (var b in buttons) b.interactable = on;
    }

    public void SetNavigationLocked(bool locked, bool forceToMain = false)
    {
        EnsureInitialized();
        navigationLocked = locked;
        SetButtonsInteractable(!locked);

        if (locked && forceToMain)
        {
            moveTween?.Kill(true);
            isTweening = false;

            RefreshLayout(snapToCurrent: false);
            ActivateOnly(startIndex, snap: true);
            BottomButtonAnim(startIndex);
        }
    }

    void BottomButtonAnim(int index)
    {
        EnsureInitialized();
        AnimateBottomButtonWidths(index);

        for (int j = 0; j < buttons.Count; j++)
        {
            Transform icon = buttons[j].transform.GetChild(0);
            Transform text = buttons[j].transform.GetChild(1);

            icon.DOKill(true);
            text.DOKill(true);

            if (j == index)
            {
                icon.DOScale(selectedIconScale, 0.25f).SetEase(Ease.OutBack);
                icon.DOLocalMoveY(iconRiseHeight, 0.25f).SetEase(Ease.OutQuad);
                text.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
            }
            else
            {
                icon.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutCubic);
                icon.DOLocalMoveY(0f, 0.2f).SetEase(Ease.OutCubic);
                text.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InCubic);
            }
        }
    }

    private void SetupBottomButtonWidthLayout()
    {
        if (!animateSelectedButtonWidth)
            return;

        navButtonLayouts.Clear();
        navButtonBaseWidths.Clear();
        bool hasAnyLayoutElement = false;

        for (int i = 0; i < buttons.Count; i++)
        {
            var btn = buttons[i];
            if (btn == null)
            {
                navButtonLayouts.Add(null);
                navButtonBaseWidths.Add(0f);
                continue;
            }

            var rect = btn.transform as RectTransform;
            if (rect == null)
            {
                navButtonLayouts.Add(null);
                navButtonBaseWidths.Add(0f);
                continue;
            }

            var layout = btn.GetComponent<LayoutElement>();
            if (layout == null)
            {
                Debug.LogError($"[UIManager] animateSelectedButtonWidth requires LayoutElement on button '{btn.name}'.", btn);
                navButtonLayouts.Add(null);
                navButtonBaseWidths.Add(0f);
                continue;
            }

            navButtonLayouts.Add(layout);
            navButtonBaseWidths.Add(0f);
            hasAnyLayoutElement = true;
        }

        if (!hasAnyLayoutElement)
        {
            animateSelectedButtonWidth = false;
            return;
        }

        RefreshBottomButtonWidthBase();
    }

    private void RefreshBottomButtonWidthBase()
    {
        if (!animateSelectedButtonWidth || navButtonLayouts.Count != buttons.Count)
            return;

        for (int i = 0; i < navButtonLayouts.Count; i++)
        {
            var layout = navButtonLayouts[i];
            if (layout != null)
                layout.preferredWidth = -1f;
        }

        Canvas.ForceUpdateCanvases();
        var parent = buttons.Count > 0 ? buttons[0].transform.parent as RectTransform : null;
        if (parent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);

        for (int i = 0; i < buttons.Count; i++)
        {
            var btn = buttons[i];
            var layout = navButtonLayouts[i];
            if (btn == null || layout == null)
                continue;

            var rect = btn.transform as RectTransform;
            float width = rect != null ? rect.rect.width : 0f;
            if (width <= 0f)
                width = 100f;

            navButtonBaseWidths[i] = width;
        }
    }

    private void AnimateBottomButtonWidths(int selectedIndex)
    {
        if (!animateSelectedButtonWidth || navButtonLayouts.Count != buttons.Count)
            return;

        selectedIndex = Mathf.Clamp(selectedIndex, 0, buttons.Count - 1);
        float multiplier = Mathf.Max(1f, selectedButtonWidthMultiplier);

        for (int i = 0; i < buttons.Count; i++)
        {
            var layout = navButtonLayouts[i];
            if (layout == null)
                continue;

            float baseWidth = i < navButtonBaseWidths.Count ? navButtonBaseWidths[i] : 0f;
            if (baseWidth <= 0f)
                continue;

            float target = (i == selectedIndex) ? baseWidth * multiplier : baseWidth;
            DOTween.Kill(layout);

            DOTween.To(() => layout.preferredWidth, x => layout.preferredWidth = x, target, buttonWidthDuration)
                .SetEase(buttonWidthEase)
                .SetUpdate(true)
                .SetTarget(layout);
        }
    }

    public bool IsBlockCanClick() => startIndex == currentIndex;

    public Button GetNavButton(int index)
    {
        EnsureInitialized();
        if (buttons == null || index < 0 || index >= buttons.Count)
            return null;
        return buttons[index];
    }

    public int GetNavButtonCount()
    {
        return buttons != null ? buttons.Count : 0;
    }

    public void SetLocationBackground(int index)
    {
        EnsureInitialized();

        if (locationTexture2D == null || locationTexture2D.Length == 0)
        {
            Debug.LogWarning("[UIManager] Location textures are not assigned.");
            return;
        }

        if (index < 0 || index >= locationTexture2D.Length)
        {
            Debug.LogWarning($"[UIManager] Location texture index out of range: {index} (count={locationTexture2D.Length}).");
            return;
        }

        foreach (var image in locationBackground)
        {
            if (image == null) continue;
            image.sprite = locationTexture2D[index];
        }
    }

    private void EnsureInitialized()
    {
        if (initialized || isInitializing)
            return;

        isInitializing = true;
        try
        {
            if (viewport == null && uIPanel != null)
                viewport = uIPanel.parent as RectTransform;

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            SetupButtons();
            InitPanelGroups();
            ActivateOnly(startIndex, snap: false);
            RefreshLayout(snapToCurrent: true);
            SetupBottomButtonWidthLayout();
            BottomButtonAnim(startIndex);
            initialized = true;
        }
        finally
        {
            isInitializing = false;
        }
    }

    private void OnValidate()
    {
        if (buttons != null && panels != null && buttons.Count != panels.Count)
        {
            Debug.LogWarning("[UIManager] Nav buttons count does not match panels count.", this);
        }
    }
}
