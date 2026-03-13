using System.Collections.Generic;
using UnityEngine;

public interface ITargetRegistry
{
    IReadOnlyList<IDamageReceiver> ActiveTargets { get; }
    void CompactInvalidTargets();
}

public interface ITargetRegistryWriter : ITargetRegistry
{
    void Register(IDamageReceiver target);
    void Unregister(IDamageReceiver target);
    void Clear();
}

public sealed class RuntimeDamageTargetRegistry : ITargetRegistryWriter
{
    private readonly List<IDamageReceiver> activeTargets;

    public RuntimeDamageTargetRegistry(int initialCapacity = 32)
    {
        activeTargets = new List<IDamageReceiver>(Mathf.Max(1, initialCapacity));
    }

    public IReadOnlyList<IDamageReceiver> ActiveTargets => activeTargets;

    public void Register(IDamageReceiver target)
    {
        if (IsNullTarget(target))
            return;

        if (activeTargets.Contains(target))
            return;

        activeTargets.Add(target);
    }

    public void Unregister(IDamageReceiver target)
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

    public void CompactInvalidTargets()
    {
        if (activeTargets.Count == 0)
            return;

        for (int i = activeTargets.Count - 1; i >= 0; i--)
        {
            if (IsNullTarget(activeTargets[i]))
                activeTargets.RemoveAt(i);
        }
    }

    public void Clear()
    {
        activeTargets.Clear();
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
