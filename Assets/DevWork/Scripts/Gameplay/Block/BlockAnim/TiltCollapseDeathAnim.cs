using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName="TiltCollapseDeathAnim", menuName="Block/Anim/Death/TiltCollapse")]
public class TiltCollapseDeathAnim : BlockAnimationAsset
{
    [Header("Timings")]
    [Min(0.01f)] public float tiltTime   = 0.18f;  // initial tip
    [Min(0.01f)] public float collapseTime = 0.22f; // collapse to zero

    [Header("Tilt")]
    [Tooltip("Max random degrees to tilt around X and Z.")]
    public float maxTiltAngle = 25f;
    [Tooltip("Extra tilt added while collapsing.")]
    public float collapseExtraTilt = 10f;

    [Header("Drop")]
    [Tooltip("How far the block drops down while collapsing (local Y).")]
    public float dropDistance = 0.35f;

    [Header("Squash (at the start of collapse)")]
    [Range(0.1f, 1.0f)] public float squashY = 0.55f;

    [Header("Scale")]
    [Tooltip("Final scale when finished (usually zero).")]
    public Vector3 finalScale = Vector3.zero;

    [Header("Rotation Reset")]
    [Tooltip("If true, rotation is reset before anim for consistent tip.")]
    public bool resetRotationBefore = false;

    public override bool IsLooping => false;
    public override float EstimatedDuration => tiltTime + collapseTime;

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);
        var t = target.transform;

        // Cache original state
        var startPos = t.localPosition;
        var startRot = t.localRotation;
        var startScale = t.localScale;

        if (resetRotationBefore) t.localRotation = Quaternion.identity;

        // Choose random tilt around X and Z (no Y spin so it "tips" rather than "spins")
        float tiltX = Random.Range(-maxTiltAngle, maxTiltAngle);
        float tiltZ = Random.Range(-maxTiltAngle, maxTiltAngle);
        var firstTilt = Quaternion.Euler(tiltX, 0f, tiltZ);

        // During collapse, add a bit more tilt in the same direction
        var extraTilt = Quaternion.Euler(
            Mathf.Sign(tiltX) * collapseExtraTilt,
            0f,
            Mathf.Sign(tiltZ) * collapseExtraTilt
        );

        var afterTilt = firstTilt * extraTilt;

        // Sequence:
        // 1) TIP: rotate quickly to firstTilt (no scaling yet)
        // 2) COLLAPSE: squash Y, move down, rotate a bit more, then scale to zero
        var seq = DOTween.Sequence()
            .SetId(TweenIdFor(target))
            .SetLink(target);

        // Step 1: Tip
        seq.Append(t.DOLocalRotateQuaternion(startRot * firstTilt, tiltTime).SetEase(Ease.OutCubic));

        // Step 2: Collapse (parallel tweens)
        // 2a) Extra tilt while collapsing
        seq.Append(t.DOLocalRotateQuaternion(startRot * afterTilt, collapseTime).SetEase(Ease.InCubic));

        // 2b) Position drop
        seq.Join(t.DOLocalMove(startPos + new Vector3(0f, -Mathf.Abs(dropDistance), 0f), collapseTime)
                 .SetEase(Ease.InCubic));

        // 2c) Squash at start of collapse, then shrink away
        //     We do two scales: quick squash, then to finalScale
        var squashScale = new Vector3(startScale.x, startScale.y * squashY, startScale.z);
        float squashPortion = Mathf.Clamp01(0.35f); // first 35% of collapse time to squash
        seq.Join(t.DOScale(squashScale, collapseTime * squashPortion).SetEase(Ease.InQuad));
        seq.Join(t.DOScale(finalScale, collapseTime).SetEase(Ease.InCubic));

        return seq;
    }
}
