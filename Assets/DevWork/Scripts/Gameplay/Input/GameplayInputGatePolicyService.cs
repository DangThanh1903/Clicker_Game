using UnityEngine.EventSystems;

public sealed class GameplayInputGatePolicyService
{
    public bool IsCombatInputAllowed()
    {
        if (WorldViewModeRuntime.CurrentMode != WorldViewMode.Combat)
            return false;

        UIManager ui = UIManager.Ins;
        if (ui == null || !ui.IsBlockCanClick())
            return false;

        return !IsAnyPopupOpen();
    }

    public bool IsWorldInteractModeAllowed(bool allowInteractDuringTransition)
    {
        WorldViewMode mode = WorldViewModeRuntime.CurrentMode;
        return mode == WorldViewMode.SideView ||
               (allowInteractDuringTransition && mode == WorldViewMode.Transition);
    }

    public bool IsWorldInteractBlockedByPopup(bool ignoreWhenPopupOpen)
    {
        if (!ignoreWhenPopupOpen)
            return false;

        return IsAnyPopupOpen();
    }

    public bool IsPointerOverUI(bool ignoreWhenPointerOverUI, int pointerId, bool hasPointerId)
    {
        if (!ignoreWhenPointerOverUI || EventSystem.current == null)
            return false;

        if (hasPointerId)
            return EventSystem.current.IsPointerOverGameObject(pointerId);

        return EventSystem.current.IsPointerOverGameObject();
    }

    private static bool IsAnyPopupOpen()
    {
        return PopupController.Instance != null && PopupController.Instance.IsAnyPopupOpen();
    }
}
