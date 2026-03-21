using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AddFriendPopupView : PopupView
{
    [System.Serializable]
    private struct AvatarSpriteEntry
    {
        public string avatarId;
        public Sprite avatarSprite;
    }

    [Header("Actions")]
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text statusText;

    [Header("Search")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button searchButton;

    [Header("Result List")]
    [SerializeField] private Transform listRoot;
    [SerializeField] private FriendSearchResultItemView itemPrefab;
    [SerializeField] private GameObject emptyRoot;

    [Header("Popup Links")]
    [SerializeField] private FriendProfilePopupView profilePopupPrefab;

    [Header("Avatar Sprites")]
    [SerializeField] private Sprite fallbackAvatar;
    [SerializeField] private List<AvatarSpriteEntry> avatarSprites = new List<AvatarSpriteEntry>();

    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private readonly List<FriendPublicProfile> cachedProfiles = new List<FriendPublicProfile>();
    private readonly HashSet<string> excludedUids = new HashSet<string>();
    private string initialKeyword = string.Empty;
    private bool wired;
    private bool loading;
    private int lifecycleVersion;

    public void OpenWithKeyword(string keyword)
    {
        initialKeyword = keyword != null ? keyword.Trim() : string.Empty;

        if (isActiveAndEnabled)
            ApplyInitialKeywordAndReload();
    }

    private void Awake()
    {
        WireOnce();
    }

    private void OnEnable()
    {
        WireOnce();
        lifecycleVersion++;
        ApplyInitialKeywordAndReload();
    }

    private void OnDisable()
    {
        lifecycleVersion++;
        ClearRows();
        cachedProfiles.Clear();
        excludedUids.Clear();
        initialKeyword = string.Empty;
        SetStatus(string.Empty);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
        if (searchButton != null)
            searchButton.onClick.RemoveListener(OnSearchClicked);
        if (searchInput != null)
            searchInput.onEndEdit.RemoveListener(OnSearchSubmitted);
    }

    private void WireOnce()
    {
        if (wired)
            return;

        wired = true;
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
        if (searchButton != null)
            searchButton.onClick.AddListener(OnSearchClicked);
        if (searchInput != null)
            searchInput.onEndEdit.AddListener(OnSearchSubmitted);
    }

    private void ApplyInitialKeywordAndReload()
    {
        if (searchInput != null)
            searchInput.SetTextWithoutNotify(initialKeyword);

        if (string.IsNullOrWhiteSpace(initialKeyword))
            _ = LoadTopAsync(lifecycleVersion);
        else
            _ = SearchAsync(initialKeyword, lifecycleVersion);
    }

    private void OnCloseClicked()
    {
        PopupController.Instance?.CloseTop();
    }

    private void OnSearchClicked()
    {
        SearchByInput();
    }

    private void OnSearchSubmitted(string _)
    {
        SearchByInput();
    }

    private void SearchByInput()
    {
        string keyword = searchInput != null ? searchInput.text : string.Empty;
        keyword = keyword != null ? keyword.Trim() : string.Empty;

        if (string.IsNullOrWhiteSpace(keyword))
        {
            _ = LoadTopAsync(lifecycleVersion);
            return;
        }

        _ = SearchAsync(keyword, lifecycleVersion);
    }

    private async System.Threading.Tasks.Task LoadTopAsync(int version)
    {
        if (loading)
            return;

        loading = true;
        SetStatus("Loading...");

        var rowsTask = FriendService.GetTopPublicProfilesAsync(FriendServiceConstants.DefaultAddFriendListLimit);
        var excludedTask = BuildExcludedUidSetAsync();
        await Task.WhenAll(rowsTask, excludedTask);
        var rows = rowsTask.Result;

        loading = false;

        if (version != lifecycleVersion || !isActiveAndEnabled)
            return;

        SetExcludedUids(excludedTask.Result);
        ApplyResult(rows);
        SetStatus(cachedProfiles.Count == 0 ? "No player found." : string.Empty);
    }

    private async System.Threading.Tasks.Task SearchAsync(string keyword, int version)
    {
        if (loading)
            return;

        loading = true;
        SetStatus("Searching...");

        var rowsTask = FriendService.SearchPublicProfilesByDisplayNamePrefixAsync(keyword, FriendServiceConstants.DefaultAddFriendListLimit);
        var excludedTask = BuildExcludedUidSetAsync();
        await Task.WhenAll(rowsTask, excludedTask);
        var rows = rowsTask.Result;

        loading = false;

        if (version != lifecycleVersion || !isActiveAndEnabled)
            return;

        SetExcludedUids(excludedTask.Result);
        ApplyResult(rows);
        SetStatus(cachedProfiles.Count == 0 ? "No player found." : string.Empty);
    }

    private void ApplyResult(List<FriendPublicProfile> rows)
    {
        cachedProfiles.Clear();

        if (rows != null)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var profile = rows[i];
                if (profile == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(profile.uid) && excludedUids.Contains(profile.uid))
                    continue;
                cachedProfiles.Add(profile);
            }
        }

        RebuildRows();
    }

    private void RebuildRows()
    {
        ClearRows();
        if (listRoot == null || itemPrefab == null)
            return;

        itemPrefab.PrepareTemplateIfNeeded();

        for (int i = 0; i < cachedProfiles.Count; i++)
        {
            var profile = cachedProfiles[i];
            var row = Instantiate(itemPrefab, listRoot);
            row.Bind(
                profile,
                ResolveAvatarSprite(profile.avatarId),
                true,
                OnAddFriendClicked,
                OpenProfile);
            spawnedRows.Add(row.gameObject);
        }

        if (emptyRoot != null)
            emptyRoot.SetActive(cachedProfiles.Count == 0);
    }

    private void ClearRows()
    {
        for (int i = spawnedRows.Count - 1; i >= 0; i--)
        {
            if (spawnedRows[i] != null)
                Destroy(spawnedRows[i]);
        }

        spawnedRows.Clear();
    }

    private async void OnAddFriendClicked(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
            return;

        var result = await FriendService.SendFriendRequestAsync(uid);
        SetStatus(result.message);

        if (!string.IsNullOrWhiteSpace(result.message))
            Toaster.Show(result.message, null, 1.2f);
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

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message ?? string.Empty;
    }

    private async Task<HashSet<string>> BuildExcludedUidSetAsync()
    {
        var set = new HashSet<string>();

        string selfUid = FirebaseBootstrap.Ins != null ? FirebaseBootstrap.Ins.Uid : string.Empty;
        if (!string.IsNullOrWhiteSpace(selfUid))
            set.Add(selfUid);

        var friends = await FriendService.GetFriendsAsync(FriendServiceConstants.MaxQueryLimit);
        if (friends != null)
        {
            for (int i = 0; i < friends.Count; i++)
            {
                var friend = friends[i];
                if (friend == null || string.IsNullOrWhiteSpace(friend.uid))
                    continue;

                set.Add(friend.uid);
            }
        }

        return set;
    }

    private void SetExcludedUids(HashSet<string> source)
    {
        excludedUids.Clear();
        if (source == null)
            return;

        foreach (var uid in source)
            excludedUids.Add(uid);
    }

    private Sprite ResolveAvatarSprite(string avatarId)
    {
        if (avatarSprites != null)
        {
            string id = avatarId ?? string.Empty;
            for (int i = 0; i < avatarSprites.Count; i++)
            {
                if (string.Equals(avatarSprites[i].avatarId, id, System.StringComparison.Ordinal))
                    return avatarSprites[i].avatarSprite;
            }
        }

        return fallbackAvatar;
    }
}
