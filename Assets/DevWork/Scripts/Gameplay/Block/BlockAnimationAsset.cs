using UnityEngine;
using DG.Tweening;

public abstract class BlockAnimationAsset : ScriptableObject
{
    public AnimChannel channel;
    public virtual bool IsLooping => channel == AnimChannel.Idle;
    public virtual float EstimatedDuration => 0f;

    protected string TweenIdFor(GameObject target) => $"{channel}_{target.GetInstanceID()}";

    // NEW: return the tween you play
    public abstract Tween PlayTween(GameObject target);

    public virtual void Stop(GameObject target)
    {
        DOTween.Kill(TweenIdFor(target));
    }

    // (optional) convenience wrapper
    public void Play(GameObject target) => PlayTween(target);
}
