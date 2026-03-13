public interface IRunFailNotifier
{
    void NotifyRunFailed(PlayerRunFailReason reason);
}

public static class RunFailNotifierRuntime
{
    public static bool TryGet(out IRunFailNotifier runFailNotifier)
    {
        return CombatRuntimeBootstrap.TryGetRunFailNotifier(out runFailNotifier, logIfMissing: true);
    }

    public static void NotifyRunFailed(PlayerRunFailReason reason)
    {
        if (!TryGet(out IRunFailNotifier runFailNotifier))
            return;

        runFailNotifier.NotifyRunFailed(reason);
    }
}
