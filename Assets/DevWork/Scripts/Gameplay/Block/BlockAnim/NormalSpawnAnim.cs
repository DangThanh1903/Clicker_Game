using UnityEngine;
using DG.Tweening;

[CreateAssetMenu(fileName="NormalSpawnAnim", menuName="Block/Anim/Spawn/Normal")]
public class NormalSpawnAnim : BlockAnimationAsset
{
    [Header("Scale Pop-in")]
    public float duration = 0.35f;
    public Vector3 endScale = new(2.5f, 2.5f, 2.5f);
    public Ease ease = Ease.OutBack;

    public override bool IsLooping => false;
    public override float EstimatedDuration => duration;

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);
        var t = target.transform;
        t.localScale = Vector3.zero;
        return t.DOScale(endScale, duration)
                .SetEase(ease)
                .SetId(TweenIdFor(target))
                .SetLink(target);
    }
}
