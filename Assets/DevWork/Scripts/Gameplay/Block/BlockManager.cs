using System;
using System.Collections;
using Lean.Pool;
using UnityEngine;

public class BlockManager : MonoBehaviour
{
    // Legacy global path: scene orchestration still routes here, but avoid adding new singleton coupling.
    public static BlockManager Ins;
    [SerializeField] private ClickableObject currentBlock;
    [SerializeField] private LocationLoader locationLoader;
    [SerializeField] private bool enableMonsterEncounters = false;
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] BossSO bossSO;
    [SerializeField] Transform  spawnPos;
    public float rareWeightCap = 10;
    public ClickableObject CurrentBlock => currentBlock;
    public BossSO BossDatabase => bossSO;
    public MonsterSpawner MonsterSpawner => enableMonsterEncounters ? monsterSpawner : null;
    public event Action<string> CurrentBlockChanged;
    public event Action<int, int> MonsterSpawnProgressChanged;
    public event Action<bool> MonsterEncounterStateChanged;
    private GameObject activeBoss;
    private BossEntry activeBossInfo;
    Boss activeBossComp;
    private Coroutine bossTimerCoroutine;
    private float bossRemainingTime;
    private float bossTotalTime;
    private bool warnedMissingMonsterSpawner;
    private bool canRefreshCurrentBlock;
    private bool restoreSavedBlockOnNextRefresh;
    public bool IsBossTimerRunning => bossTimerCoroutine != null;
    public float BossRemainingTime => bossRemainingTime;
    public float BossTotalTime => bossTotalTime;
    public event Action<float, float> OnBossTimerUpdated;
    void Awake()
    {
        if (Ins && Ins != this) { Destroy(gameObject); return; }
        Ins = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartCoroutine(InitWhenReady());
    }

    void OnEnable()
    {
        if (enableMonsterEncounters)
            BindMonsterSpawner();
    }

    void OnDisable()
    {
        UnbindMonsterSpawner();
        StopBossTimer();
    }

    private IEnumerator InitWhenReady()
    {
        yield return new WaitUntil(() => DataSaver.Ins != null && DataSaver.Ins.IsReady);
        yield return new WaitUntil(() => locationLoader == null || locationLoader.IsInitialized);
        yield return new WaitUntil(() => TimeSystem.Instance == null || TimeSystem.Instance.IsInitialized);
        yield return new WaitUntil(() => WeatherManager.Instance == null || WeatherManager.Instance.IsInitialized);
        JournalManager.GetOrCreate();

        if (enableMonsterEncounters && monsterSpawner != null && currentBlock != null)
            monsterSpawner.SetBlockAnchor(currentBlock.transform);

        canRefreshCurrentBlock = true;
        restoreSavedBlockOnNextRefresh = true;
        RefreshBlockForLocationChange();
    }


    public GameObject Summon(BlockSpawnLocation bossLocation, BossType bossType)
    {
        if (activeBoss)
        {
            DevLog.Log("[BossSpawner] Boss already active.");
            return activeBoss;
        }

        Vector3 pos = spawnPos ? spawnPos.position : Vector3.zero;
        Quaternion rot = spawnPos ? spawnPos.rotation : Quaternion.identity;

        activeBossInfo = bossSO.FindOne(bossLocation, bossType);

        if (activeBossInfo == null)
        {
            Debug.LogWarning($"[BossSpawner] No boss found for location {bossLocation} and type {bossType}");
            return null;
        }

        if (activeBossInfo.bossPrefab == null)
        {
            Debug.LogWarning($"[BossSpawner] Boss prefab is null for boss {activeBossInfo.bossName}");
            return null;
        }

        if (JournalManager.Ins != null && !JournalManager.Ins.IsBossUnlocked(activeBossInfo.bossName))
        {
            DevLog.Log($"[BossSpawner] Boss {activeBossInfo.bossName} is still locked by Journal.");
            return null;
        }

        if (IsBossOutOfCondition())
        {
            DevLog.Log($"[BossSpawner] Boss {activeBossInfo.bossName} cannot be summoned due to time/weather conditions.");
            // Game log
            return null;
        }

        // Spawn
        var bossPrefab = activeBossInfo.bossPrefab;
        var go = LeanPool.Spawn(bossPrefab, pos, rot);
        activeBoss = go;

        activeBossComp = go.GetComponent<Boss>();
        if (activeBossComp == null)
        {
            Debug.LogError($"[BossSpawner] Spawned boss prefab '{bossPrefab.name}' is missing Boss component. Despawning instance.", go);
            LeanPool.Despawn(go);
            activeBoss = null;
            activeBossInfo = null;
            return null;
        }

        activeBossComp.Died += OnBossDied;
        UIManager.Ins.SetNavigationLocked(true, forceToMain: true);

        AnalyticsManager.Ins?.TrackBossSpawn(activeBossInfo.bossName, bossLocation.ToString());
        activeBossComp.SetSpawnContext(activeBossInfo);
        StartBossTimer(Mathf.Max(1f, activeBossInfo.timeLimitSeconds));
        if (currentBlock) currentBlock.gameObject.SetActive(false);
        return go;
    }
    public void OnBossDied(Boss boss)
    {
        if (activeBossComp != null) activeBossComp.Died -= OnBossDied;
        StopBossTimer();

        if (boss && boss.gameObject && boss.gameObject.activeInHierarchy)
            LeanPool.Despawn(boss.gameObject);

        activeBoss = null;
        activeBossComp = null;
        activeBossInfo = null;

        if (currentBlock)
            currentBlock.gameObject.SetActive(true);
        UIManager.Ins.SetNavigationLocked(false);
    }
    public void OnBlockBroken()
    {
        string blockId = currentBlock.BlockName;

        var biome = locationLoader.currentLocation;
        string biomeName = biome.ToString();

        string targetId = $"{blockId}@{biomeName}";

        QuestSignals.BreakBlock(targetId, 1);
        GameplayProgressSignals.RaiseBlockBroken(blockId, biomeName, 1);

        NormalWeatherName normalName = ResolveCurrentNormalWeather();
        SpecialWeatherName specialName = ResolveCurrentSpecialWeather();
        DataSaver.Ins.SaveDataFn();
        CameraShakeController.TriggerBlockBreakShake(1.45f);
        currentBlock.SetClickableBlockByCondition(
            locationLoader.currentLocation,
            ResolveCurrentTimeState(),
            normalName,
            specialName
        );
        NotifyCurrentBlockChanged();
        NotifyMonsterSpawnerBlockBroken();
    }

    public void RefreshBlockForLocationChange()
    {
        if (currentBlock == null)
            return;

        if (!canRefreshCurrentBlock)
            return;

        BlockSpawnLocation location = locationLoader != null
            ? locationLoader.currentLocation
            : (DataSaver.Ins != null && DataSaver.Ins.currentLocation.HasValue ? DataSaver.Ins.currentLocation.Value : BlockSpawnLocation.Plain);

        TimeState timeState = ResolveCurrentTimeState();
        NormalWeatherName normalName = ResolveCurrentNormalWeather();
        SpecialWeatherName specialName = ResolveCurrentSpecialWeather();

        if (restoreSavedBlockOnNextRefresh)
        {
            restoreSavedBlockOnNextRefresh = false;
            if (TryRestoreSavedBlockForCurrentConditions(location, timeState, normalName, specialName))
            {
                NotifyCurrentBlockChanged();
                return;
            }
        }

        currentBlock.SetClickableBlockByCondition(
            location,
            timeState,
            normalName,
            specialName
        );
        NotifyCurrentBlockChanged();
    }


    // Boss
    public bool IsBossOutOfCondition()
    {
        return activeBossInfo.Matches(TimeSystem.Instance.CurrentTimeState.Value, WeatherManager.Instance.CurrentNormalWeather.Value, WeatherManager.Instance.CurrentSpecialWeather.Value) == false;
    }

    private void StartBossTimer(float durationSeconds)
    {
        StopBossTimer();

        bossTotalTime = Mathf.Max(1f, durationSeconds);
        bossRemainingTime = bossTotalTime;
        OnBossTimerUpdated?.Invoke(bossRemainingTime, bossTotalTime);
        bossTimerCoroutine = StartCoroutine(BossTimer_Co());
    }

    private void StopBossTimer()
    {
        if (bossTimerCoroutine != null)
        {
            StopCoroutine(bossTimerCoroutine);
            bossTimerCoroutine = null;
        }

        bossRemainingTime = 0f;
        bossTotalTime = 0f;
        OnBossTimerUpdated?.Invoke(0f, 0f);
    }

    private IEnumerator BossTimer_Co()
    {
        while (activeBoss != null && activeBossComp != null && bossRemainingTime > 0f)
        {
            bossRemainingTime -= Time.unscaledDeltaTime;
            float safeRemaining = Mathf.Max(0f, bossRemainingTime);
            OnBossTimerUpdated?.Invoke(safeRemaining, bossTotalTime);

            if (safeRemaining <= 0f)
            {
                HandleBossTimeoutFail();
                yield break;
            }

            yield return null;
        }

        bossTimerCoroutine = null;
    }

    private void HandleBossTimeoutFail()
    {
        StopBossTimer();
        RunFailNotifierRuntime.NotifyRunFailed(PlayerRunFailReason.BossTimeout);

        if (activeBossComp != null)
            activeBossComp.Died -= OnBossDied;

        if (activeBoss != null && activeBoss.activeInHierarchy)
            LeanPool.Despawn(activeBoss);

        activeBoss = null;
        activeBossComp = null;
        activeBossInfo = null;

        if (currentBlock)
            currentBlock.gameObject.SetActive(true);

        UIManager.Ins?.SetNavigationLocked(false);
    }

    private void NotifyCurrentBlockChanged()
    {
        string blockName = currentBlock != null ? currentBlock.BlockName : string.Empty;
        CurrentBlockChanged?.Invoke(blockName);
    }

    private void NotifyMonsterSpawnerBlockBroken()
    {
        if (!enableMonsterEncounters)
            return;

        if (!TryEnsureMonsterSpawner())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!warnedMissingMonsterSpawner)
            {
                warnedMissingMonsterSpawner = true;
                Debug.LogWarning("[BlockManager] MonsterSpawner is not assigned. Block-break spawn trigger is disabled.", this);
            }
