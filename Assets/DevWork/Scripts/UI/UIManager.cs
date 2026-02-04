using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
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

    [Header("Bottom button anim")]
    [SerializeField] private Vector3 selectedIconScale = new Vector3(1.2f, 1.2f, 1f);
    [SerializeField] private float iconRiseHeight = 20f;

    private readonly int startIndex = 2;     // start page index
    private int currentIndex = -1;
    private Tween moveTween;
    private bool isTweening;
    public Action<int, int> OnPageChanged;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private readonly List<CanvasGroup> panelGroups = new List<CanvasGroup>();
    public int CurrentIndex => currentIndex;

    [Header("Setting button")]
    private bool isOpenSetting;
    [SerializeField] private Button settingButton;

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
        if (viewport == null && uIPanel != null)
            viewport = uIPanel.parent as RectTransform;

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        SetupButtons();
        InitPanelGroups();
        ActivateOnly(startIndex, snap: false);
        RefreshLayout(snapToCurrent: true);
        BottomButtonAnim(startIndex);
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
                if (isTweening || currentIndex == index) return;
                SlideTo(index);
                BottomButtonAnim(index);
            });
        }
    }

    void SlideTo(int target)
    {
        RefreshLayout(snapToCurrent: false);
        target = Mathf.Clamp(target, 0, panels.Count - 1);

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
                               SetButtonsInteractable(true);
                               isTweening = false;
                           });
    }

    public void MoveToMain()
    {
        SlideTo(startIndex);
        BottomButtonAnim(startIndex);
    }

    public void GoToPage(int index)
    {
        if (isTweening || currentIndex == index) return;
        SlideTo(index);
        BottomButtonAnim(index);
    }

    void ActivateOnly(int idx, bool snap)
    {
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
            if (cg == null) continue;

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
                cg = panel.gameObject.AddComponent<CanvasGroup>();

            panelGroups.Add(cg);
        }
    }

    void LateUpdate()
    {
        if (!autoPageWidth)
            return;

        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            RefreshLayout(snapToCurrent: true);
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
        foreach (var b in buttons) b.interactable = on;
    }

    void BottomButtonAnim(int index)
    {
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

    public bool IsBlockCanClick() => startIndex == currentIndex && !isOpenSetting;

    public void SetLocationBackground(int index)
    {
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
}
