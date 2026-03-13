using System;
using UnityEngine;

public static class CombatRuntimeBootstrap
{
    private sealed class RuntimeContext
    {
        public object Owner;
        public ICombatResourceReadModel ReadModel;
        public ICombatFeedbackSink FeedbackSink;
        public IRunFailNotifier RunFailNotifier;
        public ITargetRegistryWriter TargetRegistry;
    }

    private static RuntimeContext context;

    private static bool hasLoggedMissingReadModel;
    private static bool hasLoggedMissingFeedbackSink;
    private static bool hasLoggedMissingRunFailNotifier;
    private static bool hasLoggedMissingTargetRegistry;

    public static event Action<ITargetRegistryWriter> TargetRegistryBound;

    public static void BindAll(
        object owner,
        ICombatResourceReadModel readModel,
        ICombatFeedbackSink feedbackSink,
        IRunFailNotifier runFailNotifier,
        ITargetRegistryWriter targetRegistry)
    {
        if (owner == null)
        {
            Debug.LogError("[CombatRuntimeBootstrap] Cannot bind with null owner.");
            return;
        }

        if (readModel == null || feedbackSink == null || runFailNotifier == null || targetRegistry == null)
        {
            Debug.LogError("[CombatRuntimeBootstrap] BindAll requires non-null dependencies.");
            return;
        }

        context = new RuntimeContext
        {
            Owner = owner,
            ReadModel = readModel,
            FeedbackSink = feedbackSink,
            RunFailNotifier = runFailNotifier,
            TargetRegistry = targetRegistry
        };

        hasLoggedMissingReadModel = false;
        hasLoggedMissingFeedbackSink = false;
        hasLoggedMissingRunFailNotifier = false;
        hasLoggedMissingTargetRegistry = false;

        TargetRegistryBound?.Invoke(targetRegistry);
    }

    public static void UnbindOwner(object owner)
    {
        if (context == null)
            return;

        if (!ReferenceEquals(context.Owner, owner))
            return;

        context = null;
    }

    public static bool TryGetReadModel(out ICombatResourceReadModel readModel, bool logIfMissing = true)
    {
        readModel = context != null ? context.ReadModel : null;
        if (readModel != null)
            return true;

        if (logIfMissing && !hasLoggedMissingReadModel)
        {
            hasLoggedMissingReadModel = true;
            Debug.LogError("[CombatRuntimeBootstrap] No combat read model bound.");
        }

        return false;
    }

    public static bool TryGetFeedbackSink(out ICombatFeedbackSink feedbackSink, bool logIfMissing = true)
    {
        feedbackSink = context != null ? context.FeedbackSink : null;
        if (feedbackSink != null)
            return true;

        if (logIfMissing && !hasLoggedMissingFeedbackSink)
        {
            hasLoggedMissingFeedbackSink = true;
            Debug.LogError("[CombatRuntimeBootstrap] No combat feedback sink bound.");
        }

        return false;
    }

    public static bool TryGetRunFailNotifier(out IRunFailNotifier runFailNotifier, bool logIfMissing = true)
    {
        runFailNotifier = context != null ? context.RunFailNotifier : null;
        if (runFailNotifier != null)
            return true;

        if (logIfMissing && !hasLoggedMissingRunFailNotifier)
        {
            hasLoggedMissingRunFailNotifier = true;
            Debug.LogError("[CombatRuntimeBootstrap] No run fail notifier bound.");
        }

        return false;
    }

    public static bool TryGetTargetRegistry(out ITargetRegistry targetRegistry, bool logIfMissing = true)
    {
        targetRegistry = context != null ? context.TargetRegistry : null;
        if (targetRegistry != null)
            return true;

        if (logIfMissing && !hasLoggedMissingTargetRegistry)
        {
            hasLoggedMissingTargetRegistry = true;
            Debug.LogError("[CombatRuntimeBootstrap] No target registry bound.");
        }

        return false;
    }

    public static bool TryGetTargetRegistryWriter(out ITargetRegistryWriter targetRegistry, bool logIfMissing = true)
    {
        targetRegistry = context != null ? context.TargetRegistry : null;
        if (targetRegistry != null)
            return true;

        if (logIfMissing && !hasLoggedMissingTargetRegistry)
        {
            hasLoggedMissingTargetRegistry = true;
            Debug.LogError("[CombatRuntimeBootstrap] No target registry writer bound.");
        }

        return false;
    }
}
