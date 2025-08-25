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

    private int currentPage = 0;
    private int maxPage = 0;

    private List<RectTransform> panels = new List<RectTransform>();
    private Tween slideTween;

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

        UpdateButtonInteractable();
    }

    public void MoveLeft()
    {
        if (currentPage > 0)
        {
            currentPage--;
            SlideToPage(currentPage);
            UpdateButtonInteractable();
        }
    }

    public void MoveRight()
    {
        if (currentPage < maxPage)
        {
            currentPage++;
            SlideToPage(currentPage);
            UpdateButtonInteractable();
        }
    }

    private void SlideToPage(int pageIndex)
    {
        Vector2 targetPos = new Vector2(-slideDistance * pageIndex, contentContainer.anchoredPosition.y);

        // Kill any existing tween before starting a new one
        slideTween?.Kill();

        slideTween = contentContainer.DOAnchorPos(targetPos, slideDuration).SetEase(Ease.OutCubic);
    }

    private void UpdateButtonInteractable()
    {
        leftButton.interactable = currentPage > 0;
        rightButton.interactable = currentPage < maxPage;
    }
}
