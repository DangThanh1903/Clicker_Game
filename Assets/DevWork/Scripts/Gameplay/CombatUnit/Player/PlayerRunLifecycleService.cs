using System;
using UnityEngine;

public enum PlayerRunFailReason
{
    Unknown = 0,
    BossTimeout = 1,
    DungeonTimeout = 2,
    DungeonStageFailed = 3,
    ManualAbort = 4
}

public sealed class PlayerRunLifecycleService
{
    public event Action<PlayerRunFailReason> RunFailed;

    private int lastFailFrame = -1;

    public void NotifyRunFailed(PlayerRunFailReason reason)
    {
        if (lastFailFrame == Time.frameCount)
            return;

        lastFailFrame = Time.frameCount;
        RunFailed?.Invoke(reason);
    }

    public void ResetRuntime()
    {
        lastFailFrame = -1;
    }
}
