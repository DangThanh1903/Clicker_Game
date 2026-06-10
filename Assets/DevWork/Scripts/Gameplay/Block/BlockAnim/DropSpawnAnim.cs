using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName="DropSpawnAnim", menuName="Block/Anim/Spawn/Drop")]
public class DropSpawnAnim : BlockAnimationAsset
{
    [Header("Drop Settings")]
    [Min(0f)] public float height = 2f;
    [Min(0.1f)] public float duration = 0.5f;
    [ReadOnly] public Vector3 endScale = new(2.5f, 2.5f, 2.5f);

    [Header("Squash")]
    [Range(0.4f, 1f)] public float squashScaleY = 0.75f;
    [Min(0.01f)] public float squashDuration = 0.1f;

    [Header("Fixed Ground Y")]
    public float groundY = 1f;

    [Header("Easing")]
    public Ease fallEase   = Ease.InQuad;
    public Ease settleEase = Ease.OutBack;

    public override bool IsLooping => false;
    public override float EstimatedDuration => duration;

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);

        var t = target.transform;
        Vector3 targetScale = t.localScale.sqrMagnitude > 0.0001f ? t.localScale : endScale;

        // Reset pose
        t.localRotation = Quaternion.identity;
        t.localScale    = targetScale;

        // End pos: keep X/Z from spawner, force Y = groundY
        Vector3 endPos  = new Vector3(t.position.x, groundY, t.position.z);
        Vector3 fromPos = endPos + Vector3.up * Mathf.Abs(height);

        float fallTime   = duration * 0.7f;
        float settleTime = duration * 0.3f;
        float impactTime = Mathf.Max(0f, fallTime - squashDuration);

        var seq = DOTween.Sequence()
                         .SetId(TweenIdFor(target))
                         .SetLink(target);

        // FALL
        seq.Append(t.DOMove(endPos, fallTime)
                    .From(fromPos)
                    .SetEase(fallEase));

        // SQUASH at impact
        seq.Insert(impactTime,
            t.DOScale(new Vector3(targetScale.x,
                                  targetScale.y * squashScaleY,
                                  targetScale.z),
                      squashDuration)
             .SetEase(Ease.OutQuad)
        );

        // SETTLE scale back
        seq.Append(t.DOScale(targetScale, settleTime).SetEase(settleEase));

        return seq;
    }
}
