using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerProfilePopupButton : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private PopupView profilePopupPrefab;

    private void Awake()
    {
        if (openButton == null)
            openButton = GetComponent<Button>();

        if (openButton != null)
            openButton.onClick.AddListener(OnOpenClicked);
    }

    private void OnDestroy()
    {
        if (openButton != null)
            openButton.onClick.RemoveListener(OnOpenClicked);
    }

    private void OnOpenClicked()
    {
        _ = OpenAsync();
    }

    private async Task OpenAsync()
    {
        if (PopupController.Instance == null)
        {
            Debug.LogError("[PlayerProfilePopupButton] Missing PopupController in scene.");
            return;
        }

        if (profilePopupPrefab == null)
        {
            Debug.LogError("[PlayerProfilePopupButton] profilePopupPrefab is not assigned.", this);
            return;
        }

        try
        {
            await PopupController.Instance.Show(profilePopupPrefab);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[PlayerProfilePopupButton] Failed to open popup: {ex.Message}");
        }
    }
}
