using UnityEngine;

[DisallowMultipleComponent]
public sealed class DamageTargetRegistrant : MonoBehaviour
{
    private IDamageReceiver cachedTarget;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool hasLoggedMissingTarget;
#endif

    private void Awake()
    {
        cachedTarget = ResolveTarget();
    }

    private void OnEnable()
    {
        cachedTarget = ResolveTarget();
        if (cachedTarget == null)
            return;

        DamageTargetRegistry.Register(cachedTarget);
    }

    private void OnDisable()
    {
        if (cachedTarget == null)
            return;

        DamageTargetRegistry.Unregister(cachedTarget);
    }

    private IDamageReceiver ResolveTarget()
    {
        IDamageReceiver target = GetComponent(typeof(IDamageReceiver)) as IDamageReceiver;
        if (target == null)
            LogMissingTarget($"No IDamageReceiver found on '{name}'.");
        return target;
    }

    private void LogMissingTarget(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (hasLoggedMissingTarget)
            return;

        hasLoggedMissingTarget = true;
        Debug.LogError($"[DamageTargetRegistrant] {message}", this);
#endif
    }
}
