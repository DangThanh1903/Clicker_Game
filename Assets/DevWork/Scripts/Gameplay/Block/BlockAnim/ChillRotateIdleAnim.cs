using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName="ChillRotateIdleAnim", menuName="Block/Anim/Idle/ChillRotate")]
public class ChillRotateIdleAnim : BlockAnimationAsset
{
    [Header("Rotation Settings")]
    [Tooltip("How many degrees per loop around X axis.")]
    public float xDegrees = 20f;
    [Tooltip("How many degrees per loop around Y axis.")]
    public float yDegrees = 10f;
    [Tooltip("Loop duration in seconds.")]
    public float duration = 4f;
    [Tooltip("Should the animation ping-pong (back and forth) or continuous spin?")]
    public bool pingPong = true;

    public override bool IsLooping => true;
    public override float EstimatedDuration => duration;

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);
        var t = target.transform;

        Vector3 targetEuler = new Vector3(xDegrees, yDegrees, 0f);

        if (pingPong)
        {
            // Smooth back and forth rotation
            return t.DOLocalRotate(targetEuler, duration, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetId(TweenIdFor(target))
                    .SetLink(target);
        }
        else
        {
            // Continuous slow spin
            return t.DOLocalRotate(targetEuler, duration, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1, LoopType.Incremental)
                    .SetId(TweenIdFor(target))
                    .SetLink(target);
        }
    }
}
