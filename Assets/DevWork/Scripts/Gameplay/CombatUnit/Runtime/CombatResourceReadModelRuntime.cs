public interface ICombatResourceReadModel
{
    float ApplyStaminaToFinalDamage(float finalClickDamage);
    bool UseUnscaledTime { get; }
}

public static class CombatResourceReadModelRuntime
{
    public static bool TryGet(out ICombatResourceReadModel model)
    {
        return CombatRuntimeBootstrap.TryGetReadModel(out model, logIfMissing: true);
    }
}
