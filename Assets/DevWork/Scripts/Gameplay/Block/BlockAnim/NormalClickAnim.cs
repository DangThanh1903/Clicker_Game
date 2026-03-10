using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName="NormalClickAnim", menuName="Block/Anim/Click/Normal")]
public class NormalClickAnim : BlockAnimationAsset
{
    [Header("Squash")]
    public float squashScale = 0.9f;
    public float duration = 0.15f;
    [Tooltip("If true, force using fixed baseScale. Keep false to respect current block scale (recommended).")]
    public bool useFixedBaseScale = false;
    public Vector3 baseScale = new(2.5f, 2.5f, 2.5f);

    [Header("Random Rotation")]
    public bool enableRandomRotation = true;
    public Vector3 randomRotationDegrees = new(8f, 12f, 12f);
    public Ease rotateOutEase = Ease.OutQuad;
    public Ease rotateBackEase = Ease.OutBack;

    [Header("Mouse Direction Momentum")]
    public bool rotateByMouseDirection = true;
    public Vector3 directionalRotationDegrees = new(10f, 6f, 14f);
    [Min(1f)] public float momentumMaxDistancePx = 220f;
    [Range(0f, 1f)] public float minMomentum = 0.25f;
    [Range(0.05f, 3f)] public float maxMomentum = 1f;
    [Min(0.5f)] public float momentumCurvePower = 1.4f;
    [Min(0f)] public float centerDeadZonePx = 6f;
    public bool addRandomJitter = true;
    public Vector3 randomJitterDegrees = new(1.5f, 2f, 2f);

    [Header("Momentum Spin")]
    [Min(0.05f)] public float spinDuration = 0.28f;
    [Min(0f)] public float minSpinDegrees = 28f;
    [Min(0f)] public float maxSpinDegrees = 200f;
    [Range(0.1f, 0.9f)] public float spinStep1Portion = 0.58f;
    [Range(0.05f, 0.5f)] public float spinStep2Portion = 0.28f;
    [Range(0.01f, 0.3f)] public float spinStep3Portion = 0.14f;
    public Ease spinEase1 = Ease.OutCubic;
    public Ease spinEase2 = Ease.OutQuad;
    public Ease spinEase3 = Ease.OutSine;
    public bool invertSpinDirection = true;

    public override bool IsLooping => false;
    public override float EstimatedDuration => duration;

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);
        var t = target.transform;
        Vector3 baseline = useFixedBaseScale ? baseScale : t.localScale;
        float safeScaleDuration = Mathf.Max(0.01f, duration);
        float half = safeScaleDuration * 0.5f;

        t.localScale = baseline;
        var seq = DOTween.Sequence();

        Tween scaleDown = t.DOScale(baseline * squashScale, half).SetEase(Ease.InQuad);
        Tween scaleUp = t.DOScale(baseline, half).SetEase(Ease.OutBack);
        var scaleSeq = DOTween.Sequence()
            .Append(scaleDown)
            .Append(scaleUp);
        seq.Append(scaleSeq);
        bool shouldRotate = rotateByMouseDirection || enableRandomRotation;
        if (shouldRotate)
        {
            Vector3 spinDirection = Vector3.zero;
            float momentum = 1f;
            bool hasMouseDirection = false;

            if (rotateByMouseDirection && TryGetMouseDirectionMomentum(t, out Vector2 mouseDir, out momentum))
            {
                hasMouseDirection = true;
                spinDirection += new Vector3(
                    -mouseDir.y * Mathf.Max(0.0001f, directionalRotationDegrees.x),
                    mouseDir.x * Mathf.Max(0.0001f, directionalRotationDegrees.y),
                    -mouseDir.x * Mathf.Max(0.0001f, directionalRotationDegrees.z));
            }

            if (!hasMouseDirection && enableRandomRotation)
            {
                spinDirection += new Vector3(
                    Random.Range(-randomRotationDegrees.x, randomRotationDegrees.x),
                    Random.Range(-randomRotationDegrees.y, randomRotationDegrees.y),
                    Random.Range(-randomRotationDegrees.z, randomRotationDegrees.z));
            }
            else if (addRandomJitter)
            {
                spinDirection += new Vector3(
                    Random.Range(-randomJitterDegrees.x, randomJitterDegrees.x),
                    Random.Range(-randomJitterDegrees.y, randomJitterDegrees.y),
                    Random.Range(-randomJitterDegrees.z, randomJitterDegrees.z)) * momentum;
            }

            if (spinDirection.sqrMagnitude > 0.000001f)
            {
                if (invertSpinDirection)
                    spinDirection = -spinDirection;

                spinDirection.Normalize();

                float safeMaxMomentum = Mathf.Max(minMomentum + 0.0001f, maxMomentum);
                float momentum01 = Mathf.InverseLerp(minMomentum, safeMaxMomentum, momentum);
                float spinTotal = Mathf.Lerp(minSpinDegrees, maxSpinDegrees, momentum01);
                float totalPortion = Mathf.Max(0.001f, spinStep1Portion + spinStep2Portion + spinStep3Portion);
                float p1 = spinStep1Portion / totalPortion;
                float p2 = spinStep2Portion / totalPortion;
                float p3 = spinStep3Portion / totalPortion;
                float rotTotalDuration = Mathf.Max(0.05f, spinDuration);

                var rotateSeq = DOTween.Sequence()
                    .Append(t.DOLocalRotate(spinDirection * (spinTotal * p1), rotTotalDuration * 0.45f, RotateMode.LocalAxisAdd).SetEase(spinEase1))
                    .Append(t.DOLocalRotate(spinDirection * (spinTotal * p2), rotTotalDuration * 0.33f, RotateMode.LocalAxisAdd).SetEase(spinEase2))
                    .Append(t.DOLocalRotate(spinDirection * (spinTotal * p3), rotTotalDuration * 0.22f, RotateMode.LocalAxisAdd).SetEase(spinEase3));

                seq.Join(rotateSeq);
            }
        }

        seq.SetId(TweenIdFor(target))
            .SetLink(target);

        return seq;
    }

    private bool TryGetMouseDirectionMomentum(Transform targetTransform, out Vector2 direction, out float momentum)
    {
        direction = Vector2.zero;
        momentum = 1f;

        Camera cam = Camera.main;
        if (cam == null)
            return false;

        Vector3 centerScreen = cam.WorldToScreenPoint(targetTransform.position);
        if (centerScreen.z <= 0f)
            return false;

        Vector2 center = new Vector2(centerScreen.x, centerScreen.y);
        Vector2 mouse = Input.mousePosition;
        Vector2 delta = mouse - center;
        float distance = delta.magnitude;
        if (distance <= centerDeadZonePx)
            return false;

        direction = delta / Mathf.Max(0.0001f, distance);

        float maxDistance = Mathf.Max(1f, momentumMaxDistancePx);
        float normalized = Mathf.Clamp01(distance / maxDistance);
        float curved = Mathf.Pow(normalized, Mathf.Max(0.5f, momentumCurvePower));
        momentum = Mathf.Lerp(minMomentum, maxMomentum, curved);
        return true;
    }
}
