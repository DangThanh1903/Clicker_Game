using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName="RandomRotateClickAnim", menuName="Block/Anim/Click/RandomRotate")]
public class RandomRotateClickAnim : BlockAnimationAsset
{
    [Header("Rotation")]
    [Tooltip("Max random degrees offset applied on click (per axis).")]
    public float maxAngle = 15f;
    [Tooltip("How long the rotation takes to play forward and back.")]
    public float duration = 0.25f;

    public override bool IsLooping => false;
    public override float EstimatedDuration => duration;

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);
        var t = target.transform;

        // Pick a random local rotation offset
        Vector3 randomOffset = new Vector3(
            Random.Range(-maxAngle, maxAngle),
            Random.Range(-maxAngle, maxAngle),
            Random.Range(-maxAngle, maxAngle)
        );

        Quaternion startRot = t.localRotation;
        Quaternion endRot   = startRot * Quaternion.Euler(randomOffset);

        // Animate to the random rotation and back
        var seq = DOTween.Sequence()
            .Append(t.DOLocalRotateQuaternion(endRot, duration * 0.5f)
                .SetEase(Ease.OutQuad))
            .Append(t.DOLocalRotateQuaternion(startRot, duration * 0.5f)
                .SetEase(Ease.InQuad))
            .SetId(TweenIdFor(target))
            .SetLink(target);

        return seq;
    }
}
