using UnityEngine;

public enum AnimChannel { Spawn, Idle, Click, Death }


public interface IBlockAnimation
{
    AnimChannel Channel { get; }
    bool IsLooping { get; }
    float EstimatedDuration { get; }

    void Play(GameObject target);
    void Stop(GameObject target);
}

