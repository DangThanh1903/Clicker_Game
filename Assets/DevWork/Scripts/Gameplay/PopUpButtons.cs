using UnityEngine;
using UnityEngine.UI;

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

    private async void OpenPopup()
    {
        if (PopupController.Instance && popupPrefab)
        {
            await PopupController.Instance.Show(popupPrefab);
        }
        else
        {
            Debug.LogWarning("[PopupButton] Missing PopupController or Popup Prefab.");
        }
    }
}
