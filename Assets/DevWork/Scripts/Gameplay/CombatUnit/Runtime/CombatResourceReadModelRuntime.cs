using UnityEngine;

public interface ICombatResourceReadModel
{
    float ApplyStaminaToFinalDamage(float finalClickDamage);
    float GetHoldDamageMultiplier();
    float GetIdleDamageMultiplier();
    bool UseUnscaledTime { get; }
}

public static class CombatResourceReadModelRuntime
{
    private static ICombatResourceReadModel readModel;
    private static bool hasLoggedMissingBinding;

    public static void Bind(ICombatResourceReadModel model)
    {
        if (model == null)
        {
            Debug.LogError("[CombatResourceReadModelRuntime] Cannot bind null read model.");
            return;
        }

        readModel = model;
        hasLoggedMissingBinding = false;
    }

    public static void Unbind(ICombatResourceReadModel model)
    {
        if (!ReferenceEquals(readModel, model))
            return;

        readModel = null;
    }

    public static bool TryGet(out ICombatResourceReadModel model)
    {
        model = readModel;
        if (model != null)
            return true;

        if (!hasLoggedMissingBinding)
        {
            hasLoggedMissingBinding = true;
            Debug.LogError("[CombatResourceReadModelRuntime] No read model bound. Ensure PlayerController binds at runtime.");
        }
        return false;
    }
}
