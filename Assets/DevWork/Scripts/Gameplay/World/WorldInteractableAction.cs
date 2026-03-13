using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class WorldInteractableAction : MonoBehaviour, IWorldInteractable
{
    [SerializeField] private bool canInteract = true;
    [SerializeField] private UnityEvent onInteract;

    public bool CanInteract => canInteract;

    public void Interact()
    {
        if (!canInteract)
            return;

        onInteract?.Invoke();
    }
}
