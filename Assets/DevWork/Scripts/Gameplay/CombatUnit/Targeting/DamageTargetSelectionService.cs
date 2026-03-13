using System.Collections.Generic;
using UnityEngine;

public interface IDamageTargetSelectionService
{
    IDamageReceiver SelectBestTarget(ITargetRegistry registry);
    bool CanReceiveDamage(IDamageReceiver target);
}

public sealed class PriorityDamageTargetSelectionService : IDamageTargetSelectionService
{
    public static readonly PriorityDamageTargetSelectionService Instance = new PriorityDamageTargetSelectionService();

    private PriorityDamageTargetSelectionService()
    {
    }

    public IDamageReceiver SelectBestTarget(ITargetRegistry registry)
    {
        if (registry == null)
            return null;

        registry.CompactInvalidTargets();
        IReadOnlyList<IDamageReceiver> targets = registry.ActiveTargets;

        IDamageReceiver bestTarget = null;
        int bestPriority = int.MinValue;

        for (int i = 0; i < targets.Count; i++)
        {
            IDamageReceiver target = targets[i];
            if (!CanReceiveDamage(target))
                continue;

            int priority = target.InputPriority;
            if (priority >= bestPriority)
            {
                bestPriority = priority;
                bestTarget = target;
            }
        }

        return bestTarget;
    }

    public bool CanReceiveDamage(IDamageReceiver target)
    {
        if (IsNullTarget(target))
            return false;

        return target.CanReceiveDamage;
    }

    private static bool IsNullTarget(IDamageReceiver target)
    {
        if (ReferenceEquals(target, null))
            return true;

        if (target is Object unityObj)
            return unityObj == null;

        return false;
    }
}
