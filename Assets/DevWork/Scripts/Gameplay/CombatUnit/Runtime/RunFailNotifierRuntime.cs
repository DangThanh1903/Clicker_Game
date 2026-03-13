using UnityEngine;

public interface IRunFailNotifier
{
    void NotifyRunFailed(PlayerRunFailReason reason);
}

public static class RunFailNotifierRuntime
{
    private static IRunFailNotifier notifier;
    private static bool hasLoggedMissingBinding;

    public static void Bind(IRunFailNotifier runFailNotifier)
    {
        if (runFailNotifier == null)
        {
            Debug.LogError("[RunFailNotifierRuntime] Cannot bind null notifier.");
            return;
        }

        notifier = runFailNotifier;
        hasLoggedMissingBinding = false;
    }

    public static void Unbind(IRunFailNotifier runFailNotifier)
    {
        if (!ReferenceEquals(notifier, runFailNotifier))
            return;

        notifier = null;
    }

    public static bool TryGet(out IRunFailNotifier runFailNotifier)
    {
        runFailNotifier = notifier;
        if (runFailNotifier != null)
            return true;

        if (!hasLoggedMissingBinding)
        {
            hasLoggedMissingBinding = true;
            Debug.LogError("[RunFailNotifierRuntime] No notifier bound. Ensure PlayerController binds at runtime.");
        }

        return false;
    }

    public static void NotifyRunFailed(PlayerRunFailReason reason)
    {
        if (!TryGet(out IRunFailNotifier runFailNotifier))
            return;

        runFailNotifier.NotifyRunFailed(reason);
    }
}
