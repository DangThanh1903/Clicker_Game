using UnityEngine;

public interface ICombatFeedbackSink
{
    void NotifyDamageHit();
    void NotifyIdleDamageDealt(float damage, Vector3 targetWorldPosition);
    int GetRecentHitCount(float windowSeconds = 1f);
}

public static class CombatFeedbackRuntime
{
    public static bool TryGet(out ICombatFeedbackSink feedbackSink)
    {
        return CombatRuntimeBootstrap.TryGetFeedbackSink(out feedbackSink, logIfMissing: true);
    }

    public static void NotifyDamageHit()
    {
        if (!TryGet(out ICombatFeedbackSink feedbackSink))
            return;

        feedbackSink.NotifyDamageHit();
    }

    public static void NotifyIdleDamageDealt(float damage, Vector3 targetWorldPosition)
    {
        if (!TryGet(out ICombatFeedbackSink feedbackSink))
            return;

        feedbackSink.NotifyIdleDamageDealt(damage, targetWorldPosition);
    }

    public static int GetRecentHitCount(float windowSeconds = 1f)
    {
        if (!TryGet(out ICombatFeedbackSink feedbackSink))
            return 0;

        return feedbackSink.GetRecentHitCount(windowSeconds);
    }
}
