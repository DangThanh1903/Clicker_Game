using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName="NormalClickAnim", menuName="Block/Anim/Click/Normal")]
public class NormalClickAnim : BlockAnimationAsset
{
    [Header("Squash")]
    public float squashScale = 0.9f;
    public float duration = 0.15f;
    public Vector3 baseScale = new(2.5f, 2.5f, 2.5f);

    public override bool IsLooping => false;
    public override float EstimatedDuration => duration;

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);
        var t = target.transform;
        float half = duration * 0.5f;

        t.localScale = baseScale;
        // chain using a Sequence so we return ONE tween
        var seq = DOTween.Sequence()
            .Append(t.DOScale(baseScale * squashScale, half).SetEase(Ease.InQuad))
            .Append(t.DOScale(baseScale, half).SetEase(Ease.OutBack))
            .SetId(TweenIdFor(target))
            .SetLink(target);

        return seq;
    }
}
