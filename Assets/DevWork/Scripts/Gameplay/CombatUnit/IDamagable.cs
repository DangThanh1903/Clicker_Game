public enum DamageInputKind
{
    Click = 0,
    Hold = 1,
    Idle = 2
}

public interface IDamagable
{
    public void ApplyDamageInput(DamageInputKind inputKind);
    int InputPriority { get; }
    bool CanReceiveDamage { get; }
    void SetPointerHit(UnityEngine.Vector3 worldPoint);
}
