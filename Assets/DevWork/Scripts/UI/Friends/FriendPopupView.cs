using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public sealed class FriendPopupView : PopupView
{
    private enum FriendTab
    {
        Friends,
        Requests
    }

    [Header("Tabs")]
    [SerializeField] private Button friendsTabButton;
    [SerializeField] private Button requestsTabButton;
    [SerializeField] private GameObject friendsPanel;
    [SerializeField] private GameObject requestsPanel;

    [Header("Actions")]
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private int realtimeLimit = 100;
    [SerializeField, FormerlySerializedAs("findFriendButton")] private Button addFriendButton;

    [Header("Friends List")]
    [SerializeField] private Transform friendsListRoot;
    [SerializeField] private FriendListItemView friendListItemPrefab;
    [SerializeField] private GameObject friendsEmptyRoot;

    [Header("Requests List")]
    [SerializeField] private Transform incomingRequestsRoot;
    [SerializeField] private Transform outgoingRequestsRoot;
    [SerializeField] private FriendRequestItemView requestItemPrefab;

    [Header("Popup Links")]
    [SerializeField] private FriendProfilePopupView profilePopupPrefab;
    [SerializeField] private AddFriendPopupView addFriendPopupPrefab;

    private readonly List<FriendLinkData> cachedFriends = new List<FriendLinkData>();
    private readonly List<FriendRequestData> cachedIncomingRequests = new List<FriendRequestData>();
    private readonly List<FriendRequestData> cachedOutgoingRequests = new List<FriendRequestData>();

    private readonly List<FriendListItemView> friendRows = new List<FriendListItemView>();
    private readonly List<FriendRequestItemView> incomingRows = new List<FriendRequestItemView>();
    private readonly List<FriendRequestItemView> outgoingRows = new List<FriendRequestItemView>();

    private bool wired;
    private bool subscribed;
    private bool startedRealtimeByThisView;
    private int lifecycleVersion;
    private int lastAddFriendOpenFrame = -1;
    private FriendTab currentTab = FriendTab.Friends;

    private void Awake()
    {
        WireOnce();
    }

    private void OnEnable()
    {
        WireOnce();
        SubscribeRealtimeOnce();
        lifecycleVersion++;
        ClearCachedData();
        SetTab(FriendTab.Friends);
        _ = StartRealtimeAsync(lifecycleVersion);
    }

    private void OnDisable()
    {
        lifecycleVersion++;
        UnsubscribeRealtime();

        if (startedRealtimeByThisView)
            FriendService.StopRealtimeSync();

        startedRealtimeByThisView = false;
        ClearAllRows();
        SetStatus(string.Empty);
    }

    private void OnDestroy()
    {
        if (friendsTabButton != null)
            friendsTabButton.onClick.RemoveListener(OnFriendsTabClicked);
        if (requestsTabButton != null)
            requestsTabButton.onClick.RemoveListener(OnRequestsTabClicked);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
        if (addFriendButton != null)
            addFriendButton.onClick.RemoveListener(OnAddFriendClicked);
    }

    private void WireOnce()
    {
        if (wired)
            return;

        wired = true;
        if (friendsTabButton != null)
            friendsTabButton.onClick.AddListener(OnFriendsTabClicked);
        if (requestsTabButton != null)
            requestsTabButton.onClick.AddListener(OnRequestsTabClicked);
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
        if (addFriendButton != null)
            addFriendButton.onClick.AddListener(OnAddFriendClicked);
    }

    private void SubscribeRealtimeOnce()
    {
        if (subscribed)
            return;

        subscribed = true;
        FriendService.RealtimeFriendsChanged += OnRealtimeFriendsChanged;
        FriendService.RealtimeIncomingRequestsChanged += OnRealtimeIncomingRequestsChanged;
        FriendService.RealtimeOutgoingRequestsChanged += OnRealtimeOutgoingRequestsChanged;
        FriendService.RealtimeError += OnRealtimeError;
    }

    private void UnsubscribeRealtime()
    {
        if (!subscribed)
            return;

        subscribed = false;
        FriendService.RealtimeFriendsChanged -= OnRealtimeFriendsChanged;
        FriendService.RealtimeIncomingRequestsChanged -= OnRealtimeIncomingRequestsChanged;
        FriendService.RealtimeOutgoingRequestsChanged -= OnRealtimeOutgoingRequestsChanged;
        FriendService.RealtimeError -= OnRealtimeError;
    }

    private async System.Threading.Tasks.Task StartRealtimeAsync(int version)
    {
        bool wasListening = FriendService.IsRealtimeListening;
        var result = await FriendService.StartRealtimeSyncAsync(realtimeLimit);

        if (version != lifecycleVersion)
        {
            if (!wasListening && result.status == FriendOpStatus.Success)
                FriendService.StopRealtimeSync();
            return;
        }

        startedRealtimeByThisView = !wasListening && result.status == FriendOpStatus.Success;

        if (result.status == FriendOpStatus.Success)
            await SeedInitialListsAsync(version);
        else
            SetStatus(result.message);
    }

    private async Task SeedInitialListsAsync(int version)
    {
        Task<List<FriendLinkData>> friendsTask;
        Task<List<FriendRequestData>> incomingTask;
        Task<List<FriendRequestData>> outgoingTask;

        try
        {
            friendsTask = FriendService.GetFriendsAsync(realtimeLimit);
            incomingTask = FriendService.GetIncomingRequestsAsync(realtimeLimit);
            outgoingTask = FriendService.GetOutgoingRequestsAsync(realtimeLimit);
            await Task.WhenAll(friendsTask, incomingTask, outgoingTask);
        }
        catch (System.Exception ex)
        {
            SetStatus($"Load friend data failed: {ex.Message}");
            return;
        }

        if (version != lifecycleVersion || !isActiveAndEnabled)
            return;

        CopyList(friendsTask.Result, cachedFriends);
        CopyList(incomingTask.Result, cachedIncomingRequests);
        CopyList(outgoingTask.Result, cachedOutgoingRequests);
        RefreshCurrentTab();
    }

    private void SetTab(FriendTab tab)
    {
        currentTab = tab;

        if (friendsPanel != null)
            friendsPanel.SetActive(tab == FriendTab.Friends);
        if (requestsPanel != null)
            requestsPanel.SetActive(tab == FriendTab.Requests);

        if (friendsTabButton != null)
            friendsTabButton.interactable = tab != FriendTab.Friends;
        if (requestsTabButton != null)
            requestsTabButton.interactable = tab != FriendTab.Requests;

        RefreshCurrentTab();
    }

    private void RefreshCurrentTab()
    {
        if (currentTab == FriendTab.Friends)
            RebuildFriends();
        else
            RebuildRequests();
    }

    private void OnFriendsTabClicked()
    {
        SetTab(FriendTab.Friends);
    }

    private void OnRequestsTabClicked()
    {
        SetTab(FriendTab.Requests);
    }

    private static void OnCloseClicked()
    {
        PopupController.Instance?.CloseTop();
    }

    // Inspector hook for Button.onClick when wiring manually in prefab/scene.
    public void OpenAddFriendPopupFromButton()
    {
        OnAddFriendClicked();
    }

    private async void OnAddFriendClicked()
    {
        if (Time.frameCount == lastAddFriendOpenFrame)
            return;

        lastAddFriendOpenFrame = Time.frameCount;

        var popupController = PopupController.Instance;
        if (popupController == null || addFriendPopupPrefab == null)
            return;

        try
        {
            await popupController.CloseTopAsync();
            await popupController.Show(addFriendPopupPrefab, popup =>
            {
                if (popup is AddFriendPopupView addFriendPopup)
                    addFriendPopup.OpenWithKeyword(string.Empty);
            });
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[FriendPopupView] Failed to open AddFriendPopup: {ex.Message}");
        }
    }

    private void OnRealtimeFriendsChanged(IReadOnlyList<FriendLinkData> rows)
    {
        CopyList(rows, cachedFriends);
        RebuildFriends();
    }

    private void OnRealtimeIncomingRequestsChanged(IReadOnlyList<FriendRequestData> rows)
    {
        CopyList(rows, cachedIncomingRequests);
        RebuildRequests();
    }

    private void OnRealtimeOutgoingRequestsChanged(IReadOnlyList<FriendRequestData> rows)
    {
        CopyList(rows, cachedOutgoingRequests);
        RebuildRequests();
    }

    private void OnRealtimeError(string error)
    {
        SetStatus(error);
    }

    private void RebuildFriends()
    {
        if (currentTab != FriendTab.Friends)
            return;

        if (friendsListRoot == null || friendListItemPrefab == null)
            return;

        friendListItemPrefab.PrepareTemplateIfNeeded();
        EnsureFriendRowCount(cachedFriends.Count);

        for (int i = 0; i < friendRows.Count; i++)
        {
            bool isActive = i < cachedFriends.Count;
            var row = friendRows[i];
            if (row == null)
                continue;

            row.gameObject.SetActive(isActive);
            if (!isActive)
                continue;

            row.Bind(cachedFriends[i], OpenProfile, OnFriendGiftClicked, OnFriendRemoveClicked);
        }

        if (friendsEmptyRoot != null)
            friendsEmptyRoot.SetActive(cachedFriends.Count == 0);
    }

    private void RebuildRequests()
    {
        if (currentTab != FriendTab.Requests)
            return;

        if (requestItemPrefab == null)
            return;

        requestItemPrefab.PrepareTemplateIfNeeded();

        EnsureRequestRowCount(incomingRows, cachedIncomingRequests.Count, incomingRequestsRoot);
        EnsureRequestRowCount(outgoingRows, cachedOutgoingRequests.Count, outgoingRequestsRoot);

        for (int i = 0; i < incomingRows.Count; i++)
        {
            bool isActive = i < cachedIncomingRequests.Count;
            var row = incomingRows[i];
            if (row == null)
                continue;

            row.gameObject.SetActive(isActive);
            if (!isActive)
                continue;

            row.Bind(
                cachedIncomingRequests[i],
                FriendRequestItemView.RequestItemMode.Incoming,
                OnIncomingAcceptClicked,
                OnIncomingRejectClicked,
                null,
                OpenProfile);
        }

        for (int i = 0; i < outgoingRows.Count; i++)
        {
            bool isActive = i < cachedOutgoingRequests.Count;
            var row = outgoingRows[i];
            if (row == null)
                continue;

            row.gameObject.SetActive(isActive);
            if (!isActive)
                continue;

            row.Bind(
                cachedOutgoingRequests[i],
                FriendRequestItemView.RequestItemMode.Outgoing,
                null,
                null,
                OnOutgoingCancelClicked,
                OpenProfile);
        }
    }

    private async void OnFriendGiftClicked(string uid)
    {
        var result = await FriendService.SendDailyGiftAsync(uid);
        HandleOperationResult(result);
    }

    private async void OnFriendRemoveClicked(string uid)
    {
        var result = await FriendService.RemoveFriendAsync(uid);
        HandleOperationResult(result);
    }

    private async void OnIncomingAcceptClicked(string uid)
    {
        var result = await FriendService.AcceptFriendRequestAsync(uid);
        HandleOperationResult(result);
    }

    private async void OnIncomingRejectClicked(string uid)
    {
        var result = await FriendService.RejectFriendRequestAsync(uid);
        HandleOperationResult(result);
    }

    private async void OnOutgoingCancelClicked(string uid)
    {
        var result = await FriendService.CancelFriendRequestAsync(uid);
        HandleOperationResult(result);
    }

    private void OpenProfile(string uid)
    {
        if (profilePopupPrefab == null || PopupController.Instance == null || string.IsNullOrWhiteSpace(uid))
            return;

        _ = PopupController.Instance.Show(profilePopupPrefab, popup =>
        {
            if (popup is FriendProfilePopupView profileView)
                profileView.Bind(uid);
        });
    }

    private void HandleOperationResult(FriendOpResult result)
    {
        SetStatus(result.message);
        if (!string.IsNullOrWhiteSpace(result.message))
            Toaster.Show(result.message, null, 1.2f);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message ?? string.Empty;
    }

    private void ClearAllRows()
    {
        SetRowsActive(friendRows, false);
        SetRowsActive(incomingRows, false);
        SetRowsActive(outgoingRows, false);
    }

    private void ClearCachedData()
    {
        cachedFriends.Clear();
        cachedIncomingRequests.Clear();
        cachedOutgoingRequests.Clear();
    }

    private void EnsureFriendRowCount(int needed)
    {
        if (friendsListRoot == null || friendListItemPrefab == null)
            return;

        while (friendRows.Count < needed)
        {
            var row = Instantiate(friendListItemPrefab, friendsListRoot);
            row.gameObject.SetActive(false);
            friendRows.Add(row);
        }
    }

    private void EnsureRequestRowCount(List<FriendRequestItemView> rows, int needed, Transform root)
    {
        if (root == null || requestItemPrefab == null)
            return;

        while (rows.Count < needed)
        {
            var row = Instantiate(requestItemPrefab, root);
            row.gameObject.SetActive(false);
            rows.Add(row);
        }
    }

    private static void SetRowsActive<T>(List<T> rows, bool active) where T : Component
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null)
                rows[i].gameObject.SetActive(active);
        }
    }

    private static void CopyList<T>(IReadOnlyList<T> source, List<T> destination)
    {
        destination.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
            destination.Add(source[i]);
    }
}
