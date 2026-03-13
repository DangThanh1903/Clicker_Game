using UnityEngine;

public sealed class PlayerIdleAttackTickService
{
    private const int MaxIdleTicksPerUpdate = 3;

    private float idleAttackTimer;

    public void OnStateChanged(bool isIdleState, float firstAttackInterval)
    {
        idleAttackTimer = isIdleState ? Mathf.Max(0f, firstAttackInterval) : 0f;
    }

    public void ResetRuntime()
    {
        idleAttackTimer = 0f;
    }

    public void TickAndDispatch(
        IDamageReceiver target,
        bool isIdleState,
        bool isDead,
        float combatDelta,
        float attackInterval)
    {
        if (target == null || !isIdleState || isDead)
            return;

        float interval = Mathf.Max(0.0001f, attackInterval);
        float dt = Mathf.Max(0f, combatDelta);
        idleAttackTimer += dt;
        if (idleAttackTimer < interval)
            return;

        int ticks = Mathf.Min(MaxIdleTicksPerUpdate, Mathf.FloorToInt(idleAttackTimer / interval));
        idleAttackTimer -= ticks * interval;

        for (int i = 0; i < ticks; i++)
            target.ApplyDamageInput(DamageInputKind.Idle);
    }
}
