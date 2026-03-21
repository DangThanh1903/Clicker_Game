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

public interface ISpinHitContext
{
    bool TryGetPointerScreenDirectionFromCenter(out UnityEngine.Vector2 direction, out float distancePx, int maxAgeFrames = -1);
    bool TryGetPointerTorqueWorldAxis(out UnityEngine.Vector3 worldAxis, int maxAgeFrames = -1);
    bool TryGetRecentDamageRatioNormalized(out float ratio01, int maxAgeFrames = 2);
    BlockMomentumSpinDriver MomentumSpinDriver { get; }
}
