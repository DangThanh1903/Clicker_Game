using UnityEngine;

public interface ICombatFeedbackSink
{
    void NotifyDamageHit();
    void NotifyIdleDamageDealt(float damage, Vector3 targetWorldPosition);
    int GetRecentHitCount(float windowSeconds = 1f);
}

public static class CombatFeedbackRuntime
{
    private static ICombatFeedbackSink sink;
    private static bool hasLoggedMissingBinding;

    public static void Bind(ICombatFeedbackSink feedbackSink)
    {
        if (feedbackSink == null)
        {
            Debug.LogError("[CombatFeedbackRuntime] Cannot bind null feedback sink.");
            return;
        }

        sink = feedbackSink;
        hasLoggedMissingBinding = false;
    }

    public static void Unbind(ICombatFeedbackSink feedbackSink)
    {
        if (!ReferenceEquals(sink, feedbackSink))
            return;

        sink = null;
    }

    public static bool TryGet(out ICombatFeedbackSink feedbackSink)
    {
        feedbackSink = sink;
        if (feedbackSink != null)
            return true;

        if (!hasLoggedMissingBinding)
        {
            hasLoggedMissingBinding = true;
            Debug.LogError("[CombatFeedbackRuntime] No feedback sink bound. Ensure PlayerController binds at runtime.");
        }

        return false;
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
