using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;

public class PopupButtons : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private Button button;
    [SerializeField] private PopupView popupPrefab;

    void Awake()
    {
        if (!button) button = GetComponent<Button>();

        if (button)
            button.onClick.AddListener(OpenPopup);
    }

    private void OpenPopup()
    {
        _ = OpenPopupAsync();
    }

    private async Task OpenPopupAsync()
    {
        if (PopupController.Instance && popupPrefab)
        {
            try
            {
                await PopupController.Instance.Show(popupPrefab);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PopupButton] Failed to open popup: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[PopupButton] Missing PopupController or Popup Prefab.");
        }
    }
}
