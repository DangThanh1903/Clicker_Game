using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class BlockAnimationController : MonoBehaviour
{
    [Header("Options per Channel")]
    public List<BlockAnimationAsset> spawnOptions = new();
    public List<BlockAnimationAsset> idleOptions  = new();
    public List<BlockAnimationAsset> clickOptions = new();
    public List<BlockAnimationAsset> holdOptions  = new();
    public List<BlockAnimationAsset> deathOptions = new();

    [Header("Selected Indices")]
    public int spawnIndex = 0, idleIndex = 0, clickIndex = 0, holdIndex = 0, deathIndex = 0;

    private bool isDeathPlaying;
    private bool waitForMomentumBeforeIdle;
    private BlockMomentumSpinDriver waitingSpinDriver;
    private BlockMomentumSpinDriver cachedSpinDriver;

    private void Awake()
    {
        TryGetComponent(out cachedSpinDriver);
    }

    void OnDisable()
    {
        waitForMomentumBeforeIdle = false;
        waitingSpinDriver = null;
        StopAll();
    }

    public void SetSpawnIndex(int idx)   { spawnIndex = Clamp(idx, spawnOptions.Count); }
    public void SetIdleIndex(int idx) { idleIndex = Clamp(idx, idleOptions.Count); TryPlayIdle(); }
    public void SetClickIndex(int idx)   { clickIndex = Clamp(idx, clickOptions.Count); }
    public void SetHoldIndex(int idx) { holdIndex = Clamp(idx, holdOptions.Count); }
    public void SetDeathIndex(int idx)   { deathIndex = Clamp(idx, deathOptions.Count); }

    public void TryPlayIdle()
    {
        if (isDeathPlaying) return;
        var s = Get(idleOptions, idleIndex);
        if (!s) { Debug.LogWarning("[Anim] No idle option selected"); return; }

        s.Stop(gameObject);
        s.PlayTween(gameObject);
    }

    public void PlaySpawn(Action onDone = null, bool playAnimation = false)
    {
        isDeathPlaying = false;
        waitForMomentumBeforeIdle = false;
        waitingSpinDriver = null;
        if (!playAnimation)
        {
            onDone?.Invoke();
            return;
        }

        var spawn = Get(spawnOptions, spawnIndex);
        if (!spawn)
        {
            onDone?.Invoke();
            return;
        }

        spawn.Stop(gameObject);
        var tw = spawn.PlayTween(gameObject);
        if (tw != null) tw.OnComplete(() => onDone?.Invoke());
        else onDone?.Invoke();
    }

    public void PlayClick()
    {
        if (isDeathPlaying) return;
        var idle  = Get(idleOptions,  idleIndex);
        var click = Get(clickOptions, clickIndex);

        waitForMomentumBeforeIdle = false;
        waitingSpinDriver = null;
        idle?.Stop(gameObject);

        if (!click)
        {
            ResumeIdleWhenReady();
            return;
        }

        click.Stop(gameObject);
        var tw = click.PlayTween(gameObject);
        if (tw != null) tw.OnComplete(ResumeIdleWhenReady);
        else ResumeIdleWhenReady();
    }

    public void PlayHold()
    {
        if (isDeathPlaying) return;
        var idle = Get(idleOptions, idleIndex);
        var hold = GetHoldSelected();

        waitForMomentumBeforeIdle = false;
        waitingSpinDriver = null;
        idle?.Stop(gameObject);

        if (!hold)
        {
            ResumeIdleWhenReady();
            return;
        }

        hold.Stop(gameObject);
        var tw = hold.PlayTween(gameObject);
        if (tw != null) tw.OnComplete(ResumeIdleWhenReady);
        else ResumeIdleWhenReady();
    }

    public void PlayDeath(Action onDone = null)
    {
        isDeathPlaying = true;                 // block Idle/Click until next spawn
        waitForMomentumBeforeIdle = false;
        waitingSpinDriver = null;
        StopAll();

        var death = Get(deathOptions, deathIndex);
        if (!death) { onDone?.Invoke(); return; }

        death.Stop(gameObject);
        var tw = death.PlayTween(gameObject);
        if (tw != null) tw.OnComplete(() => onDone?.Invoke());
        else onDone?.Invoke();
    }


    public void StopAll()
    {
        Get(spawnOptions, spawnIndex)?.Stop(gameObject);
        Get(idleOptions, idleIndex)?.Stop(gameObject);
        Get(clickOptions, clickIndex)?.Stop(gameObject);
        GetHoldSelected()?.Stop(gameObject);
        Get(deathOptions, deathIndex)?.Stop(gameObject);
    }

    private void LateUpdate()
    {
        if (!waitForMomentumBeforeIdle)
            return;

        if (isDeathPlaying)
        {
            waitForMomentumBeforeIdle = false;
            waitingSpinDriver = null;
            return;
        }

        if (waitingSpinDriver != null && waitingSpinDriver.HasMomentum)
            return;

        waitForMomentumBeforeIdle = false;
        waitingSpinDriver = null;
        TryPlayIdle();
    }

    private void ResumeIdleWhenReady()
    {
        if (isDeathPlaying)
            return;

        var spinDriver = cachedSpinDriver;
        if (spinDriver != null && spinDriver.HasMomentum)
        {
            waitingSpinDriver = spinDriver;
            waitForMomentumBeforeIdle = true;
            return;
        }

        TryPlayIdle();
    }

    private static int Clamp(int i, int n) => (n <= 0) ? 0 : Mathf.Clamp(i, 0, n - 1);
    private BlockAnimationAsset GetHoldSelected()
    {
        var hold = Get(holdOptions, holdIndex);
        return hold ? hold : Get(clickOptions, clickIndex);
    }

    private static BlockAnimationAsset Get(List<BlockAnimationAsset> list, int idx)
        => (list != null && list.Count > 0 && idx >= 0 && idx < list.Count) ? list[idx] : null;
}
