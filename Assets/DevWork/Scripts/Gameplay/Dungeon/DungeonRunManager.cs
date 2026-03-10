using System;
using System.Collections;
using System.Collections.Generic;
using Lean.Pool;
using UnityEngine;

public class DungeonRunManager : MonoBehaviour
{
    public static DungeonRunManager Ins { get; private set; }

    [Header("References")]
    [SerializeField] private LocationLoader locationLoader;

    [Header("Profile Selection")]
    [SerializeField] private List<DungeonRunProfileSO> profiles = new List<DungeonRunProfileSO>();
    [SerializeField] private DungeonRunProfileSO fallbackProfile;
    [SerializeField] private bool useEntryBiomeToSelectProfile = true;

    [Header("Run Setup")]
    [SerializeField] private BlockSpawnLocation dungeonLocation = BlockSpawnLocation.Dungeon;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool allowEnterWhenDungeonLocked = true;

    [Header("Legacy Fallback (when no profile/stage)")]
    [SerializeField, Min(1)] private int legacyTotalWaves = 3;
    [SerializeField, Min(1f)] private float legacyPerWaveDuration = 20f;
    [SerializeField, Min(1f)] private float legacyTotalRunDuration = 90f;
    [SerializeField] private List<DungeonRewardEntry> legacySuccessRewards = new List<DungeonRewardEntry>();

    [Header("Monster Spawn")]
    [SerializeField] private Transform monsterSpawnRoot;
    [SerializeField] private List<Transform> monsterSpawnPoints = new List<Transform>();
    [SerializeField, Min(0f)] private float spawnPointJitterRadius = 0.35f;

    [Header("Flow")]
    [SerializeField] private bool lockNavigationDuringRun = true;
    [SerializeField] private bool returnToPreviousLocationOnFail = true;
    [SerializeField] private bool saveOnRunEnd = true;

    [Header("Rewards")]
    [SerializeField] private bool grantRewardsOnSuccess = true;

    public bool IsRunning => isRunning;
    public bool IsWaitingMiniGameResult => waitingMiniGameResult;
    public int CurrentStage => currentStage;
    public int TotalStages => activeStages.Count;
    public int CurrentWave => currentWave;
    public int TotalWaves => totalWaves;
    public float RemainingRunTime => remainingRunTime;
    public float RemainingStageTime => remainingStageTime;
    public DungeonRunProfileSO ActiveProfile => activeProfile;
    public DungeonStageData ActiveStageData => activeStageData;
    public BlockSpawnLocation DungeonLocation => dungeonLocation;

    public event Action OnRunStarted;
    public event Action<bool> OnRunEnded;

    // Backward-compatible events.
    public event Action<int, int> OnWaveStarted;
    public event Action<float, float> OnTimerUpdated;

    // New events.
    public event Action<DungeonRunProfileSO> OnProfileSelected;
    public event Action<int, int, DungeonStageData> OnStageStarted;
    public event Action<int, int, float, float> OnStageTimerUpdated;
    public event Action<DungeonStageData> OnMiniGameRequested;
    public event Action<DungeonStageData, bool> OnMiniGameResolved;

    private Coroutine runRoutine;
    private bool isRunning;
    private int currentStage;
    private int currentWave;
    private int totalWaves;
    private float remainingRunTime;
    private float remainingStageTime;
    private BlockSpawnLocation previousLocation;
    private bool hasPreviousLocation;
    private BlockSpawnLocation entryBiome;
    private DungeonRunProfileSO activeProfile;
    private DungeonStageData activeStageData;

    private bool waitingMiniGameResult;
    private bool miniGameResolved;
    private bool miniGameSuccess;

    private int requiredMonsterKills;
    private int currentMonsterKills;
    private bool stageFailedByMiss;

    private readonly List<DungeonStageData> activeStages = new List<DungeonStageData>();
    private readonly List<MonsterClickable> stageMonsters = new List<MonsterClickable>();

