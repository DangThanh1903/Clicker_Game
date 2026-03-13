using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class ChestWorldInteractable : MonoBehaviour, IWorldInteractable
{
    [SerializeField] private bool canInteract = true;
    [SerializeField] private bool oneShot = false;
    [SerializeField] private UnityEvent onInteracted;

    private bool used;
    public bool CanInteract => canInteract && (!oneShot || !used);

    public void Interact()
    {
        if (!CanInteract)
            return;

        used = true;
        onInteracted?.Invoke();
        InventoryPopupRuntime.OpenInventoryPopup();
    }
}
