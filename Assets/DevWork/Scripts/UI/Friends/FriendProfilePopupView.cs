using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class FriendProfilePopupView : PopupView
{
    [Header("UI")]
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private TMP_Text uidText;
    [SerializeField] private TMP_Text clicksText;
    [SerializeField] private TMP_Text playtimeText;
    [SerializeField] private TMP_Text locationText;
    [SerializeField] private TMP_Text blockText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button addFriendButton;
    [SerializeField] private Button closeButton;

    private string targetUid;
    private bool wired;
    private int requestVersion;

    private void Awake()
    {
        WireOnce();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogMissingReferencesIfAny();
#endif
    }

    private void OnEnable()
    {
        WireOnce();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogMissingReferencesIfAny();
#endif
        if (!string.IsNullOrWhiteSpace(targetUid))
            _ = LoadAsync(targetUid);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
        if (addFriendButton != null)
            addFriendButton.onClick.RemoveListener(OnAddFriendClicked);
    }

    public void Bind(string uid)
    {
        targetUid = uid != null ? uid.Trim() : string.Empty;
        if (isActiveAndEnabled && !string.IsNullOrWhiteSpace(targetUid))
            _ = LoadAsync(targetUid);
    }

    private void WireOnce()
    {
        if (wired)
            return;

        wired = true;
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
        if (addFriendButton != null)
            addFriendButton.onClick.AddListener(OnAddFriendClicked);
    }

    private async System.Threading.Tasks.Task LoadAsync(string uid)
    {
        int version = ++requestVersion;
        SetStatus("Loading...");
        var profile = await FriendService.GetPublicProfileAsync(uid);

        if (version != requestVersion)
            return;

        if (profile == null)
        {
            SetStatus("Profile not found.");
            return;
        }

        SetStatus(string.Empty);
        if (displayNameText != null)
            displayNameText.text = string.IsNullOrWhiteSpace(profile.displayName) ? "Player" : profile.displayName;
        if (uidText != null)
            uidText.text = $"UID: {profile.uid}";
        if (clicksText != null)
            clicksText.text = $"Clicks: {FriendUiFormat.FormatNumber(profile.clicks)}";
        if (playtimeText != null)
            playtimeText.text = $"Playtime: {FriendUiFormat.FormatDuration(profile.totalPlaytime)}";
        if (locationText != null)
            locationText.text = $"Location: {GetSafe(profile.currentLocation)}";
        if (blockText != null)
            blockText.text = $"Block: {GetSafe(profile.currentBlock)}";
    }

    private async void OnAddFriendClicked()
    {
        if (string.IsNullOrWhiteSpace(targetUid))
            return;

        var result = await FriendService.SendFriendRequestAsync(targetUid);
        SetStatus(result.message);
        if (result.status == FriendOpStatus.Success)
            ShowToast("Friend request sent.");
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message ?? string.Empty;
    }

    private static string GetSafe(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool HasRequiredReferences()
    {
        return displayNameText != null &&
               uidText != null &&
               clicksText != null &&
               playtimeText != null &&
               locationText != null &&
               blockText != null &&
               statusText != null &&
               addFriendButton != null &&
               closeButton != null;
    }

    private void LogMissingReferencesIfAny()
    {
        if (HasRequiredReferences())
            return;

        Debug.LogWarning(
            "[FriendProfilePopupView] Missing UI references. Please bind display/uid/clicks/playtime/location/block/status + buttons in prefab.",
            this);
    }
#endif

    private static void ShowToast(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Toaster.Show(message, null, 1.2f);
    }

    private static void Close()
    {
        PopupController.Instance?.CloseTop();
    }
}
