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
    public List<BlockAnimationAsset> deathOptions = new();

    [Header("Selected Indices")]
    public int spawnIndex = 0, idleIndex = 0, clickIndex = 0, deathIndex = 0;

    private bool isDeathPlaying;
    private const string RESUME_IDLE_ID = "ANIM_RESUME_IDLE";

    void OnEnable()  => TryPlayIdle();
    void OnDisable() => StopAll();

    public void SetSpawnIndex(int idx)   { spawnIndex = Clamp(idx, spawnOptions.Count); }
    public void SetIdleIndex(int idx) { idleIndex = Clamp(idx, idleOptions.Count); TryPlayIdle(); }
    public void SetClickIndex(int idx)   { clickIndex = Clamp(idx, clickOptions.Count); }
    public void SetDeathIndex(int idx)   { deathIndex = Clamp(idx, deathOptions.Count); }

    public void TryPlayIdle()
    {
        if (isDeathPlaying) { Debug.Log("[Anim] Idle blocked: death flag"); return; }
        var s = Get(idleOptions, idleIndex);
        if (!s) { Debug.LogWarning("[Anim] No idle option selected"); return; }

        DOTween.Kill(RESUME_IDLE_ID);
        s.Stop(gameObject);
        s.PlayTween(gameObject);
        Debug.Log("[Anim] Play Idle");
    }

    public void PlaySpawn(Action onDone = null)
    {
        isDeathPlaying = false;

        var s = Get(spawnOptions, spawnIndex);
        if (!s) { onDone?.Invoke(); return; }

        s.Stop(gameObject);
        var tw = s.PlayTween(gameObject);
        if (tw != null) tw.OnComplete(() => onDone?.Invoke());
        else onDone?.Invoke();

        Debug.Log("Play Spawn");
    }

    public void PlayClick()
    {
        if (isDeathPlaying) return;
        var idle  = Get(idleOptions,  idleIndex);
        var click = Get(clickOptions, clickIndex);

        DOTween.Kill(RESUME_IDLE_ID);
        idle?.Stop(gameObject);

        if (!click) { TryPlayIdle(); return; }

        click.Stop(gameObject);
        var tw = click.PlayTween(gameObject);
        if (tw != null) tw.OnComplete(() => { if (!isDeathPlaying) TryPlayIdle(); });
        else if (!isDeathPlaying) TryPlayIdle();

        Debug.Log("Play Click");
    }

    public void PlayDeath(Action onDone = null)
    {
        isDeathPlaying = true;                 // block Idle/Click until next spawn
        DOTween.Kill(RESUME_IDLE_ID);
        StopAll();

        var death = Get(deathOptions, deathIndex);
        if (!death) { onDone?.Invoke(); return; }

        death.Stop(gameObject);
        var tw = death.PlayTween(gameObject);
        if (tw != null) tw.OnComplete(() => onDone?.Invoke());
        else onDone?.Invoke();

        Debug.Log("[Anim] Play Death");
    }


    public void StopAll()
    {
        Get(spawnOptions, spawnIndex)?.Stop(gameObject);
        Get(idleOptions, idleIndex)?.Stop(gameObject);
        Get(clickOptions, clickIndex)?.Stop(gameObject);
        Get(deathOptions, deathIndex)?.Stop(gameObject);
    }

    private static int Clamp(int i, int n) => (n <= 0) ? 0 : Mathf.Clamp(i, 0, n - 1);
    private static BlockAnimationAsset Get(List<BlockAnimationAsset> list, int idx)
        => (list != null && list.Count > 0 && idx >= 0 && idx < list.Count) ? list[idx] : null;
}
