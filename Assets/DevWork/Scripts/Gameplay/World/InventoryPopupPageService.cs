using UnityEngine;

[DisallowMultipleComponent]
public sealed class InventoryPopupPageService : MonoBehaviour, IInventoryPopupService
{
    [SerializeField] private UIManager uiManager;
    [SerializeField] private int inventoryPageIndex = 0;
    [SerializeField] private bool skipInventoryCameraFocus = true;

    private void Awake()
    {
        if (uiManager == null)
            uiManager = UIManager.Ins;
    }

    private void OnEnable()
    {
        InventoryPopupRuntime.Bind(this);
    }

    private void OnDisable()
    {
        InventoryPopupRuntime.Unbind(this);
    }

    public void OpenInventoryPopup()
    {
        if (uiManager == null)
            uiManager = UIManager.Ins;

        if (uiManager == null)
        {
            Debug.LogWarning("[InventoryPopupPageService] Missing UIManager.");
            return;
        }

        uiManager.GoToPage(inventoryPageIndex, !skipInventoryCameraFocus);
    }
}
