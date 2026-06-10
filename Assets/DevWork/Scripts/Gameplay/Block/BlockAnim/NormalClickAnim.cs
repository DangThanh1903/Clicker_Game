using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName="NormalClickAnim", menuName="Block/Anim/Click/Normal")]
public class NormalClickAnim : BlockAnimationAsset
{
    private static bool hasLoggedMissingSpinDriver;

    [Header("Squash")]
    public float squashScale = 0.9f;
    public float duration = 0.15f;
    [Tooltip("If true, force using fixed baseScale. Keep false to respect current block scale (recommended).")]
    public bool useFixedBaseScale = false;
    public Vector3 baseScale = new(2.5f, 2.5f, 2.5f);

    [Header("Random Rotation")]
    public bool enableRandomRotation = true;
    public Vector3 randomRotationDegrees = new(8f, 12f, 12f);

    [Header("Pointer Hit Momentum")]
    public bool rotateByMouseDirection = true;
    [Min(1f)] public float momentumMaxDistancePx = 220f;
    [Range(0f, 1f)] public float minMomentum = 0.25f;
    [Range(0.05f, 3f)] public float maxMomentum = 1f;
    [Min(0.5f)] public float momentumCurvePower = 1.4f;
    [Min(0.5f)] public float pointerSensitivity = 1.25f;
    [Tooltip("Flip camera-space physical torque direction if it feels reversed in your scene setup.")]
    public bool invertPhysicalPointerDirection = false;
    public bool addRandomJitter = true;
    public Vector3 randomJitterDegrees = new(1.5f, 2f, 2f);

    [Header("Damage Ratio Impulse")]
    [Tooltip("If enabled, impulse strength follows damage ratio (damage / maxHP).")]
    public bool scaleImpulseByDamageRatio = true;
    [Range(0f, 1f), Tooltip("1 = use only damage ratio for impulse strength. 0 = use pointer momentum only.")]
    public float damageRatioBlend = 1f;
    [Min(0f), Tooltip("Scale damage ratio before clamp to 0..1.")]
    public float damageRatioScale = 40f;
    [Min(0.1f)] public float damageRatioCurvePower = 0.85f;

    [Header("Momentum Spin")]
    [Tooltip("Angular velocity injected per click at minimum momentum (deg/sec).")]
    [Min(0f)] public float minSpinImpulseSpeed = 140f;
    [Tooltip("Angular velocity injected per click at maximum momentum (deg/sec).")]
    [Min(0f)] public float maxSpinImpulseSpeed = 920f;
    [Tooltip("Higher value = slows down faster. Lower value = longer inertia.")]
    [Min(0.1f)] public float spinAngularDamping = 7.5f;
    [Tooltip("Hard clamp for stacked click impulses (deg/sec). Set 0 for unlimited.")]
    [Min(0f)] public float spinMaxAngularSpeed = 900f;
    [Tooltip("Lower value keeps tiny residual spin longer (more flexible tail).")]
    [Min(0.001f)] public float spinStopSpeedThreshold = 1.2f;
    public bool invertSpinDirection = true;

