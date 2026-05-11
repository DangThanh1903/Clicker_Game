using UnityEngine;

public interface IPetAutoAttackFeedback
{
    void PlayAutoAttack(float damage, Vector3 targetWorldPosition);
}
