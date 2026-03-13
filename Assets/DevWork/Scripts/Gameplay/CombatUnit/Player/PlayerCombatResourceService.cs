using UnityEngine;

public sealed class PlayerCombatResourceService
{
    private const float DefaultTimeManaReset = 0.1f;
    private const float DefaultStaminaRegenPercentPerSecond = 0.25f;
    private const float IdleResetSentinelSeconds = 999f;

    private readonly bool resetIdleStackOnStateChange;
    private readonly float timeManaReset;
    private readonly float staminaRegenPercentPerSecond;

    private float manaRegenTimer;
    private float manaUsageTimer;
    private float timeSinceLastNormalClick = IdleResetSentinelSeconds;
    private float timeSinceIdleStackRefresh = IdleResetSentinelSeconds;
    private int idleStackCount;

    public PlayerCombatResourceService(
        bool resetIdleStackOnStateChange = true,
        float timeManaReset = DefaultTimeManaReset,
        float staminaRegenPercentPerSecond = DefaultStaminaRegenPercentPerSecond)
    {
        this.resetIdleStackOnStateChange = resetIdleStackOnStateChange;
        this.timeManaReset = Mathf.Max(0.01f, timeManaReset);
        this.staminaRegenPercentPerSecond = Mathf.Max(0f, staminaRegenPercentPerSecond);
    }

    public int IdleStackCount => idleStackCount;

    public void InitializeResources()
    {
        if (StatsManager.Ins == null)
            return;

        StatsManager.Ins.Set(StatType.CurrentMana, StatsManager.Ins.Get(StatType.Mana));
        StatsManager.Ins.Set(StatType.CurrentStamina, StatsManager.Ins.Get(StatType.Stamina));
    }

    public void ResetRuntime()
    {
        manaRegenTimer = 0f;
        manaUsageTimer = 0f;
        timeSinceLastNormalClick = IdleResetSentinelSeconds;
        ResetIdleStack();
    }

    public void OnStateChanged()
    {
        if (!resetIdleStackOnStateChange)
            return;

        ResetIdleStack();
    }

    public void OnClickDispatched(bool isIdleState)
    {
        if (!isIdleState)
            return;

        int maxStack = GetIdleMaxStack();
        if (maxStack <= 0)
            return;

        idleStackCount = Mathf.Min(maxStack, idleStackCount + 1);
        timeSinceIdleStackRefresh = 0f;
    }

    public void Tick(bool isDead, bool isHoldState, float combatNow, float combatDelta, float lastHoldUpdateTime)
    {
        float dt = Mathf.Max(0f, combatDelta);

        UpdateIdleStackLifetime(dt);
        UpdateStaminaOverTime(isDead, dt);

        if (!isDead &&
            isHoldState &&
            combatNow - lastHoldUpdateTime > 0.08f)
        {
            RegenMana(dt);
        }
    }

    public void ConsumeMana(float combatDelta)
    {
        if (StatsManager.Ins == null)
            return;

        manaUsageTimer += Mathf.Max(0f, combatDelta);

        if (manaUsageTimer < timeManaReset)
            return;

        float manaLostPerSecond = 10f * timeManaReset;
        StatsManager.Ins.Set(
            StatType.CurrentMana,
            Mathf.Max(StatsManager.Ins.Get(StatType.CurrentMana) - manaLostPerSecond, 0f));

        manaUsageTimer = 0f;
    }