    void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }

        Ins = this;
        if (locationLoader == null)
            locationLoader = LocationLoader.Ins;
    }

    void OnDestroy()
    {
        if (Ins == this)
            Ins = null;
    }

    void OnDisable()
    {
        if (isRunning)
            FinishRun(success: false);
    }

    public bool TryEnterRun()
    {
        if (isRunning)
            return false;

        if (locationLoader == null)
            locationLoader = LocationLoader.Ins;

        if (locationLoader == null)
        {
            Debug.LogError("[DungeonRun] Missing LocationLoader.");
            return false;
        }

        bool dungeonLocked = !locationLoader.IsLocationUnlocked(dungeonLocation);
        if (dungeonLocked && !allowEnterWhenDungeonLocked)
        {
            Debug.LogWarning($"[DungeonRun] Dungeon '{dungeonLocation}' is locked.");
            return false;
        }

        entryBiome = locationLoader.currentLocation;
        previousLocation = entryBiome;
        hasPreviousLocation = true;

        SelectActiveProfile();
        BuildActiveStageList();
        if (activeStages.Count == 0)
        {
            Debug.LogWarning("[DungeonRun] No active stages to run.");
            return false;
        }

        if (lockNavigationDuringRun)
            UIManager.Ins?.SetNavigationLocked(true, forceToMain: true);

        if (locationLoader.currentLocation != dungeonLocation)
            locationLoader.SetLocation((int)dungeonLocation, isInitiate: dungeonLocked);

        StartRun();
        return true;
    }

    public void ExitRunAsFail()
    {
        if (!isRunning)
            return;

        FinishRun(success: false);
    }

    public void ExitRunAsSuccess()
    {
        if (!isRunning)
            return;

        FinishRun(success: true);
    }

    public bool CompleteCurrentMiniGame(bool success)
    {
        if (!isRunning || !waitingMiniGameResult)
            return false;

        miniGameResolved = true;
        miniGameSuccess = success;
        waitingMiniGameResult = false;
        return true;
    }

    public void EnterRunFromButton()
    {
        TryEnterRun();
    }

    public void ExitRunFromButton()
    {
        ExitRunAsFail();
    }

    [ContextMenu("Dungeon/Enter Run")]
    void DebugEnterRun()
    {
        TryEnterRun();
    }

    [ContextMenu("Dungeon/Force Success")]
    void DebugSuccessRun()
    {
        ExitRunAsSuccess();
    }

    [ContextMenu("Dungeon/Force Fail")]
    void DebugFailRun()
    {
        ExitRunAsFail();
    }

    void SelectActiveProfile()
    {
        activeProfile = null;

        if (useEntryBiomeToSelectProfile && profiles != null && profiles.Count > 0)
        {
            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile == null)
                    continue;
                if (profile.sourceBiome == entryBiome)
                {
                    activeProfile = profile;
                    break;
                }
            }
        }

        if (activeProfile == null && profiles != null && profiles.Count > 0)
        {
            for (int i = 0; i < profiles.Count; i++)
            {
                var profile = profiles[i];
                if (profile == null)
                    continue;
                if (profile.sourceBiome == BlockSpawnLocation.Any)
                {
                    activeProfile = profile;
                    break;
                }
            }
        }

        if (activeProfile == null)
            activeProfile = fallbackProfile;

        OnProfileSelected?.Invoke(activeProfile);
    }

    void BuildActiveStageList()
    {
        activeStages.Clear();

        if (activeProfile != null && activeProfile.stages != null)
        {
            for (int i = 0; i < activeProfile.stages.Count; i++)
            {
                var stage = activeProfile.stages[i];
                if (stage != null)
                    activeStages.Add(stage);
            }
        }

        if (activeStages.Count == 0)
        {
            int waveCount = Mathf.Max(1, legacyTotalWaves);
            float perWave = Mathf.Max(0.1f, legacyPerWaveDuration);
            for (int i = 0; i < waveCount; i++)
            {
                activeStages.Add(new DungeonStageData
                {
                    stageId = $"legacy_wave_{i + 1}",
                    stageType = DungeonStageType.MonsterWave,
                    duration = perWave,
                    requireKillAllMonsters = false,
                    monsters = new List<DungeonMonsterEntry>()
                });
            }
        }

        remainingRunTime = activeProfile != null
            ? Mathf.Max(1f, activeProfile.runTimeLimit)
            : Mathf.Max(1f, legacyTotalRunDuration);

        totalWaves = 0;
        for (int i = 0; i < activeStages.Count; i++)
        {
            if (activeStages[i].stageType == DungeonStageType.MonsterWave)
                totalWaves++;
        }
    }

    void StartRun()
    {
        isRunning = true;
        currentStage = 0;
        currentWave = 0;
        activeStageData = null;

        OnRunStarted?.Invoke();
        OnTimerUpdated?.Invoke(remainingRunTime, 0f);

        if (runRoutine != null)
            StopCoroutine(runRoutine);
        runRoutine = StartCoroutine(RunLoop());
    }

    IEnumerator RunLoop()
    {
        int stageCount = activeStages.Count;

        for (int i = 0; i < stageCount && isRunning; i++)
        {
            var stage = activeStages[i];
            activeStageData = stage;
            currentStage = i + 1;
            remainingStageTime = Mathf.Max(0.1f, stage.duration);

            PrepareStage(stage);
            OnStageStarted?.Invoke(currentStage, stageCount, stage);

            if (stage.stageType == DungeonStageType.MonsterWave)
            {
                currentWave++;
                OnWaveStarted?.Invoke(currentWave, Mathf.Max(1, totalWaves));
            }

            while (isRunning)
            {
                float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                if (dt < 0f) dt = 0f;

                remainingRunTime -= dt;
                remainingStageTime -= dt;
                CleanupStageMonsterList();

                float safeRunTime = Mathf.Max(0f, remainingRunTime);
                float safeStageTime = Mathf.Max(0f, remainingStageTime);
                OnTimerUpdated?.Invoke(safeRunTime, safeStageTime);
                OnStageTimerUpdated?.Invoke(currentStage, stageCount, safeRunTime, safeStageTime);

                if (remainingRunTime <= 0f)
                {
                    FinishRun(success: false);
                    yield break;
                }

                bool stageDone = false;
                bool stageSuccess = true;

                if (stage.stageType == DungeonStageType.MonsterWave)
                {
                    if (stageFailedByMiss)
                    {
                        stageDone = true;
                        stageSuccess = false;
                    }
                    else if (requiredMonsterKills <= 0)
                    {
                        stageDone = true;
                        stageSuccess = true;
                    }
                    else if (stage.requireKillAllMonsters)
                    {
                        if (currentMonsterKills >= requiredMonsterKills)
                        {
                            stageDone = true;
                            stageSuccess = true;
                        }
                        else if (remainingStageTime <= 0f)
                        {
                            stageDone = true;
                            stageSuccess = false;
                        }
                    }
                    else if (remainingStageTime <= 0f || currentMonsterKills >= requiredMonsterKills)
                    {
                        stageDone = true;
                        stageSuccess = true;
                    }
                }
                else
                {
                    if (miniGameResolved)
                    {
                        stageDone = true;
                        stageSuccess = miniGameSuccess;
                        waitingMiniGameResult = false;
                        OnMiniGameResolved?.Invoke(stage, stageSuccess);
                    }
                    else if (remainingStageTime <= 0f)
                    {
                        stageDone = true;
                        stageSuccess = stage.miniGameAutoSuccessOnTimeout;
                        waitingMiniGameResult = false;
                        OnMiniGameResolved?.Invoke(stage, stageSuccess);
                    }
                }

                if (stageDone)
                {
                    ClearStageMonsters(despawnAlive: true);
                    if (!stageSuccess)
                    {
                        FinishRun(success: false);
                        yield break;
                    }

                    break;
                }

                yield return null;
            }
        }

        if (isRunning)
            FinishRun(success: true);
    }

    void PrepareStage(DungeonStageData stage)
    {
        ClearStageMonsters(despawnAlive: true);
        waitingMiniGameResult = false;
        miniGameResolved = false;
        miniGameSuccess = false;
        requiredMonsterKills = 0;
        currentMonsterKills = 0;
        stageFailedByMiss = false;

        if (stage == null)
            return;

        if (stage.stageType == DungeonStageType.MonsterWave)
            SpawnStageMonsters(stage);
        else
            BeginMiniGameStage(stage);
    }

    void BeginMiniGameStage(DungeonStageData stage)
    {
        waitingMiniGameResult = true;
        miniGameResolved = false;
        miniGameSuccess = false;
        OnMiniGameRequested?.Invoke(stage);
    }

    void SpawnStageMonsters(DungeonStageData stage)
    {
        if (stage == null || stage.monsters == null || stage.monsters.Count == 0)
            return;

        for (int i = 0; i < stage.monsters.Count; i++)
        {
            var entry = stage.monsters[i];
            if (entry == null || entry.monster == null || entry.monster.prefab == null)
                continue;

            int count = Mathf.Max(0, entry.count);
            requiredMonsterKills += count;
            for (int j = 0; j < count; j++)
                SpawnSingleMonster(entry.monster);
        }
    }

    void SpawnSingleMonster(MonsterDef def)
    {
        Vector3 pos = ResolveSpawnPosition();
        Transform parent = monsterSpawnRoot != null ? monsterSpawnRoot : null;
        var go = LeanPool.Spawn(def.prefab, pos, Quaternion.identity, parent);
        if (go == null)
            return;

        var clickable = go.GetComponent<MonsterClickable>();
        if (clickable == null)
            clickable = go.AddComponent<MonsterClickable>();

        clickable.Resolved -= OnStageMonsterResolved;
        clickable.Resolved += OnStageMonsterResolved;
        clickable.Init(def, null);
        stageMonsters.Add(clickable);

        if (def.appearSfx != null)
            SoundEffectController.Ins?.PlaySFX(def.appearSfx);
    }

    Vector3 ResolveSpawnPosition()
    {
        Transform anchor = null;

        if (monsterSpawnPoints != null && monsterSpawnPoints.Count > 0)
        {
            int start = UnityEngine.Random.Range(0, monsterSpawnPoints.Count);
            for (int i = 0; i < monsterSpawnPoints.Count; i++)
            {
                var candidate = monsterSpawnPoints[(start + i) % monsterSpawnPoints.Count];
                if (candidate != null)
                {
                    anchor = candidate;
                    break;
                }
            }
        }

        if (anchor == null)
            anchor = monsterSpawnRoot != null ? monsterSpawnRoot : transform;

        Vector3 pos = anchor.position;
        if (spawnPointJitterRadius > 0f)
        {
            Vector2 jitter = UnityEngine.Random.insideUnitCircle * spawnPointJitterRadius;
            pos += new Vector3(jitter.x, 0f, jitter.y);
        }

        return pos;
    }

    void OnStageMonsterResolved(MonsterClickable monster, bool killed)
    {
        if (monster != null)
            monster.Resolved -= OnStageMonsterResolved;

        stageMonsters.Remove(monster);

        if (!isRunning)
            return;

        if (killed)
            currentMonsterKills++;
        else
            stageFailedByMiss = true;
    }

    void CleanupStageMonsterList()
    {
        for (int i = stageMonsters.Count - 1; i >= 0; i--)
        {
            var monster = stageMonsters[i];
            if (monster == null)
            {
                stageMonsters.RemoveAt(i);
                stageFailedByMiss = true;
                continue;
            }

            if (!monster.gameObject.activeInHierarchy)
            {
                monster.Resolved -= OnStageMonsterResolved;
                stageMonsters.RemoveAt(i);
                stageFailedByMiss = true;
            }
        }
    }

    void ClearStageMonsters(bool despawnAlive)
    {
        for (int i = 0; i < stageMonsters.Count; i++)
        {
            var monster = stageMonsters[i];
            if (monster == null)
                continue;

            monster.Resolved -= OnStageMonsterResolved;
            if (despawnAlive && monster.gameObject != null && monster.gameObject.activeInHierarchy)
                LeanPool.Despawn(monster.gameObject);
        }

        stageMonsters.Clear();
        requiredMonsterKills = 0;
        currentMonsterKills = 0;
        stageFailedByMiss = false;
    }

    void FinishRun(bool success)
    {
        if (!isRunning)
            return;

        isRunning = false;

        if (runRoutine != null)
        {
            StopCoroutine(runRoutine);
            runRoutine = null;
        }

        ClearStageMonsters(despawnAlive: true);
        activeStageData = null;
        waitingMiniGameResult = false;
        miniGameResolved = false;
        miniGameSuccess = false;

        if (success && grantRewardsOnSuccess)
            GrantSuccessRewards();

        bool shouldReturnToPrevious = success || returnToPreviousLocationOnFail;
        if (shouldReturnToPrevious)
            ReturnToPreviousLocation();

        if (lockNavigationDuringRun)
            UIManager.Ins?.SetNavigationLocked(false);

        if (saveOnRunEnd)
            DataSaver.Ins?.SaveDataFn(force: true, forceLocalWrite: true);

        OnRunEnded?.Invoke(success);
    }

    void ReturnToPreviousLocation()
    {
        if (!hasPreviousLocation)
            return;

        if (locationLoader == null)
            locationLoader = LocationLoader.Ins;
        if (locationLoader == null)
            return;

        if (locationLoader.currentLocation != previousLocation)
            locationLoader.SetLocation((int)previousLocation, isInitiate: false);
    }

    void GrantSuccessRewards()
    {
        var rewards = GetActiveRewardList();
        if (rewards == null || rewards.Count == 0)
            return;

        var inventory = InventoryController.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("[DungeonRun] InventoryController.Instance is null, skip rewards.");
            return;
        }

        for (int i = 0; i < rewards.Count; i++)
        {
            var reward = rewards[i];
            if (reward == null || reward.item == null)
                continue;

            float chance = Mathf.Clamp01(reward.dropChance);
            if (chance < 1f && UnityEngine.Random.value > chance)
                continue;

            int min = Mathf.Max(1, reward.minAmount);
            int max = Mathf.Max(min, reward.maxAmount);
            int amount = UnityEngine.Random.Range(min, max + 1);
            if (amount <= 0)
                continue;

            var inventoryItem = new InventoryItem(reward.item, amount);
            bool ok = inventory.TryAddItemToInventory(inventoryItem);

            int remain = inventoryItem.quantity != null ? Mathf.Max(0, inventoryItem.quantity.Value) : 0;
            int added = Mathf.Max(0, amount - remain);

            if (added > 0)
            {
                bool rainbow = reward.item.rarity == Rarity.Exclusive;
                Vector2 pos = Toaster.GetRandomAnchoredPosition();
                Toaster.Show($"+{added}", reward.item.icon, 1.6f, pos, rainbow);
            }

            if (!ok || remain > 0)
                Debug.LogWarning($"[DungeonRun] Reward partially added: {reward.item.itemName}, added={added}, remain={remain}");
        }
    }

    List<DungeonRewardEntry> GetActiveRewardList()
    {
        if (activeProfile != null && activeProfile.successRewards != null && activeProfile.successRewards.Count > 0)
            return activeProfile.successRewards;
        return legacySuccessRewards;
    }
}
