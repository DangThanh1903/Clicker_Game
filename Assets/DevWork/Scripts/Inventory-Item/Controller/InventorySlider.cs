using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening; // <-- DOTween namespace

public class InventorySlider : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [Header("Settings")]
    [SerializeField] private float slideDistance = 700f;
    [SerializeField] private float slideDuration = 0.5f;

    [SerializeField] private int currentPage = 0;
    private int maxPage = 3;

    private List<RectTransform> panels = new List<RectTransform>();
    private Tween slideTween;
    public Action<int, int> OnPageChanged;

    private void Awake()
    {
        panels.Clear();
        foreach (Transform child in contentContainer)
        {
            var rect = child as RectTransform;
            if (rect != null)
                panels.Add(rect);
        }

        maxPage = Mathf.Max(0, panels.Count - 1);

        leftButton.onClick.AddListener(MoveLeft);
        rightButton.onClick.AddListener(MoveRight);

        SyncCurrentPageFromPosition();
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
