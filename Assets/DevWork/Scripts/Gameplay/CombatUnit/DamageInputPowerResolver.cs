using UnityEngine;

public static class DamageInputPowerResolver
{
    public static float GetClickPower()
    {
        float finalDamage = StatsManager.Ins.Get(StatType.NormalPower);
        return PlayerController.Instance != null
            ? PlayerController.Instance.ApplyStaminaToFinalDamage(finalDamage)
            : finalDamage;
    }

    public static float GetHoldTickPower(float tickSeconds)
    {
        float manaMul = PlayerController.Instance != null
            ? PlayerController.Instance.GetHoldDamageMultiplier()
            : 1f;
        return StatsManager.Ins.Get(StatType.HoldPower) * manaMul * tickSeconds;
    }

    public static float GetIdleTickPower(float tickSeconds)
    {
        float idleMul = PlayerController.Instance != null
            ? PlayerController.Instance.GetIdleDamageMultiplier()
            : 1f;
        return StatsManager.Ins.Get(StatType.IdlePower) * idleMul * tickSeconds;
    }

    public static float GetInputDeltaTime()
    {
        var player = PlayerController.Instance;
        return player != null && player.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
