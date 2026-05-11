using UnityEngine;

public static class DamageInputPowerResolver
{
    public static float GetClickPower()
    {
        if (!CombatResourceReadModelRuntime.TryGet(out ICombatResourceReadModel readModel))
            return 0f;

        float finalDamage = StatsManager.Ins != null
            ? StatsManager.Ins.Get(StatType.NormalPower)
            : 0f;
        finalDamage = Mathf.Max(1f, finalDamage);

        return readModel.ApplyStaminaToFinalDamage(finalDamage);
    }

    public static float GetAutoAttackPower()
    {
        if (!CombatResourceReadModelRuntime.TryGet(out _))
            return 0f;

        float autoAttackPower = StatsManager.Ins != null
            ? StatsManager.Ins.Get(StatType.NormalPower)
            : 0f;
        autoAttackPower = Mathf.Max(1f, autoAttackPower);
        return autoAttackPower;
    }

    public static float GetInputDeltaTime()
    {
        if (!CombatResourceReadModelRuntime.TryGet(out ICombatResourceReadModel readModel))
            return 0f;

        return readModel.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
