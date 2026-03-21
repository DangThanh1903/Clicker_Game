using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FriendEntryView : MonoBehaviour
{
    [Header("Common")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private Sprite fallbackAvatar;
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private Button profileButton;

    [Header("Friend Row")]
    [SerializeField] private TMP_Text sinceText;
    [SerializeField] private Button giftButton;
    [SerializeField] private Button removeButton;

    [Header("Request Row")]
    [SerializeField] private TMP_Text createdAtText;
    [SerializeField] private Button acceptButton;
    [SerializeField] private Button rejectButton;
    [SerializeField] private Button cancelButton;

    [Header("Search Row")]
    [SerializeField] private TMP_Text clicksText;
    [SerializeField] private TMP_Text playtimeText;
    [SerializeField] private Button addButton;

    private string uid;
    private Action<string> onProfile;
    private Action<string> onGift;
    private Action<string> onRemove;
    private Action<string> onAccept;
    private Action<string> onReject;
    private Action<string> onCancel;
    private Action<string> onAdd;
    private bool listenersBound;
    private int lastAddInvokeFrame = -1;

    private void Awake()
    {
        BindButtonListeners();
    }

    public void PrepareTemplateIfNeeded()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogMissingReferencesIfAny();
#endif
    }

    private void BindButtonListeners()
    {
        if (listenersBound)
            return;

        listenersBound = true;

        if (profileButton != null)
            profileButton.onClick.AddListener(HandleProfileClicked);
        if (giftButton != null)
            giftButton.onClick.AddListener(HandleGiftClicked);
        if (removeButton != null)
            removeButton.onClick.AddListener(HandleRemoveClicked);
        if (acceptButton != null)
            acceptButton.onClick.AddListener(HandleAcceptClicked);
        if (rejectButton != null)
            rejectButton.onClick.AddListener(HandleRejectClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(HandleCancelClicked);
        if (addButton != null)
            addButton.onClick.AddListener(HandleAddClicked);
    }

    private void OnDestroy()
    {
        if (!listenersBound)
            return;

        if (profileButton != null)
            profileButton.onClick.RemoveListener(HandleProfileClicked);
        if (giftButton != null)
            giftButton.onClick.RemoveListener(HandleGiftClicked);
        if (removeButton != null)
            removeButton.onClick.RemoveListener(HandleRemoveClicked);
        if (acceptButton != null)
            acceptButton.onClick.RemoveListener(HandleAcceptClicked);
        if (rejectButton != null)
            rejectButton.onClick.RemoveListener(HandleRejectClicked);
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(HandleCancelClicked);
        if (addButton != null)
            addButton.onClick.RemoveListener(HandleAddClicked);

        listenersBound = false;
    }

    protected void BindFriendRow(
        FriendLinkData data,
        Action<string> profileCallback,
        Action<string> giftCallback,
        Action<string> removeCallback)
    {
        uid = data != null ? data.uid : string.Empty;
        onProfile = profileCallback;
        onGift = giftCallback;
        onRemove = removeCallback;
        onAccept = null;
        onReject = null;
        onCancel = null;
        onAdd = null;

        SetDisplayName(uid, data != null ? data.displayName : string.Empty);
        SetAvatar(null);
        SetText(sinceText, data != null ? $"Since {FriendUiFormat.FormatDate(data.sinceAt)}" : "Since -", true);
        SetText(createdAtText, string.Empty, false);
        SetText(clicksText, string.Empty, false);
        SetText(playtimeText, string.Empty, false);

        SetActive(giftButton, giftCallback != null);
        SetActive(removeButton, removeCallback != null);
        SetActive(acceptButton, false);
        SetActive(rejectButton, false);
        SetActive(cancelButton, false);
        SetActive(addButton, false);
        SetActive(profileButton, profileCallback != null);
    }

    protected void BindRequestRow(
        FriendRequestData data,
        bool incoming,
        Action<string> acceptCallback,
        Action<string> rejectCallback,
        Action<string> cancelCallback,
        Action<string> profileCallback)
    {
        uid = data != null ? data.uid : string.Empty;
        onProfile = profileCallback;
        onGift = null;
        onRemove = null;
        onAccept = acceptCallback;
        onReject = rejectCallback;
        onCancel = cancelCallback;
        onAdd = null;

        SetDisplayName(uid, data != null ? data.displayName : string.Empty);
        SetAvatar(null);
        SetText(sinceText, string.Empty, false);
        SetText(createdAtText, data != null ? FriendUiFormat.FormatDate(data.createdAt) : "-", true);
        SetText(clicksText, string.Empty, false);
        SetText(playtimeText, string.Empty, false);

        SetActive(giftButton, false);
        SetActive(removeButton, false);
        SetActive(acceptButton, incoming && acceptCallback != null);
        SetActive(rejectButton, incoming && rejectCallback != null);
        SetActive(cancelButton, !incoming && cancelCallback != null);
        SetActive(addButton, false);
        SetActive(profileButton, profileCallback != null);
    }

    protected void BindSearchRow(
        FriendPublicProfile profile,
        Sprite avatarSprite,
        bool canAdd,
        Action<string> addCallback,
        Action<string> profileCallback)
    {
        uid = profile != null ? profile.uid : string.Empty;
        onProfile = profileCallback;
        onGift = null;
        onRemove = null;
        onAccept = null;
        onReject = null;
        onCancel = null;
        onAdd = addCallback;

        SetDisplayName(uid, profile != null ? profile.displayName : string.Empty);
        SetAvatar(avatarSprite);
        SetText(sinceText, string.Empty, false);
        SetText(createdAtText, string.Empty, false);
        SetText(clicksText, profile != null ? $"Clicks: {FriendUiFormat.FormatNumber(profile.clicks)}" : "Clicks: 0", true);
        SetText(
            playtimeText,
            profile != null ? $"Playtime: {FriendUiFormat.FormatDuration(profile.totalPlaytime)}" : "Playtime: 00:00:00",
            true);

        SetActive(giftButton, false);
        SetActive(removeButton, false);
        SetActive(acceptButton, false);
        SetActive(rejectButton, false);
        SetActive(cancelButton, false);
        SetActive(addButton, canAdd && addCallback != null);
        SetActive(profileButton, profileCallback != null);
    }

    private void SetDisplayName(string currentUid, string displayName)
    {
        if (displayNameText == null)
            return;

        displayNameText.text = string.IsNullOrWhiteSpace(displayName)
            ? FriendUiFormat.ShortUid(currentUid)
            : displayName;
    }

    private void SetAvatar(Sprite sprite)
    {
        if (avatarImage == null)
            return;

        avatarImage.sprite = sprite != null ? sprite : fallbackAvatar;
    }

    private static void SetText(TMP_Text text, string value, bool isVisible)
    {
        if (text == null)
            return;

        text.gameObject.SetActive(isVisible);
        if (isVisible)
            text.text = value ?? string.Empty;
    }

    private static void SetActive(Component component, bool isActive)
    {
        if (component != null)
            component.gameObject.SetActive(isActive);
    }

    private void HandleProfileClicked()
    {
        if (!string.IsNullOrEmpty(uid))
            onProfile?.Invoke(uid);
    }

    private void HandleGiftClicked()
    {
        if (!string.IsNullOrEmpty(uid))
            onGift?.Invoke(uid);
    }

    private void HandleRemoveClicked()
    {
        if (!string.IsNullOrEmpty(uid))
            onRemove?.Invoke(uid);
    }

    private void HandleAcceptClicked()
    {
        if (!string.IsNullOrEmpty(uid))
            onAccept?.Invoke(uid);
    }

    private void HandleRejectClicked()
    {
        if (!string.IsNullOrEmpty(uid))
            onReject?.Invoke(uid);
    }

    private void HandleCancelClicked()
    {
        if (!string.IsNullOrEmpty(uid))
            onCancel?.Invoke(uid);
    }

    private void HandleAddClicked()
    {
        InvokeAddAction();
    }

    // Inspector hook for Button.onClick when you want manual binding.
    public void OnAddButtonPressed()
    {
        InvokeAddAction();
    }

    private void InvokeAddAction()
    {
        if (Time.frameCount == lastAddInvokeFrame)
            return;

        lastAddInvokeFrame = Time.frameCount;

        if (!string.IsNullOrEmpty(uid))
            onAdd?.Invoke(uid);
    }

    private bool IsFriendListMode()
    {
        return this is FriendListItemView;
    }

    private bool IsRequestMode()
    {
        return this is FriendRequestItemView;
    }

    private bool IsSearchMode()
    {
        return this is FriendSearchResultItemView;
    }

    private bool HasRequiredReferencesForCurrentMode()
    {
        if (displayNameText == null)
            return false;

        if (IsFriendListMode())
            return sinceText != null && giftButton != null && removeButton != null && profileButton != null;

        if (IsRequestMode())
            return createdAtText != null && acceptButton != null && rejectButton != null && cancelButton != null && profileButton != null;

        if (IsSearchMode())
            return avatarImage != null && clicksText != null && playtimeText != null && addButton != null && profileButton != null;

        return true;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogMissingReferencesIfAny()
    {
        if (HasRequiredReferencesForCurrentMode())
            return;

        Debug.LogWarning($"[FriendEntryView] Missing references in {name}. Please bind required fields in scene/prefab.", this);
    }
}