#endif
            return;
        }

        monsterSpawner.NotifyBlockBroken();
    }

    private bool TryEnsureMonsterSpawner()
    {
        if (!enableMonsterEncounters)
            return false;

        if (monsterSpawner != null)
            return true;

        monsterSpawner = FindObjectOfType<MonsterSpawner>();
        if (monsterSpawner == null)
            return false;

        warnedMissingMonsterSpawner = false;
        if (currentBlock != null)
            monsterSpawner.SetBlockAnchor(currentBlock.transform);
        return true;
    }

    private void BindMonsterSpawner()
    {
        if (!enableMonsterEncounters)
        {
            MonsterSpawnProgressChanged?.Invoke(0, 1);
            MonsterEncounterStateChanged?.Invoke(false);
            return;
        }

        if (!TryEnsureMonsterSpawner())
            return;

        monsterSpawner.SpawnProgressChanged -= OnMonsterSpawnProgressChanged;
        monsterSpawner.SpawnProgressChanged += OnMonsterSpawnProgressChanged;

        monsterSpawner.EncounterStateChanged -= OnMonsterEncounterStateChanged;
        monsterSpawner.EncounterStateChanged += OnMonsterEncounterStateChanged;

        if (currentBlock != null)
            monsterSpawner.SetBlockAnchor(currentBlock.transform);

        MonsterSpawnProgressChanged?.Invoke(monsterSpawner.CurrentBreakProgress, monsterSpawner.BlocksPerSpawn);
        MonsterEncounterStateChanged?.Invoke(monsterSpawner.HasActiveEncounter);
    }

    private void UnbindMonsterSpawner()
    {
        if (monsterSpawner == null)
            return;

        monsterSpawner.SpawnProgressChanged -= OnMonsterSpawnProgressChanged;
        monsterSpawner.EncounterStateChanged -= OnMonsterEncounterStateChanged;
    }

    private void OnMonsterSpawnProgressChanged(int progress, int threshold)
    {
        MonsterSpawnProgressChanged?.Invoke(progress, threshold);
    }

    private void OnMonsterEncounterStateChanged(bool active)
    {
        MonsterEncounterStateChanged?.Invoke(active);

        if (currentBlock == null)
            return;

        if (active)
        {
            currentBlock.gameObject.SetActive(false);
            return;
        }

        if (activeBoss == null)
            currentBlock.gameObject.SetActive(true);
    }

    private bool IsMonsterEncounterRunning()
    {
        return enableMonsterEncounters && monsterSpawner != null && monsterSpawner.HasActiveEncounter;
    }

    private bool TryRestoreSavedBlockForCurrentConditions(
        BlockSpawnLocation location,
        TimeState timeState,
        NormalWeatherName normalWeather,
        SpecialWeatherName specialWeather)
    {
        if (currentBlock == null || DataSaver.Ins == null || currentBlock.blockUVDatabase == null)
            return false;

        string savedBlock = DataSaver.Ins.currentBlock;
        if (string.IsNullOrWhiteSpace(savedBlock))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevLog.Log("[BlockInit] Saved block is empty. Falling back to condition roll.");
#endif
            return false;
        }

        if (!currentBlock.blockUVDatabase.IsBlockValidForConditions(savedBlock, location, timeState, normalWeather, specialWeather))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevLog.Log($"[BlockInit] Saved block '{savedBlock}' is invalid for location={location}, time={timeState}, normal={normalWeather}, special={specialWeather}. Falling back to condition roll.");
#endif
            return false;
        }

        currentBlock.SetClickableBlock(savedBlock);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevLog.Log($"[BlockInit] Restored saved block '{savedBlock}' for location={location}, time={timeState}, normal={normalWeather}, special={specialWeather}.");
#endif
        return true;
    }

    private static TimeState ResolveCurrentTimeState()
    {
        return TimeSystem.Instance != null
            ? TimeSystem.Instance.CurrentTimeState.Value
            : TimeState.Any;
    }

    private static NormalWeatherName ResolveCurrentNormalWeather()
    {
        return (WeatherManager.Instance != null ? WeatherManager.Instance.CurrentNormalWeather.Value as NormalWeatherData : null)?.weatherName
            ?? NormalWeatherName.Any;
    }

    private static SpecialWeatherName ResolveCurrentSpecialWeather()
    {
        return (WeatherManager.Instance != null ? WeatherManager.Instance.CurrentSpecialWeather.Value as SpecialWeatherData : null)?.weatherName
            ?? SpecialWeatherName.Any;
    }
}

