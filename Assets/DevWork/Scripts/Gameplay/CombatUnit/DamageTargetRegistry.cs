using System.Collections.Generic;
using UnityEngine;

public static class DamageTargetRegistry
{
    private static readonly List<IDamagable> activeTargets = new List<IDamagable>(32);
    public static IReadOnlyList<IDamagable> ActiveTargets => activeTargets;

    public static void Register(IDamagable target)
    {
        if (IsNullTarget(target))
            return;

        if (activeTargets.Contains(target))
            return;

        activeTargets.Add(target);
    }

    public static void Unregister(IDamagable target)
    {
        if (activeTargets.Count == 0)
            return;

        for (int i = activeTargets.Count - 1; i >= 0; i--)
        {
            IDamagable current = activeTargets[i];
            if (IsNullTarget(current) || IsSameTarget(current, target))
                activeTargets.RemoveAt(i);
        }
    }

    public static void CompactInvalidTargets()
    {
        if (activeTargets.Count == 0)
            return;

        for (int i = activeTargets.Count - 1; i >= 0; i--)
        {
            if (IsNullTarget(activeTargets[i]))
                activeTargets.RemoveAt(i);
        }
    }

    private static bool IsNullTarget(IDamagable target)
    {
        if (ReferenceEquals(target, null))
            return true;

        if (target is Object unityObj)
            return unityObj == null;

        return false;
    }

    private static bool IsSameTarget(IDamagable a, IDamagable b)
    {
        if (ReferenceEquals(a, b))
            return true;

        if (a is Object aObj && b is Object bObj)
            return aObj == bObj;

        return false;
    }
}
