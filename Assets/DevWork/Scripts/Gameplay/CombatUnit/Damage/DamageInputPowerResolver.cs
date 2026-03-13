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

        return readModel.ApplyStaminaToFinalDamage(finalDamage);
    }

    public static float GetHoldTickPower(float tickSeconds)
    {
        if (!CombatResourceReadModelRuntime.TryGet(out ICombatResourceReadModel readModel))
            return 0f;

        float holdPower = StatsManager.Ins != null
            ? StatsManager.Ins.Get(StatType.HoldPower)
            : 0f;

        float manaMul = readModel.GetHoldDamageMultiplier();
        return holdPower * manaMul * tickSeconds;
    }

    public static float GetIdleTickPower(float tickSeconds)
    {
        if (!CombatResourceReadModelRuntime.TryGet(out ICombatResourceReadModel readModel))
            return 0f;

        float idlePower = StatsManager.Ins != null
            ? StatsManager.Ins.Get(StatType.IdlePower)
            : 0f;

        float idleMul = readModel.GetIdleDamageMultiplier();
        return idlePower * idleMul * tickSeconds;
    }

    public static float GetInputDeltaTime()
    {
        if (!CombatResourceReadModelRuntime.TryGet(out ICombatResourceReadModel readModel))
            return 0f;

        return readModel.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
