using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName="SpinInSpawnAnim", menuName="Block/Anim/Spawn/Spin-In")]
public class SpinInSpawnAnim : BlockAnimationAsset
{
    [Header("Scale")]
    [ReadOnly] public Vector3 endScale = new(2.5f, 2.5f, 2.5f);
    public float scaleDuration = 0.35f;
    public Ease scaleEase = Ease.OutBack;

    [Header("Rotation")]
    [Tooltip("How long the rotation takes (can be same as scaleDuration).")]
    public float rotateDuration = 0.35f;
    public Ease rotateEase = Ease.OutCubic;

    [Tooltip("Spin amount in degrees. If Randomize is on, this is the max absolute degrees per axis.")]
    public Vector3 spinDegrees = new(0f, 360f, 0f);

    [Tooltip("If true, start from a random rotation offset (±spinDegrees per axis). If false, spin exactly spinDegrees and settle at 0.")]
    public bool randomizeStartRotation = true;

    public override bool IsLooping => false;
    public override float EstimatedDuration => Mathf.Max(scaleDuration, rotateDuration);

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);
        var t = target.transform;

        // Reset to a clean base
        t.localScale = Vector3.zero;

        // Rotation setup
        Quaternion startRot;
        if (randomizeStartRotation)
        {
            var rand = new Vector3(
                Random.Range(-Mathf.Abs(spinDegrees.x), Mathf.Abs(spinDegrees.x)),
                Random.Range(-Mathf.Abs(spinDegrees.y), Mathf.Abs(spinDegrees.y)),
                Random.Range(-Mathf.Abs(spinDegrees.z), Mathf.Abs(spinDegrees.z))
            );
            startRot = Quaternion.Euler(rand);
        }
        else
        {
            // Start at 0, then perform a forward spin to settle back at 0
            startRot = Quaternion.identity;
        }
        t.localRotation = startRot;

        // Target rotation is always identity (0,0,0) to end perfectly aligned
        var endRot = Quaternion.identity;

        // Build a sequence so we return a single Tween
        var seq = DOTween.Sequence().SetId(TweenIdFor(target)).SetLink(target);

        // Scale pop-in
        seq.Join(t.DOScale(endScale, scaleDuration).SetEase(scaleEase));

        // Rotation to zero (if not already)
        // For non-randomized spin feel, you can add Beyond360 using Euler if desired:
        if (!randomizeStartRotation && (spinDegrees != Vector3.zero))
        {
            // Spin Beyond360 then settle at 0
            seq.Join(t.DOLocalRotate(spinDegrees, rotateDuration, RotateMode.FastBeyond360)
                     .SetEase(rotateEase))
               .Append(t.DOLocalRotate(Vector3.zero, 0.0001f)); // ensure ends at 0
        }
        else
        {
            // From random offset back to 0
            seq.Join(t.DOLocalRotateQuaternion(endRot, rotateDuration).SetEase(rotateEase));
        }

        return seq;
    }
}
