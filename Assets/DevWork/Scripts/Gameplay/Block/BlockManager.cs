using System;
using System.Collections;
using Lean.Pool;
using UnityEngine;

public class BlockManager : MonoBehaviour
{
    public static BlockManager Ins;
    [SerializeField] private ClickableObject currentBlock;
    [SerializeField] private LocationLoader locationLoader;
    [SerializeField] BossSO bossSO;
    [SerializeField] Transform  spawnPos;
    public float rareWeightCap = 10;
    public ClickableObject CurrentBlock => currentBlock;
    public event Action<string> CurrentBlockChanged;
    private GameObject activeBoss;
    private BossEntry activeBossInfo;
    Boss activeBossComp;
    private Coroutine bossTimerCoroutine;
    private float bossRemainingTime;
    private float bossTotalTime;
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

    void OnDisable()
    {
        StopBossTimer();
    }

    private IEnumerator InitWhenReady()
    {
        yield return new WaitUntil(() => DataSaver.Ins != null);

        if (currentBlock != null)
        {
            currentBlock.SetClickableBlock(DataSaver.Ins.currentBlock ?? "Dirt");
            NotifyCurrentBlockChanged();
        }

        int startIndex = DataSaver.Ins.currentLocation.HasValue ? (int)DataSaver.Ins.currentLocation.Value : 1;
        if (startIndex == 0)
            startIndex = 1;
        if (locationLoader != null)
            locationLoader.SetLocation(startIndex, isInitiate: true);
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
        BlockSpawnLocation clearedLocation = activeBossInfo != null
            ? activeBossInfo.biome
            : (locationLoader != null ? locationLoader.currentLocation : BlockSpawnLocation.Plain);

        if (locationLoader != null)
            locationLoader.TryUnlockNextLocationFromBoss(clearedLocation);

        if (activeBossComp != null) activeBossComp.Died -= OnBossDied;
        StopBossTimer();

        if (boss && boss.gameObject && boss.gameObject.activeInHierarchy)
            LeanPool.Despawn(boss.gameObject);

        activeBoss = null;
        activeBossComp = null;
        activeBossInfo = null;

        if (currentBlock) currentBlock.gameObject.SetActive(true);
        UIManager.Ins.SetNavigationLocked(false);
    }
    public void OnBlockBroken()
    {
        string blockId = currentBlock.BlockName;

        var biome = locationLoader.currentLocation;
        string biomeName = biome.ToString();

        string targetId = $"{blockId}@{biomeName}";

        QuestSignals.BreakBlock(targetId, 1);

        NormalWeatherName normalName =
            (WeatherManager.Instance.CurrentNormalWeather.Value as NormalWeatherData)?.weatherName
            ?? NormalWeatherName.Any;

        SpecialWeatherName specialName =
            (WeatherManager.Instance.CurrentSpecialWeather.Value as SpecialWeatherData)?.weatherName
            ?? SpecialWeatherName.Any;
        DataSaver.Ins.SaveDataFn();
        CameraShakeController.TriggerBlockBreakShake(1.45f);
        currentBlock.SetClickableBlockByCondition(
            locationLoader.currentLocation,
            TimeSystem.Instance.CurrentTimeState.Value,
            normalName,
            specialName
        );
        NotifyCurrentBlockChanged();
    }

    public void RefreshBlockForLocationChange()
    {
        NormalWeatherName normalName =
            (WeatherManager.Instance.CurrentNormalWeather.Value as NormalWeatherData)?.weatherName
            ?? NormalWeatherName.Any;

        SpecialWeatherName specialName =
            (WeatherManager.Instance.CurrentSpecialWeather.Value as SpecialWeatherData)?.weatherName
            ?? SpecialWeatherName.Any;

        currentBlock.SetClickableBlockByCondition(
            locationLoader.currentLocation,
            TimeSystem.Instance.CurrentTimeState.Value,
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
}