    public float ApplyStaminaToFinalDamage(float finalClickDamage, bool isNormalState)
    {
        float safeFinalDamage = Mathf.Max(0f, finalClickDamage);
        if (safeFinalDamage <= 0f)
            return 0f;

        if (!isNormalState)
            return safeFinalDamage;

        if (StatsManager.Ins == null)
            return safeFinalDamage;

        timeSinceLastNormalClick = 0f;

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

    public float GetHoldDamageMultiplier()
    {
        if (StatsManager.Ins == null)
            return 1f;

        float maxMana = Mathf.Max(0f, StatsManager.Ins.Get(StatType.Mana));
        if (maxMana <= 0f)
            return 1f;

        float currentMana = Mathf.Max(0f, StatsManager.Ins.Get(StatType.CurrentMana));
        float manaPercent = currentMana / maxMana;
        float highThreshold = Mathf.Clamp01(GetStatOrDefault(StatType.HighManaThreshold, 0.5f));
        float middleThreshold = Mathf.Clamp01(GetStatOrDefault(StatType.MiddleManaThreshold, 0.2f));
        middleThreshold = Mathf.Min(middleThreshold, highThreshold);

        if (manaPercent >= highThreshold)
        {
            float high = StatsManager.Ins.Get(StatType.HighManaMul);
            return Mathf.Max(1f, high);
        }

        if (manaPercent >= middleThreshold)
        {
            float middle = StatsManager.Ins.Get(StatType.MiddleManaMul);
            return Mathf.Max(1f, middle);
        }

        return 1f;
    }

    public float GetIdleDamageMultiplier()
    {
        int maxStack = GetIdleMaxStack();
        if (maxStack <= 0 || idleStackCount <= 0)
            return 1f;

        int effectiveStack = Mathf.Clamp(idleStackCount, 0, maxStack);
        float perStack = GetIdleStackDamagePerStack();
        return 1f + effectiveStack * perStack;
    }

    public float GetIdleStackPercent()
    {
        int maxStack = GetIdleMaxStack();
        if (maxStack <= 0)
            return 0f;

        return Mathf.Clamp01((float)idleStackCount / maxStack);
    }

    private void RegenMana(float combatDelta)
    {
        if (StatsManager.Ins == null)
            return;

        manaRegenTimer += Mathf.Max(0f, combatDelta);

        if (manaRegenTimer < timeManaReset)
            return;

        float manaRegenerationPerSecond = StatsManager.Ins.Get(StatType.ManaRegen) * timeManaReset;
        StatsManager.Ins.Set(
            StatType.CurrentMana,
            Mathf.Min(
                StatsManager.Ins.Get(StatType.CurrentMana) + manaRegenerationPerSecond,
                StatsManager.Ins.Get(StatType.Mana)));

        manaRegenTimer = 0f;
    }

    private void UpdateStaminaOverTime(bool isDead, float combatDelta)
    {
        if (isDead || StatsManager.Ins == null)
            return;

        timeSinceLastNormalClick += combatDelta;

        float maxStamina = Mathf.Max(0f, StatsManager.Ins.Get(StatType.Stamina));
        float currentStamina = Mathf.Max(0f, StatsManager.Ins.Get(StatType.CurrentStamina));
        float regenTick = Mathf.Max(0f, StatsManager.Ins.Get(StatType.StaminaRegenTick));

        if (maxStamina <= 0f || currentStamina >= maxStamina)
            return;

        if (timeSinceLastNormalClick < regenTick)
            return;

        float staminaRegenPerSecond = maxStamina * staminaRegenPercentPerSecond;
        currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenPerSecond * combatDelta);
        StatsManager.Ins.Set(StatType.CurrentStamina, currentStamina);
    }

    private void UpdateIdleStackLifetime(float combatDelta)
    {
        if (idleStackCount <= 0)
            return;

        timeSinceIdleStackRefresh += combatDelta;

        float resetTime = GetIdleStackResetTime();
        if (timeSinceIdleStackRefresh >= resetTime)
            ResetIdleStack();
    }

    private void ResetIdleStack()
    {
        idleStackCount = 0;
        timeSinceIdleStackRefresh = IdleResetSentinelSeconds;
    }

    private int GetIdleMaxStack()
    {
        float maxStack = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.IdleMaxStack) : 0f;
        return Mathf.Max(0, Mathf.RoundToInt(maxStack));
    }

    private float GetIdleStackDamagePerStack()
    {
        float perStack = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.IdleStackDamagePerStack) : 0f;
        return Mathf.Max(0f, perStack);
    }

    private float GetIdleStackResetTime()
    {
        float resetTime = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.IdleStackResetTime) : 0f;
        return Mathf.Max(0.1f, resetTime);
    }

    private float GetStatOrDefault(StatType statType, float defaultValue)
    {
        if (StatsManager.Ins == null)
            return defaultValue;

        float value = StatsManager.Ins.Get(statType);
        return value > 0f ? value : defaultValue;
    }
}
