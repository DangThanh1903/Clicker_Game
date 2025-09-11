using UnityEngine;
using DG.Tweening;
using Lean.Pool;

[CreateAssetMenu(fileName="NormalDeathAnim", menuName="Block/Anim/Death/Normal")]
public class NormalDeathAnim : BlockAnimationAsset
{
    [Header("Scale Timeline")]
    public float shrinkScale = 2f;
    public float shrinkTime = 0.2f;
    public float delayBeforeExpand = 0.1f;
    public float expandScale = 4f;
    public float expandTime = 0.2f;

    [Header("Fragments")]
    public GameObject fragmentPrefab;
    public int numberOfFragments = 5;

    public override bool IsLooping => false;
    public override float EstimatedDuration => shrinkTime + delayBeforeExpand + expandTime;

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);
        var t = target.transform;

        var seq = DOTween.Sequence()
            .Append(t.DOScale(shrinkScale, shrinkTime))
            .AppendInterval(delayBeforeExpand)
            .Append(t.DOScale(expandScale, expandTime))
            .OnComplete(() =>
            {
                if (fragmentPrefab)
                {
                    for (int i = 0; i < numberOfFragments; i++)
                        Lean.Pool.LeanPool.Spawn(fragmentPrefab, t.position, Quaternion.identity, t);
                }
            })
            .SetId(TweenIdFor(target))
            .SetLink(target);

        return seq;
    }
}
