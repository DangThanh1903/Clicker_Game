using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardPopupView : PopupView
{
    [Header("Tabs")]
    [SerializeField] private Button clicksTabButton;
    [SerializeField] private Button playtimeTabButton;
    [SerializeField] private TMP_Text clicksTabLabel;
    [SerializeField] private TMP_Text playtimeTabLabel;

    [Header("Content")]
    [SerializeField] private Transform listRoot;
    [SerializeField] private LeaderboardEntryItem itemPrefab;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingRoot;
    [SerializeField] private int limit = 50;

    [Header("Actions")]
    [SerializeField] private Button closeButton;

    private readonly List<LeaderboardEntryItem> spawnedItems = new List<LeaderboardEntryItem>();
    private bool wired;
    private LeaderboardMetric currentMetric = LeaderboardMetric.Clicks;
    private bool isLoading;
    private int requestId;
    private LeaderboardMetric? pendingMetric;

    private void Awake()
    {
        WireOnce();
    }

    private void OnEnable()
    {
        WireOnce();
        LoadMetric(currentMetric);
    }

    private void OnDestroy()
    {
        if (clicksTabButton != null)
            clicksTabButton.onClick.RemoveListener(OnClicksTab);
        if (playtimeTabButton != null)
            playtimeTabButton.onClick.RemoveListener(OnPlaytimeTab);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnClose);
    }

    private void WireOnce()
    {
        if (wired) return;
        wired = true;

        if (clicksTabButton != null)
            clicksTabButton.onClick.AddListener(OnClicksTab);
        if (playtimeTabButton != null)
            playtimeTabButton.onClick.AddListener(OnPlaytimeTab);
        if (closeButton != null)
            closeButton.onClick.AddListener(OnClose);
    }

    private void OnClicksTab()
    {
        LoadMetric(LeaderboardMetric.Clicks);
    }

    private void OnPlaytimeTab()
    {
        LoadMetric(LeaderboardMetric.TotalPlaytime);
    }

    private void OnClose()
    {
        PopupController.Instance?.CloseTop();
    }

    private async void LoadMetric(LeaderboardMetric metric)
    {
        if (isLoading)
        {
            if (metric != currentMetric)
                pendingMetric = metric;
            return;
        }

        currentMetric = metric;
        UpdateTabVisuals();
        SetLoading(true, "Loading...");
        ClearItems();

        isLoading = true;
        int localRequest = ++requestId;

        List<LeaderboardEntry> entries = metric == LeaderboardMetric.Clicks
            ? await LeaderboardService.GetTopClicks(limit)
            : await LeaderboardService.GetTopTotalPlaytime(limit);

        if (localRequest != requestId)
            return;

        if (entries == null || entries.Count == 0)
        {
            SetLoading(false, "No data");
            isLoading = false;
            TryConsumePending();
            return;
        }

        SetLoading(false, "");
        int rank = 1;
        foreach (var e in entries)
        {
            if (itemPrefab == null || listRoot == null) break;
            var item = Instantiate(itemPrefab, listRoot);
            item.Bind(rank, e.displayName, e.value, metric);
            spawnedItems.Add(item);
            rank++;
        }

        isLoading = false;
        TryConsumePending();
    }

    private void UpdateTabVisuals()
    {
        bool isClicks = currentMetric == LeaderboardMetric.Clicks;
        if (clicksTabButton != null) clicksTabButton.interactable = !isClicks;
        if (playtimeTabButton != null) playtimeTabButton.interactable = isClicks;

        if (clicksTabLabel != null) clicksTabLabel.alpha = isClicks ? 1f : 0.6f;
        if (playtimeTabLabel != null) playtimeTabLabel.alpha = isClicks ? 0.6f : 1f;
    }

    private void SetLoading(bool show, string message)
    {
        if (loadingRoot != null)
            loadingRoot.SetActive(show);
        if (statusText != null)
            statusText.text = message;

        if (clicksTabButton != null) clicksTabButton.interactable = !show && currentMetric != LeaderboardMetric.Clicks;
        if (playtimeTabButton != null) playtimeTabButton.interactable = !show && currentMetric != LeaderboardMetric.TotalPlaytime;
    }

    private void TryConsumePending()
    {
        if (pendingMetric.HasValue && pendingMetric.Value != currentMetric)
        {
            var next = pendingMetric.Value;
            pendingMetric = null;
            LoadMetric(next);
        }
        else
        {
            pendingMetric = null;
        }
    }

    private void ClearItems()
    {
        for (int i = spawnedItems.Count - 1; i >= 0; i--)
        {
            if (spawnedItems[i] != null)
                Destroy(spawnedItems[i].gameObject);
        }
        spawnedItems.Clear();
    }
}
