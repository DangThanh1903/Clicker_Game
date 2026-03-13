using UnityEngine;

[DisallowMultipleComponent]
public sealed class DamageTargetRegistrant : MonoBehaviour
{
    private IDamageReceiver cachedTarget;
    private bool isRegistered;
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

        CombatRuntimeBootstrap.TargetRegistryBound -= HandleTargetRegistryBound;
        CombatRuntimeBootstrap.TargetRegistryBound += HandleTargetRegistryBound;
        TryRegisterToRuntimeRegistry();
    }

    private void OnDisable()
    {
        CombatRuntimeBootstrap.TargetRegistryBound -= HandleTargetRegistryBound;

        if (!isRegistered || cachedTarget == null)
        {
            isRegistered = false;
            return;
        }

        if (CombatRuntimeBootstrap.TryGetTargetRegistryWriter(out ITargetRegistryWriter registry, logIfMissing: false))
            registry.Unregister(cachedTarget);

        isRegistered = false;
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

    private void TryRegisterToRuntimeRegistry()
    {
        if (isRegistered || cachedTarget == null)
            return;

        if (!CombatRuntimeBootstrap.TryGetTargetRegistryWriter(out ITargetRegistryWriter registry, logIfMissing: false))
            return;

        registry.Register(cachedTarget);
        isRegistered = true;
    }

    private void HandleTargetRegistryBound(ITargetRegistryWriter registry)
    {
        if (!isActiveAndEnabled || cachedTarget == null || isRegistered || registry == null)
            return;

        registry.Register(cachedTarget);
        isRegistered = true;
    }
}
