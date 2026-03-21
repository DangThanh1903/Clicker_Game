using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class FriendPopupButton : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private FriendPopupView friendPopupPrefab;
    [SerializeField] private bool autoBindButtonClick = true;

    private int lastOpenFrame = -1;

    private void Awake()
    {
        if (!autoBindButtonClick)
            return;

        if (openButton == null)
            openButton = GetComponent<Button>();

        if (openButton != null)
            openButton.onClick.AddListener(OpenFriendPopupFromButton);
    }

    private void OnDestroy()
    {
        if (!autoBindButtonClick)
            return;

        if (openButton != null)
            openButton.onClick.RemoveListener(OpenFriendPopupFromButton);
    }

    public void OpenFriendPopupFromButton()
    {
        if (Time.frameCount == lastOpenFrame)
            return;

        lastOpenFrame = Time.frameCount;
        _ = OpenAsync();
    }

    private async Task OpenAsync()
    {
        if (PopupController.Instance == null)
        {
            Debug.LogError("[FriendPopupButton] Missing PopupController in scene.");
            return;
        }

        if (friendPopupPrefab == null)
        {
            Debug.LogError("[FriendPopupButton] friendPopupPrefab is not assigned.", this);
            return;
        }

        try
        {
            await PopupController.Instance.Show(friendPopupPrefab);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[FriendPopupButton] Failed to open popup: {ex.Message}");
        }
    }
}
