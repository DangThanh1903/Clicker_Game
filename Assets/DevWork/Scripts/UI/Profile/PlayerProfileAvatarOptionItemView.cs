using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerProfileAvatarOptionItemView : MonoBehaviour
{
    [SerializeField] private Image avatarImage;
    [SerializeField] private GameObject selectedRoot;
    [SerializeField] private Button selectButton;

    private string avatarId;
    private Action<string> onSelect;

    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(HandleSelectClicked);
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(HandleSelectClicked);
    }

    public void Bind(string avatarId, Sprite avatarSprite, bool selected, Action<string> onSelect)
    {
        this.avatarId = avatarId ?? string.Empty;
        this.onSelect = onSelect;

        if (avatarImage != null)
            avatarImage.sprite = avatarSprite;
        if (selectedRoot != null)
            selectedRoot.SetActive(selected);
    }

    public void SetSelected(bool selected)
    {
        if (selectedRoot != null)
            selectedRoot.SetActive(selected);
    }

    private void HandleSelectClicked()
    {
        onSelect?.Invoke(avatarId);
    }
}
