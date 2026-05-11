using UnityEngine;

public sealed class PlayerAutoAttackTickService
{
    private const int MaxAutoAttackTicksPerUpdate = 3;

    private float autoAttackTimer;

    public void OnCombatModeChanged(bool autoCombatEnabled, float firstAttackInterval)
    {
        autoAttackTimer = autoCombatEnabled ? Mathf.Max(0f, firstAttackInterval) : 0f;
    }

    public void ResetRuntime()
    {
        autoAttackTimer = 0f;
    }

    public void TickAndDispatch(
        IDamageReceiver target,
        bool autoCombatEnabled,
        bool isDead,
        float combatDelta,
        float attackInterval)
    {
        if (target == null || !autoCombatEnabled || isDead)
            return;

        float interval = Mathf.Max(0.0001f, attackInterval);
        float dt = Mathf.Max(0f, combatDelta);
        autoAttackTimer += dt;
        if (autoAttackTimer < interval)
            return;

        int ticks = Mathf.Min(MaxAutoAttackTicksPerUpdate, Mathf.FloorToInt(autoAttackTimer / interval));
        autoAttackTimer -= ticks * interval;

        for (int i = 0; i < ticks; i++)
            target.ApplyDamageInput(DamageInputKind.AutoAttack);
    }
}
