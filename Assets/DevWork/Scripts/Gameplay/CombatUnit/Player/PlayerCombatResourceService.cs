using UnityEngine;

public sealed class PlayerCombatResourceService
{
    private const float DefaultStaminaRegenPercentPerSecond = 0.25f;
    private const float RegenStartSentinelSeconds = 999f;

    private readonly float staminaRegenPercentPerSecond;
    private float timeSinceLastManualClick = RegenStartSentinelSeconds;

    public PlayerCombatResourceService(
        float staminaRegenPercentPerSecond = DefaultStaminaRegenPercentPerSecond)
    {
        this.staminaRegenPercentPerSecond = Mathf.Max(0f, staminaRegenPercentPerSecond);
    }

    public void InitializeResources()
    {
        if (StatsManager.Ins == null)
            return;

        StatsManager.Ins.Set(StatType.CurrentMana, StatsManager.Ins.Get(StatType.Mana));
        StatsManager.Ins.Set(StatType.CurrentStamina, StatsManager.Ins.Get(StatType.Stamina));
    }

    public void ResetRuntime()
    {
        timeSinceLastManualClick = RegenStartSentinelSeconds;
    }

    public void Tick(bool isDead, float combatDelta)
    {
        UpdateStaminaOverTime(isDead, Mathf.Max(0f, combatDelta));
    }

    public float ApplyStaminaToFinalDamage(float finalClickDamage, bool isManualCombat)
    {
        float safeFinalDamage = Mathf.Max(0f, finalClickDamage);
        if (safeFinalDamage <= 0f)
            return 0f;

        if (!isManualCombat)
            return safeFinalDamage;

        if (StatsManager.Ins == null)
            return safeFinalDamage;

        timeSinceLastManualClick = 0f;

        float staminaCost = Mathf.Max(0.1f, StatsManager.Ins.Get(StatType.StaminaCostPerClick));
        float currentStamina = Mathf.Max(0f, StatsManager.Ins.Get(StatType.CurrentStamina));
        bool ignoreStaminaEffect = StatsManager.Ins.Get(StatType.IgnoreStaminaEffect) > 0f;

        if (currentStamina >= staminaCost)
        {
            StatsManager.Ins.Set(StatType.CurrentStamina, Mathf.Max(0f, currentStamina - staminaCost));

            if (ignoreStaminaEffect)
                return safeFinalDamage;

            float haveStaminaMul = Mathf.Max(0f, StatsManager.Ins.Get(StatType.HaveStaminaDamageMul));
            return safeFinalDamage * Mathf.Max(0f, haveStaminaMul);
        }

        if (ignoreStaminaEffect)
            return safeFinalDamage;

        float multiplier = Mathf.Max(0f, StatsManager.Ins.Get(StatType.LowStaminaDamageMultiplier));
        return safeFinalDamage * Mathf.Max(0f, multiplier);
    }

    public float GetStaminaPercent()
    {
        if (StatsManager.Ins == null)
            return 0f;

        float maxStamina = Mathf.Max(0f, StatsManager.Ins.Get(StatType.Stamina));
        if (maxStamina <= 0f)
            return 0f;

        float currentStamina = Mathf.Max(0f, StatsManager.Ins.Get(StatType.CurrentStamina));
        return Mathf.Clamp01(currentStamina / maxStamina);
    }

    private void UpdateStaminaOverTime(bool isDead, float combatDelta)
    {
        if (isDead || StatsManager.Ins == null)
            return;

        timeSinceLastManualClick += combatDelta;

        float maxStamina = Mathf.Max(0f, StatsManager.Ins.Get(StatType.Stamina));
        float currentStamina = Mathf.Max(0f, StatsManager.Ins.Get(StatType.CurrentStamina));
        float regenTick = Mathf.Max(0f, StatsManager.Ins.Get(StatType.StaminaRegenTick));

        if (maxStamina <= 0f || currentStamina >= maxStamina)
            return;

        if (timeSinceLastManualClick < regenTick)
            return;

        float staminaRegenPerSecond = maxStamina * staminaRegenPercentPerSecond;
        currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenPerSecond * combatDelta);
        StatsManager.Ins.Set(StatType.CurrentStamina, currentStamina);
    }
}
