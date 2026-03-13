using System.Collections.Generic;
using UnityEngine;

public interface ITargetRegistry
{
    IReadOnlyList<IDamageReceiver> ActiveTargets { get; }
    void CompactInvalidTargets();
}

public sealed class DamageTargetRegistryRuntimeAdapter : ITargetRegistry
{
    public static readonly DamageTargetRegistryRuntimeAdapter Instance = new DamageTargetRegistryRuntimeAdapter();

    private DamageTargetRegistryRuntimeAdapter()
    {
    }

    public IReadOnlyList<IDamageReceiver> ActiveTargets => DamageTargetRegistry.ActiveTargets;

    public void CompactInvalidTargets()
    {
        DamageTargetRegistry.CompactInvalidTargets();
    }
}

public static class DamageTargetRegistry
{
    private static readonly List<IDamageReceiver> activeTargets = new List<IDamageReceiver>(32);
    public static IReadOnlyList<IDamageReceiver> ActiveTargets => activeTargets;

    public static void Register(IDamageReceiver target)
    {
        if (IsNullTarget(target))
            return;

        if (activeTargets.Contains(target))
            return;

        activeTargets.Add(target);
    }

    public static void Unregister(IDamageReceiver target)
    {
        if (activeTargets.Count == 0)
            return;

        for (int i = activeTargets.Count - 1; i >= 0; i--)
        {
            IDamageReceiver current = activeTargets[i];
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

    private static bool IsNullTarget(IDamageReceiver target)
    {
        if (ReferenceEquals(target, null))
            return true;

        if (target is Object unityObj)
            return unityObj == null;

        return false;
    }

    private static bool IsSameTarget(IDamageReceiver a, IDamageReceiver b)
    {
        if (ReferenceEquals(a, b))
            return true;

        if (a is Object aObj && b is Object bObj)
            return aObj == bObj;

        return false;
    }
}
