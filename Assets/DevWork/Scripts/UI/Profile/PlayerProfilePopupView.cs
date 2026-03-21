using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerProfilePopupView : PopupView
{
    [System.Serializable]
    private struct AvatarOption
    {
        public string avatarId;
        public Sprite avatarSprite;
    }

    [Header("Profile UI")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_Text uidText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image avatarPreview;
    [SerializeField] private Sprite fallbackAvatar;

    [Header("Actions")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Button closeButton;

    [Header("Avatar Grid")]
    [SerializeField] private Transform avatarOptionsRoot;
    [SerializeField] private PlayerProfileAvatarOptionItemView avatarOptionPrefab;
    [SerializeField] private List<AvatarOption> avatarOptions = new List<AvatarOption>();

    private readonly List<PlayerProfileAvatarOptionItemView> spawnedAvatarItems = new List<PlayerProfileAvatarOptionItemView>();
    private const int NameCharacterLimit = 16;

    private bool wired;
    private string selectedAvatarId = string.Empty;
    private string savedDisplayName = string.Empty;
    private string savedAvatarId = string.Empty;
    private Coroutine loadProfileCoroutine;

    private void Awake()
    {
        WireOnce();
    }

    private void OnEnable()
    {
        WireOnce();
        RestartLoadProfileRoutine();
    }

    private void OnDisable()
    {
        StopLoadProfileRoutine();
        SetStatus(string.Empty);
    }

    private void OnDestroy()
    {
        if (nameInput != null)
            nameInput.onValueChanged.RemoveListener(OnNameChanged);
        if (saveButton != null)
            saveButton.onClick.RemoveListener(OnSaveClicked);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    private void WireOnce()
    {
        if (wired)
            return;

        wired = true;
        if (nameInput != null)
        {
            nameInput.characterLimit = NameCharacterLimit;
            nameInput.onValueChanged.AddListener(OnNameChanged);
        }
        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void RestartLoadProfileRoutine()
    {
        StopLoadProfileRoutine();
        loadProfileCoroutine = StartCoroutine(CoLoadProfile());
    }

    private void StopLoadProfileRoutine()
    {
        if (loadProfileCoroutine == null)
            return;

        StopCoroutine(loadProfileCoroutine);
        loadProfileCoroutine = null;
    }

    private IEnumerator CoLoadProfile()
    {
        while (DataSaver.Ins == null)
            yield return null;

        loadProfileCoroutine = null;

        if (uidText != null)
            uidText.text = !string.IsNullOrWhiteSpace(FirebaseBootstrap.Ins?.Uid)
                ? FirebaseBootstrap.Ins.Uid
                : "-";

        savedDisplayName = DataSaver.Ins.DisplayName ?? string.Empty;
        savedAvatarId = DataSaver.Ins.AvatarId ?? string.Empty;
        selectedAvatarId = savedAvatarId;

        if (nameInput != null)
            nameInput.SetTextWithoutNotify(savedDisplayName);

        EnsureValidAvatarSelection();
        RebuildAvatarOptions();
        UpdateAvatarPreview();
        UpdateSaveButtonState();
        SetStatus(string.Empty);
    }

    private void RebuildAvatarOptions()
    {
        ClearAvatarOptionItems();

        if (avatarOptionsRoot == null || avatarOptionPrefab == null || avatarOptions == null)
            return;

        for (int i = 0; i < avatarOptions.Count; i++)
        {
            var option = avatarOptions[i];
            string id = option.avatarId ?? string.Empty;
            var item = Instantiate(avatarOptionPrefab, avatarOptionsRoot);
            item.Bind(id, option.avatarSprite, id == selectedAvatarId, OnAvatarSelected);
            spawnedAvatarItems.Add(item);
        }
    }

    private void ClearAvatarOptionItems()
    {
        for (int i = spawnedAvatarItems.Count - 1; i >= 0; i--)
        {
            var item = spawnedAvatarItems[i];
            if (item != null)
                Destroy(item.gameObject);
        }
        spawnedAvatarItems.Clear();
    }

    private void OnAvatarSelected(string avatarId)
    {
        selectedAvatarId = avatarId ?? string.Empty;
        RefreshAvatarSelectionVisual();
        UpdateAvatarPreview();
        UpdateSaveButtonState();
    }

    private void RefreshAvatarSelectionVisual()
    {
        for (int i = 0; i < spawnedAvatarItems.Count; i++)
        {
            var item = spawnedAvatarItems[i];
            if (item == null)
                continue;

            string id = avatarOptions != null && i < avatarOptions.Count
                ? avatarOptions[i].avatarId ?? string.Empty
                : string.Empty;

            item.SetSelected(id == selectedAvatarId);
        }
    }

    private void UpdateAvatarPreview()
    {
        if (avatarPreview == null)
            return;

        avatarPreview.sprite = FindAvatarSprite(selectedAvatarId) ?? fallbackAvatar;
    }

    private Sprite FindAvatarSprite(string avatarId)
    {
        if (avatarOptions == null || avatarOptions.Count == 0)
            return null;

        string target = avatarId ?? string.Empty;
        for (int i = 0; i < avatarOptions.Count; i++)
        {
            if (string.Equals(avatarOptions[i].avatarId, target, System.StringComparison.Ordinal))
                return avatarOptions[i].avatarSprite;
        }
        return null;
    }

    private void EnsureValidAvatarSelection()
    {
        if (avatarOptions == null || avatarOptions.Count == 0)
            return;

        bool found = false;
        for (int i = 0; i < avatarOptions.Count; i++)
        {
            if (string.Equals(avatarOptions[i].avatarId, selectedAvatarId, System.StringComparison.Ordinal))
            {
                found = true;
                break;
            }
        }

        if (!found)
            selectedAvatarId = avatarOptions[0].avatarId ?? string.Empty;
    }

    private void OnNameChanged(string _)
    {
        SetStatus(string.Empty);
        UpdateSaveButtonState();
    }

    private void UpdateSaveButtonState()
    {
        if (saveButton == null)
            return;

        string currentName = nameInput != null ? (nameInput.text ?? string.Empty).Trim() : string.Empty;
        bool validName = !string.IsNullOrWhiteSpace(currentName);
        bool changed = !string.Equals(currentName, savedDisplayName ?? string.Empty, System.StringComparison.Ordinal) ||
                       !string.Equals(selectedAvatarId ?? string.Empty, savedAvatarId ?? string.Empty, System.StringComparison.Ordinal);

        saveButton.interactable = validName && changed;
    }

    private void OnSaveClicked()
    {
        if (DataSaver.Ins == null)
            return;

        string name = nameInput != null ? (nameInput.text ?? string.Empty).Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Name required.");
            UpdateSaveButtonState();
            return;
        }

        DataSaver.Ins.SetDisplayName(name, forceSave: false);
        DataSaver.Ins.SetAvatarId(selectedAvatarId, forceSave: false);
        DataSaver.Ins.SaveDataFn(force: true);

        savedDisplayName = DataSaver.Ins.DisplayName ?? name;
        savedAvatarId = DataSaver.Ins.AvatarId ?? selectedAvatarId;

        SetStatus("Profile saved.");
        Toaster.Show("Profile saved.", null, 1.2f);
        UpdateSaveButtonState();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message ?? string.Empty;
    }

    private void OnCloseClicked()
    {
        PopupController.Instance?.CloseTop();
    }
}
