using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName="NormalSpawnAnim", menuName="Block/Anim/Spawn/Normal")]
public class NormalSpawnAnim : BlockAnimationAsset
{
    [Header("Scale Pop-in")]
    public float duration = 0.35f;
    [ReadOnly] public Vector3 endScale = new(2.5f, 2.5f, 2.5f);
    public Ease ease = Ease.OutBack;

    public override bool IsLooping => false;
    public override float EstimatedDuration => duration;

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);
        var t = target.transform;

        // Reset scale & rotation first
        t.localScale = Vector3.zero;
        t.localRotation = Quaternion.identity;

        // Animate scale in
        return t.DOScale(endScale, duration)
                .SetEase(ease)
                .SetId(TweenIdFor(target))
                .SetLink(target);
    }
}
