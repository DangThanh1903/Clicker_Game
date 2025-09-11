using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName="NormalIdleAnim", menuName="Block/Anim/Idle/Normal")]
public class NormalIdleAnim : BlockAnimationAsset
{
    [Header("Rotate")]
    public bool enableRotate = true;
    public float degPerSec = 60f;
    [Tooltip("Local axis to spin around (normalized internally).")]
    public Vector3 localAxis = Vector3.up;

    [Header("Bob")]
    public bool enableBob = true;
    public float amplitude = 0.2f;
    public float period = 1.0f;

    public override bool IsLooping => true;
    public override float EstimatedDuration => 0f; // infinite

    private static readonly System.Collections.Generic.Dictionary<int, float> baseYMap
        = new System.Collections.Generic.Dictionary<int, float>();

    public override Tween PlayTween(GameObject target)
    {
        var id = TweenIdFor(target);
        DOTween.Kill(id);

        var t = target.transform;
        int instId = target.GetInstanceID();

        if (!baseYMap.ContainsKey(instId))
            baseYMap[instId] = t.localPosition.y;

        float baseY = baseYMap[instId];

        Tweener rot = null;
        Tweener bob = null;

        if (enableRotate && Mathf.Abs(degPerSec) > 0.0001f)
        {
            // Use signed angle for direction; abs value for duration
            float duration = 360f / Mathf.Abs(degPerSec);
            float signedAngle = Mathf.Sign(degPerSec) * 360f;

            // Normalize axis; default to up if zero
            Vector3 axis = localAxis.sqrMagnitude > 0.000001f ? localAxis.normalized : Vector3.up;

            // Rotate by signedAngle around chosen local axis, additively, forever
            // NOTE: LocalAxisAdd uses the *vector magnitude* per axis; build an euler from axis*signedAngle
            Vector3 eulerStep = new Vector3(axis.x * signedAngle, axis.y * signedAngle, axis.z * signedAngle);

            rot = t.DORotate(eulerStep, duration, RotateMode.LocalAxisAdd)
                  .SetEase(Ease.Linear)
                  .SetLoops(-1, LoopType.Restart)
                  .SetId(id)
                  .SetLink(target);
        }

        if (enableBob && amplitude > 0f && period > 0.0001f)
        {
            t.localPosition = new Vector3(t.localPosition.x, baseY, t.localPosition.z);

            bob = t.DOLocalMoveY(baseY + amplitude, period * 0.5f)
                  .SetEase(Ease.InOutSine)
                  .SetLoops(-1, LoopType.Yoyo)
                  .SetId(id)
                  .SetLink(target);
        }

        return rot ?? bob ?? DOVirtual.DelayedCall(0f, () => {}).SetId(id).SetLink(target);
    }

    public override void Stop(GameObject target)
    {
        DOTween.Kill(TweenIdFor(target));

        int instId = target.GetInstanceID();
        if (baseYMap.TryGetValue(instId, out float baseY))
        {
            var t = target.transform;
            t.localPosition = new Vector3(t.localPosition.x, baseY, t.localPosition.z);
        }
    }
}
