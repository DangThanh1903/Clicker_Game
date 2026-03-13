public enum DamageInputKind
{
    Click = 0,
    Hold = 1,
    Idle = 2
}

public interface IDamageReceiver
{
    public void ApplyDamageInput(DamageInputKind inputKind);
    int InputPriority { get; }
    bool CanReceiveDamage { get; }
}

public interface IPointerHitContext
{
    void SetPointerHit(UnityEngine.Vector3 worldPoint);
}
