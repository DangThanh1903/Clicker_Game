using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class WorldInteractInputController : MonoBehaviour
{
    private readonly GameplayInputGatePolicyService inputGatePolicyService = new GameplayInputGatePolicyService();

    [SerializeField] private LayerMask interactableLayers = ~0;
    [SerializeField, Min(0.1f)] private float rayDistance = 200f;
    [SerializeField] private bool ignoreWhenPopupOpen = true;
    [SerializeField] private bool ignoreWhenPointerOverUI = true;
    [SerializeField] private bool allowWorldInteractEvenWhenPointerOverUI = false;
    [SerializeField] private bool allowInteractDuringTransition = true;

    private Camera cachedMainCamera;
    private int cachedMainCameraFrame = -1;

    private void Update()
    {
        if (!inputGatePolicyService.IsWorldInteractModeAllowed(allowInteractDuringTransition))
            return;

        if (!TryGetPointerDown(out Vector2 pointerPosition, out int pointerId, out bool hasPointerId))
            return;

        if (inputGatePolicyService.IsWorldInteractBlockedByPopup(ignoreWhenPopupOpen))
            return;

        bool pointerOverUI = inputGatePolicyService.IsPointerOverUI(ignoreWhenPointerOverUI, pointerId, hasPointerId);
        if (pointerOverUI && !allowWorldInteractEvenWhenPointerOverUI)
            return;

        Camera cam = ResolveMainCamera();
        if (cam == null)
            return;

        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            EventSystem.current.SetSelectedGameObject(null);

        Ray ray = cam.ScreenPointToRay(pointerPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, interactableLayers, QueryTriggerInteraction.Ignore))
            return;

        if (!TryResolveInteractable(hit.transform, out IWorldInteractable interactable))
            return;

        if (!interactable.CanInteract)
            return;

        interactable.Interact();
    }

    private static bool TryGetPointerDown(out Vector2 pointerPosition, out int pointerId, out bool hasPointerId)
    {
        pointerPosition = default;
        pointerId = -1;
        hasPointerId = false;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began)
                return false;

            pointerPosition = touch.position;
            pointerId = touch.fingerId;
            hasPointerId = true;
            return true;
        }

        if (!Input.GetMouseButtonDown(0))
            return false;

        pointerPosition = Input.mousePosition;
        return true;
    }

    private bool TryResolveInteractable(Transform hitTransform, out IWorldInteractable interactable)
    {
        interactable = null;
        if (hitTransform == null)
            return false;

        interactable = hitTransform.GetComponent(typeof(IWorldInteractable)) as IWorldInteractable;
        if (interactable != null)
            return true;

        interactable = hitTransform.GetComponentInParent(typeof(IWorldInteractable)) as IWorldInteractable;
        return interactable != null;
    }

    private Camera ResolveMainCamera()
    {
        if (cachedMainCamera != null && cachedMainCamera.isActiveAndEnabled)
            return cachedMainCamera;

        if (cachedMainCameraFrame == Time.frameCount)
            return cachedMainCamera;

        cachedMainCameraFrame = Time.frameCount;
        cachedMainCamera = Camera.main;
        return cachedMainCamera;
    }
}
