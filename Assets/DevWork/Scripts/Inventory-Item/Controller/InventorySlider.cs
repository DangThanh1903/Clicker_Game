using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // <-- DOTween namespace

public class InventorySlider : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [Header("Settings")]
    [SerializeField] private float slideDistance = 700f;
    [SerializeField] private bool autoSlideDistance = true;
    [SerializeField] private bool matchPageWidth = true;
    [SerializeField] private float slideDuration = 0.5f;

    [SerializeField] private int currentPage = 0;
    private int maxPage = 3;

    private List<RectTransform> panels = new List<RectTransform>();
    private Tween slideTween;
    public Action<int, int> OnPageChanged;
    private bool initialized;

    private void Awake()
    {
        CachePanels();
        maxPage = Mathf.Max(0, panels.Count - 1);

        leftButton?.onClick.AddListener(MoveLeft);
        rightButton?.onClick.AddListener(MoveRight);

        UpdateLayout(true);
        SyncCurrentPageFromPosition();
        UpdateButtonInteractable();
        initialized = true;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!initialized)
            return;

        UpdateLayout(true);
        UpdateButtonInteractable();
    }

    public void MoveLeft()
    {
        GoToPage(currentPage - 1);
    }

    public void MoveRight()
    {
        GoToPage(currentPage + 1);
    }

    public void GoToPage(int pageIndex)
    {
        SyncCurrentPageFromPosition();
        int clamped = Mathf.Clamp(pageIndex, 0, maxPage);
        if (clamped == currentPage)
            return;

        int previousPage = currentPage;
        currentPage = clamped;
        SlideToPage(currentPage);
        UpdateButtonInteractable();
        OnPageChanged?.Invoke(previousPage, currentPage);
    }

    public void GoToStatsPage()
    {
        GoToPage(0);
    }

    private void SlideToPage(int pageIndex)
    {
        Vector2 targetPos = new Vector2(-slideDistance * pageIndex, contentContainer.anchoredPosition.y);

        // Kill any existing tween before starting a new one
        slideTween?.Kill();

        slideTween = contentContainer.DOAnchorPos(targetPos, slideDuration).SetEase(Ease.OutCubic);
    }

    private void SlideToPage(int pageIndex, bool animate)
    {
        Vector2 targetPos = new Vector2(-slideDistance * pageIndex, contentContainer.anchoredPosition.y);

        slideTween?.Kill();

        if (!animate || slideDuration <= 0f)
        {
            contentContainer.anchoredPosition = targetPos;
            return;
        }

        slideTween = contentContainer.DOAnchorPos(targetPos, slideDuration).SetEase(Ease.OutCubic);
    }

    private void CachePanels()
    {
        panels.Clear();
        if (contentContainer == null)
            return;

        foreach (Transform child in contentContainer)
        {
            var rect = child as RectTransform;
            if (rect != null)
                panels.Add(rect);
        }
    }

    private void UpdateLayout(bool snapContent)
    {
        if (contentContainer == null)
            return;

        float width = GetViewportWidth();
        if (autoSlideDistance && width > 0f)
            slideDistance = width;

        if (slideDistance > 0f)
        {
            for (int i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                if (matchPageWidth)
                    panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, slideDistance);
                panel.anchoredPosition = new Vector2(slideDistance * i, panel.anchoredPosition.y);
            }
        }

        if (snapContent && slideDistance > 0f)
            SlideToPage(currentPage, false);
    }

    private float GetViewportWidth()
    {
        RectTransform target = viewport;
        if (target == null && contentContainer != null)
            target = contentContainer.parent as RectTransform;
        if (target == null)
            target = transform as RectTransform;

        return target != null ? target.rect.width : 0f;
    }

    private void SyncCurrentPageFromPosition()
    {
        if (slideDistance <= 0f)
            return;

        int inferred = Mathf.RoundToInt(-contentContainer.anchoredPosition.x / slideDistance);
        currentPage = Mathf.Clamp(inferred, 0, maxPage);
    }

    private void UpdateButtonInteractable()
    {
        leftButton.interactable = currentPage > 0;
        rightButton.interactable = currentPage < maxPage;
    }
}
