using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName="ShakeSpawnAnim", menuName="Block/Anim/Spawn/ShakeIntoPlace")]
public class ShakeSpawnAnim : BlockAnimationAsset
{
    [Header("Pop-In (optional)")]
    public bool usePopIn = true;
    [Tooltip("Total duration of the full animation (pop + shake + settle).")]
    public float duration = 0.45f;
    [Tooltip("Portion of duration used by the pop-in phase (0..1).")]
    [Range(0f, 1f)] public float popPortion = 0.35f;
    [ReadOnly] public Vector3 endScale = new(2.5f, 2.5f, 2.5f);
    public Ease popEase = Ease.OutBack;

    [Header("Shake")]
    [Tooltip("Local start offset before shake (e.g., appear slightly above).")]
    public Vector3 startLocalOffset = new(0f, 0.15f, 0f);
    [Tooltip("Shake strength per axis in world units (use small values).")]
    public Vector3 shakeStrength = new(0.2f, 0.2f, 0.2f);
    [Tooltip("How rapidly it vibrates during the shake.")]
    public int vibrato = 18;
    [Tooltip("Higher = more random directions.")]
    [Range(0f, 180f)] public float randomness = 45f;
    [Tooltip("Fade the shake amplitude over its duration.")]
    public bool fadeOut = true;

    [Header("Settle")]
    [Tooltip("Extra settle time to ensure exact final position.")]
    public float settleTime = 0.08f;
    public Ease settleEase = Ease.OutSine;

    [Header("Reset")]
    [Tooltip("Reset local rotation to 0,0,0 before playing.")]
    public bool resetRotation = true;

    public override bool IsLooping => false;
    public override float EstimatedDuration => duration;

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);
        var t = target.transform;

        // Cache original local transforms
        var origLocalPos = t.localPosition;
        var origLocalRot = t.localRotation;
        Vector3 targetScale = t.localScale.sqrMagnitude > 0.0001f ? t.localScale : endScale;

        if (resetRotation) t.localRotation = Quaternion.identity;

        // Start at exact final scale or pop from zero, based on setting
        if (usePopIn)
            t.localScale = Vector3.zero;
        else
            t.localScale = targetScale;

        // Optional start offset so it "appears" then shakes into place
        t.localPosition = origLocalPos + startLocalOffset;

        float popDur   = usePopIn ? Mathf.Clamp(duration * popPortion, 0f, duration) : 0f;
        float shakeDur = Mathf.Max(0f, duration - popDur - settleTime);

        var seq = DOTween.Sequence().SetId(TweenIdFor(target)).SetLink(target);

        // Pop-in
        if (usePopIn && popDur > 0f)
        {
            seq.Append(t.DOScale(targetScale, popDur).SetEase(popEase));
        }
        else
        {
            // Ensure correct scale even if no pop
            t.localScale = targetScale;
        }

        // Shake (position) — DOTween shake uses world position internally, so we force a final settle after
        if (shakeDur > 0f)
        {
            seq.Append(t.DOShakePosition(
                shakeDur,
                shakeStrength,
                vibrato,
                randomness,
                snapping: false,
                fadeOut: fadeOut
            ));
        }

        // Precise settle back to exact local position (so floating error is gone)
        if (settleTime > 0f)
        {
            seq.Append(t.DOLocalMove(origLocalPos, settleTime).SetEase(settleEase));
        }
        else
        {
            // Snap just in case
            seq.AppendCallback(() => t.localPosition = origLocalPos);
        }

        return seq;
    }
}
