using UnityEngine;

public interface IInventoryPopupService
{
    void OpenInventoryPopup();
}

public static class InventoryPopupRuntime
{
    private static IInventoryPopupService service;
    private static bool hasLoggedMissingService;

    public static void Bind(IInventoryPopupService popupService)
    {
        if (popupService == null)
        {
            Debug.LogError("[InventoryPopupRuntime] Cannot bind null popup service.");
            return;
        }

        service = popupService;
        hasLoggedMissingService = false;
    }

    public static void Unbind(IInventoryPopupService popupService)
    {
        if (!ReferenceEquals(service, popupService))
            return;

        service = null;
    }

    public static bool TryGet(out IInventoryPopupService popupService)
    {
        popupService = service;
        if (popupService != null)
            return true;

        if (!hasLoggedMissingService)
        {
            hasLoggedMissingService = true;
            Debug.LogError("[InventoryPopupRuntime] No inventory popup service is bound.");
        }

        return false;
    }

    public static void OpenInventoryPopup()
    {
        if (!TryGet(out IInventoryPopupService popupService))
            return;

        popupService.OpenInventoryPopup();
    }
}
