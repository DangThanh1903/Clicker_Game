using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UniRx;
using Lean.Pool;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Ins { get; private set; }

    [Header("VFX Triggers")]
    [SerializeField] private List<VFXTrigger> triggers;
    [Header("Block Click VFX")]
    [SerializeField] private GameObject blockClickVfxPrefab;
    [SerializeField, Min(1)] private int maxBlockClicksPerRateWindow = 6;
    [SerializeField, Min(0.05f)] private float blockClickRateWindowSeconds = 0.4f;
    [SerializeField, Min(0f)] private float blockClickVfxDespawnDelay = 0.6f;

    [Header("Biome Music")]
    [SerializeField] private AudioSource musicSource;          // Loop ON, PlayOnAwake OFF
    [SerializeField] private List<BiomeMusicEntry> biomeMusic; // Map BlockSpawnLocation -> clip
    [SerializeField] private float defaultFadeTime = 1.5f;
    [SerializeField] private bool preloadBiomeClips = true;
    [SerializeField] private float preloadTimeoutSeconds = 5f;

    private Coroutine _fadeRoutine;
    private Coroutine _preloadRoutine;
    private bool blockClickVfxPrewarmed;
    private bool blockClickVfxRotationCached;
    private Quaternion blockClickVfxCachedRotation = Quaternion.identity;
    private readonly List<GameObject> pooledBlockClickVfxBuffer = new List<GameObject>(16);

    // đŸ§ user volume (0â€“1)
    private float _musicVolume = 1f;
    private const string MusicVolumeKey = "MusicVolume";

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }

        Ins = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SetupVFXTriggers();
        SetupBiomeMusic();
        PrewarmBlockClickVfxIfNeeded();
    }

    #region VFX

    private void SetupVFXTriggers()
    {
        foreach (var trigger in triggers)
        {
            var reactiveStat = StatsManager.Ins.GetReactive(trigger.watchStat);

            switch (trigger.triggerType)
            {
                case VFXTriggerType.Achivement:
                    reactiveStat
                        .Where(val => val >= trigger.triggerThreshold)
                        .Where(_ => !trigger.triggered)
                        .Subscribe(_ => PlayAchivementVFX(trigger))
                        .AddTo(this);
                    break;

                case VFXTriggerType.InGame:
                    reactiveStat
                        .Subscribe(val => HandleInGameVFX(trigger, val))
                        .AddTo(this);
                    break;
            }
        }
    }

    private void PlayAchivementVFX(VFXTrigger trigger)
    {
        DevLog.Log($"Achivement VFX played: {trigger.name}");
        if (trigger.vfxPrefab)
        {
            LeanPool.Spawn(trigger.vfxPrefab, transform.position, Quaternion.identity);
        }

        trigger.triggered = true;
    }

    private void HandleInGameVFX(VFXTrigger trigger, float value)
    {
        bool shouldPlay = value >= trigger.triggerThreshold;

        if (shouldPlay && trigger.spawnedVFX == null)
        {
            trigger.spawnedVFX = LeanPool.Spawn(
                trigger.vfxPrefab,
                transform.position,
                Quaternion.identity,
                transform
            );
            DevLog.Log($"Started in-game VFX: {trigger.name}");
        }
        else if (!shouldPlay && trigger.spawnedVFX != null)
        {
            LeanPool.Despawn(trigger.spawnedVFX);
            trigger.spawnedVFX = null;
            DevLog.Log($"Stopped in-game VFX: {trigger.name}");
        }
    }

    #endregion

    #region Block Click VFX

    public void EnsureBlockClickVfxPrewarmed()
    {
        PrewarmBlockClickVfxIfNeeded();
    }

    public void PlayBlockClickVfx(Vector3 worldPosition)
    {
        if (blockClickVfxPrefab == null)
            return;

        PrewarmBlockClickVfxIfNeeded();

        Quaternion rotation = blockClickVfxRotationCached
            ? blockClickVfxCachedRotation
            : Quaternion.identity;

        var fx = LeanPool.Spawn(blockClickVfxPrefab, worldPosition, rotation);
        if (fx != null && blockClickVfxDespawnDelay > 0f)
            LeanPool.Despawn(fx, blockClickVfxDespawnDelay);
    }

    private void PrewarmBlockClickVfxIfNeeded()
    {
        if (blockClickVfxPrewarmed || blockClickVfxPrefab == null)
            return;

        CacheBlockClickVfxRotation();

        LeanGameObjectPool pool = null;
        if (!LeanGameObjectPool.TryFindPoolByPrefab(blockClickVfxPrefab, ref pool))
        {
            var bootstrap = LeanPool.Spawn(blockClickVfxPrefab);
            if (bootstrap != null)
                LeanPool.Despawn(bootstrap);

            LeanGameObjectPool.TryFindPoolByPrefab(blockClickVfxPrefab, ref pool);
            if (pool == null)
                return;
        }

        int prewarmCount = ResolveBlockClickVfxPrewarmCount();
        if (pool.Preload < prewarmCount)
            pool.Preload = prewarmCount;

        pool.PreloadAll();
        ApplyCachedBlockClickVfxRotationToPreloaded(pool);
        blockClickVfxPrewarmed = true;
    }

    private int ResolveBlockClickVfxPrewarmCount()
    {
        float window = Mathf.Max(0.05f, blockClickRateWindowSeconds);
        float scaledClicks = maxBlockClicksPerRateWindow * (0.4f / window);
        return Mathf.Max(1, Mathf.CeilToInt(scaledClicks));
    }

    private void CacheBlockClickVfxRotation()
    {
        if (blockClickVfxRotationCached)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            cam = FindObjectOfType<Camera>();
        if (cam == null)
            return;

        // Cache one facing rotation once during preload.
        blockClickVfxCachedRotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
        blockClickVfxRotationCached = true;
    }

    private void ApplyCachedBlockClickVfxRotationToPreloaded(LeanGameObjectPool pool)
    {
        if (pool == null || !blockClickVfxRotationCached)
            return;

        pooledBlockClickVfxBuffer.Clear();
        pool.GetClones(pooledBlockClickVfxBuffer, addSpawnedClones: false, addDespawnedClones: true);
        for (int i = 0; i < pooledBlockClickVfxBuffer.Count; i++)
        {
            var clone = pooledBlockClickVfxBuffer[i];
            if (clone == null)
                continue;
            clone.transform.rotation = blockClickVfxCachedRotation;
        }
    }

    #endregion

    #region Biome Music

    private void SetupBiomeMusic()
    {
        // Load saved volume first (0â€“1, default 1)
        _musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        if (musicSource != null)
            musicSource.volume = _musicVolume;

        if (LocationLoader.Ins == null || LocationLoader.Ins.ReactiveLocation == null)
        {
            Debug.LogWarning("[VFXManager] No LocationLoader.Ins or ReactiveLocation, biome music will not play.");
            return;
        }

        // React to biome changes from LocationLoader
        LocationLoader.Ins.ReactiveLocation
            .DistinctUntilChanged()
            .Subscribe(OnBiomeChanged)
            .AddTo(this);

        // Apply initial biome
        OnBiomeChanged(LocationLoader.Ins.ReactiveLocation.Value);

        if (preloadBiomeClips)
        {
            if (_preloadRoutine != null)
                StopCoroutine(_preloadRoutine);
            _preloadRoutine = StartCoroutine(PreloadBiomeMusicClips());
        }
    }

    private void OnBiomeChanged(BlockSpawnLocation biome)
    {
        var entry = biomeMusic.Find(b => b.biome == biome);

        if (entry == null || entry.clip == null)
        {
            Debug.LogWarning($"[VFXManager] No music clip set for biome: {biome}");
            return;
        }

        float fade = entry.fadeTime > 0f ? entry.fadeTime : defaultFadeTime;
        PlayBiomeMusic(entry.clip, fade);
        DevLog.Log($"[VFXManager] Switched biome music to {biome}");
    }

    private void PlayBiomeMusic(AudioClip newClip, float fadeTime)
    {
        // Already playing this clip
        if (musicSource != null && musicSource.clip == newClip && musicSource.isPlaying)
            return;

        if (newClip != null && newClip.loadState != AudioDataLoadState.Loaded)
        {
            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(WaitThenFadeToClip(newClip, fadeTime));
            return;
        }

        if (_fadeRoutine != null)
            StopCoroutine(_fadeRoutine);

        _fadeRoutine = StartCoroutine(FadeToClipRoutine(newClip, fadeTime));
    }

    private IEnumerator FadeToClipRoutine(AudioClip newClip, float fadeTime)
    {
        if (musicSource == null)
            yield break;

        float targetVolume = _musicVolume;   // user volume 0â€“1
        float startVolume = musicSource.volume;

        // Fade out
        if (musicSource.clip != null && fadeTime > 0f)
        {
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, t / fadeTime);
                yield return null;
            }
        }

        // Swap clip
        musicSource.clip = newClip;
        if (newClip != null)
        {
            musicSource.loop = true;
            musicSource.Play();
        }

        // Fade in
        if (fadeTime > 0f)
        {
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, targetVolume, t / fadeTime);
                yield return null;
            }
        }

        musicSource.volume = targetVolume;
        _fadeRoutine = null;
    }

    private IEnumerator PreloadBiomeMusicClips()
    {
        var seen = new HashSet<AudioClip>();
        foreach (var entry in biomeMusic)
        {
            var clip = entry?.clip;
            if (clip == null || !seen.Add(clip))
                continue;

            if (clip.loadState == AudioDataLoadState.Loaded ||
                clip.loadState == AudioDataLoadState.Failed)
                continue;

            clip.LoadAudioData();

            float t = 0f;
            while (clip.loadState == AudioDataLoadState.Loading &&
                   t < Mathf.Max(0.1f, preloadTimeoutSeconds))
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (clip.loadState == AudioDataLoadState.Failed)
                Debug.LogWarning($"[VFXManager] Failed to preload biome music: {clip.name}");

            yield return null;
        }

        _preloadRoutine = null;
    }

    private IEnumerator WaitThenFadeToClip(AudioClip newClip, float fadeTime)
    {
        if (newClip == null)
            yield break;

        if (newClip.loadState == AudioDataLoadState.Unloaded)
            newClip.LoadAudioData();

        float t = 0f;
        while (newClip.loadState == AudioDataLoadState.Loading &&
               t < Mathf.Max(0.1f, preloadTimeoutSeconds))
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (newClip.loadState == AudioDataLoadState.Loaded)
            yield return FadeToClipRoutine(newClip, fadeTime);
        else
            Debug.LogWarning($"[VFXManager] Biome clip not ready: {newClip.name}");
    }

    /// <summary>
    /// Called from SettingsUI. volume01 is 0â€“1.
    /// </summary>
    public void SetMusicVolume(float volume01)
    {
        _musicVolume = Mathf.Clamp01(volume01);

        if (musicSource != null)
            musicSource.volume = _musicVolume;
    }

    #endregion
}

[System.Serializable]
public class BiomeMusicEntry
{
    public BlockSpawnLocation biome;
    public AudioClip clip;
    public float fadeTime = 1.5f;
}

