using System.Collections.Generic;
using Lean.Pool;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BlockFragmentCullingManager : MonoBehaviour
{
    private static BlockFragmentCullingManager instance;

    private readonly List<BlockFragment> trackedFragments = new List<BlockFragment>(128);
    private readonly HashSet<BlockFragment> trackedSet = new HashSet<BlockFragment>();
    private readonly List<BlockFragment> pendingAdd = new List<BlockFragment>(32);
    private readonly HashSet<BlockFragment> pendingAddSet = new HashSet<BlockFragment>();
    private readonly List<BlockFragment> despawnBuffer = new List<BlockFragment>(64);

    private bool isTicking;

    public static void Register(BlockFragment fragment)
    {
        if (fragment == null || !fragment.isActiveAndEnabled)
            return;

        EnsureInstance().RegisterInternal(fragment);
    }

    public static void Unregister(BlockFragment fragment)
    {
        if (fragment == null || instance == null)
            return;

        instance.UnregisterInternal(fragment);
    }

    private static BlockFragmentCullingManager EnsureInstance()
    {
        if (instance != null)
            return instance;

        var go = new GameObject(nameof(BlockFragmentCullingManager));
        instance = go.AddComponent<BlockFragmentCullingManager>();
        DontDestroyOnLoad(go);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        trackedFragments.Clear();
        trackedSet.Clear();
        pendingAdd.Clear();
        pendingAddSet.Clear();
        despawnBuffer.Clear();
        isTicking = false;
    }

    private void Update()
    {
        if (trackedFragments.Count == 0)
        {
            FlushPendingAdd();
            return;
        }

        float now = Time.unscaledTime;
        isTicking = true;

        for (int i = trackedFragments.Count - 1; i >= 0; i--)
        {
            BlockFragment fragment = trackedFragments[i];
            if (fragment == null || !fragment.isActiveAndEnabled)
            {
                trackedFragments.RemoveAt(i);
                trackedSet.Remove(fragment);
                continue;
            }

            if (fragment.ShouldDespawnOutOfCamera(now))
                despawnBuffer.Add(fragment);
        }

        isTicking = false;
        FlushPendingAdd();

        if (despawnBuffer.Count == 0)
            return;

        for (int i = 0; i < despawnBuffer.Count; i++)
        {
            BlockFragment fragment = despawnBuffer[i];
            if (fragment != null && fragment.isActiveAndEnabled)
                LeanPool.Despawn(fragment.gameObject);
        }

        despawnBuffer.Clear();
    }

    private void RegisterInternal(BlockFragment fragment)
    {
        if (trackedSet.Contains(fragment) || pendingAddSet.Contains(fragment))
            return;

        if (isTicking)
        {
            pendingAdd.Add(fragment);
            pendingAddSet.Add(fragment);
            return;
        }

        trackedFragments.Add(fragment);
        trackedSet.Add(fragment);
    }

    private void UnregisterInternal(BlockFragment fragment)
    {
        if (pendingAddSet.Remove(fragment))
            pendingAdd.Remove(fragment);

        if (!trackedSet.Remove(fragment))
            return;

        if (isTicking)
        {
            int idx = trackedFragments.IndexOf(fragment);
            if (idx >= 0)
                trackedFragments[idx] = null;
            return;
        }

        trackedFragments.Remove(fragment);
    }

    private void FlushPendingAdd()
    {
        if (pendingAdd.Count == 0)
            return;

        for (int i = 0; i < pendingAdd.Count; i++)
        {
            BlockFragment fragment = pendingAdd[i];
            if (fragment == null || !fragment.isActiveAndEnabled)
                continue;

            if (trackedSet.Contains(fragment))
                continue;

            trackedFragments.Add(fragment);
            trackedSet.Add(fragment);
        }

        pendingAdd.Clear();
        pendingAddSet.Clear();
    }
}
