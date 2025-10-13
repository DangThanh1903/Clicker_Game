using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(menuName="Boss/Skill")]
public class BossSkillDef : ScriptableObject
{
    public string skillId = "Normal";

    [Header("Body Motion (relative to base pose)")]
    public Vector3 moveLocal = Vector3.zero;   // delta from base pose
    public float   moveTime  = 0.25f;
    public Vector3 scaleTo   = Vector3.one;    // absolute target scale
    public float   scaleTime = 0.25f;
    public Vector3 rotateEuler = Vector3.zero; // absolute local euler
    public float   rotateTime  = 0.25f;

    [Header("Timing")]
    public float fireMoment   = 0.3f;
    public float recoverTime  = 0.2f;
    public float cooldown     = 3.0f;

    public Sequence Build(Transform modelRoot, BossAnimManager.LocalPose basePose, System.Action onFire)
    {
        var seq = DOTween.Sequence();

        // Attack body (relative to cached base pose)
        if (moveLocal != Vector3.zero)
            seq.Append(modelRoot.DOLocalMove(basePose.pos + moveLocal, moveTime));
        if (scaleTo != Vector3.one)
            seq.Join(modelRoot.DOScale(scaleTo, scaleTime));
        if (rotateEuler != Vector3.zero)
            seq.Join(modelRoot.DOLocalRotate(rotateEuler, rotateTime, RotateMode.Fast));

        // Fire callback at impact
        if (fireMoment > 0f)
            seq.Insert(fireMoment, DOVirtual.DelayedCall(0f, () => onFire?.Invoke()));

        // Recover back to base pose
        if (recoverTime > 0f)
        {
            seq.AppendInterval(0.02f);
            seq.Append(modelRoot.DOLocalMove(basePose.pos, recoverTime));
            seq.Join(modelRoot.DOScale(basePose.scale, recoverTime));
            seq.Join(modelRoot.DOLocalRotateQuaternion(basePose.rot, recoverTime));
        }

        return seq;
    }
}