    public override bool IsLooping => false;
    public override float EstimatedDuration => duration;

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);
        var t = target.transform;
        ISpinHitContext spinContext = target.GetComponent(typeof(ISpinHitContext)) as ISpinHitContext;
        Vector3 baseline = useFixedBaseScale ? baseScale : t.localScale;

        t.localScale = baseline;
        var seq = DOTween.Sequence();

        bool shouldScale = Mathf.Abs(squashScale - 1f) > 0.0001f;
        if (shouldScale)
        {
            float safeScaleDuration = Mathf.Max(0.01f, duration);
            float half = safeScaleDuration * 0.5f;
            Tween scaleDown = t.DOScale(baseline * squashScale, half).SetEase(Ease.InQuad);
            Tween scaleUp = t.DOScale(baseline, half).SetEase(Ease.OutBack);
            var scaleSeq = DOTween.Sequence()
                .Append(scaleDown)
                .Append(scaleUp);
            seq.Append(scaleSeq);
        }

        bool shouldRotate = rotateByMouseDirection || enableRandomRotation;
        if (shouldRotate)
        {
            Vector3 spinDirection = Vector3.zero;
            float momentum = 1f;
            bool hasPointerDirection = false;

            if (rotateByMouseDirection &&
                TryGetPointerDrivenSpin(spinContext, out Vector3 pointerSpinAxis, out momentum))
            {
                hasPointerDirection = true;
                spinDirection += pointerSpinAxis;
            }

            if (!hasPointerDirection && enableRandomRotation)
            {
                Vector3 randomLocal = new Vector3(
                    Random.Range(-randomRotationDegrees.x, randomRotationDegrees.x),
                    Random.Range(-randomRotationDegrees.y, randomRotationDegrees.y),
                    Random.Range(-randomRotationDegrees.z, randomRotationDegrees.z));
                spinDirection += t.TransformDirection(randomLocal);
            }
            else if (!hasPointerDirection && addRandomJitter)
            {
                Vector3 jitterLocal = new Vector3(
                    Random.Range(-randomJitterDegrees.x, randomJitterDegrees.x),
                    Random.Range(-randomJitterDegrees.y, randomJitterDegrees.y),
                    Random.Range(-randomJitterDegrees.z, randomJitterDegrees.z));
                spinDirection += t.TransformDirection(jitterLocal) * momentum;
            }

            if (spinDirection.sqrMagnitude > 0.000001f)
            {
                if (hasPointerDirection)
                {
                    if (invertPhysicalPointerDirection)
                        spinDirection = -spinDirection;
                }
                else if (invertSpinDirection)
                    spinDirection = -spinDirection;

                spinDirection.Normalize();

                float safeMaxMomentum = Mathf.Max(minMomentum + 0.0001f, maxMomentum);
                float momentum01 = Mathf.InverseLerp(minMomentum, safeMaxMomentum, momentum);
                float impulseLerp01 = ResolveImpulseLerp01(momentum01, spinContext);
                float impulseSpeed = Mathf.Lerp(minSpinImpulseSpeed, maxSpinImpulseSpeed, impulseLerp01);

                var spinDriver = ResolveSpinDriver(spinContext);
                if (spinDriver != null)
                {
                    spinDriver.Configure(spinAngularDamping, spinMaxAngularSpeed, true, spinStopSpeedThreshold);
                    spinDriver.AddAngularVelocity(spinDirection, impulseSpeed);
                }
                else
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (!hasLoggedMissingSpinDriver)
                    {
                        hasLoggedMissingSpinDriver = true;
                        Debug.LogError("[NormalClickAnim] Missing BlockMomentumSpinDriver on clickable block. Spin animation is skipped.", t);
                    }
#endif
                }
            }
        }

        seq.SetId(TweenIdFor(target))
            .SetLink(target);

        return seq;
    }

    private bool TryGetPointerDrivenSpin(
        ISpinHitContext spinContext,
        out Vector3 spinAxis,
        out float momentum)
    {
        spinAxis = Vector3.zero;
        momentum = 1f;

        if (spinContext != null)
        {
            if (spinContext.TryGetPointerTorqueWorldAxis(out spinAxis, maxAgeFrames: 1))
            {
                if (spinContext.TryGetPointerScreenDirectionFromCenter(out _, out float pointerDistance, maxAgeFrames: 1))
                    momentum = ResolveMomentumFromDistance(pointerDistance);
                else
                    momentum = Mathf.Max(0f, minMomentum);

                return spinAxis.sqrMagnitude > 0.000001f;
            }
        }

        return false;
    }

    private float ResolveImpulseLerp01(float pointerMomentum01, ISpinHitContext spinContext)
    {
        float pointer01 = Mathf.Clamp01(pointerMomentum01);
        if (!scaleImpulseByDamageRatio || spinContext == null)
            return pointer01;

        if (!spinContext.TryGetRecentDamageRatioNormalized(out float damageRatio01, maxAgeFrames: 2))
            return pointer01;

        float scaledDamage = Mathf.Clamp01(Mathf.Max(0f, damageRatioScale) * damageRatio01);
        float curvedDamage = Mathf.Pow(scaledDamage, Mathf.Max(0.1f, damageRatioCurvePower));
        return Mathf.Lerp(pointer01, curvedDamage, Mathf.Clamp01(damageRatioBlend));
    }

    private static BlockMomentumSpinDriver ResolveSpinDriver(ISpinHitContext spinContext)
    {
        return spinContext != null ? spinContext.MomentumSpinDriver : null;
    }

    private float ResolveMomentumFromDistance(float distancePx)
    {
        float sensitivity = pointerSensitivity > 0f ? pointerSensitivity : 1.25f;
        float maxDistance = Mathf.Max(1f, momentumMaxDistancePx);
        float normalized = Mathf.Clamp01((distancePx * sensitivity) / maxDistance);
        float curved = Mathf.Pow(normalized, Mathf.Max(0.5f, momentumCurvePower));
        float safeMaxMomentum = Mathf.Max(minMomentum + 0.0001f, maxMomentum);
        return Mathf.Lerp(minMomentum, safeMaxMomentum, curved);
    }

}
