using UnityEngine;
using Firebase.Firestore;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using UniRx;

public enum CloudSyncMode
{
    PeriodicAndLifecycle,
    LifecycleOnly
}

public class DataSaver : MonoBehaviour
{
    public static DataSaver Ins { get; private set; }

    [Header("Gameplay")]
    public string currentBlock;
    public BlockSpawnLocation? currentLocation;
    public BlockSpawnLocation? PeakLocation;
    public float CurrentTime;
    public float TotalPlaytime;

    [Header("Profile")]
    public string DisplayName;
    public string AvatarId;

    [Header("Autosave")]
    private IntReactiveProperty blockBreakCounter = new IntReactiveProperty(0);
    private const int SaveThreshold = 10;

    [SerializeField] private List<InventoryData> inventoryDatas = new List<InventoryData>();
    [SerializeField] private CraftNodeManager craftNodeManager;
    private readonly Dictionary<string, List<int>> craftNodeStatesByBiomeCache = new Dictionary<string, List<int>>();
    private List<int> pendingLegacyCraftNodeStates;

    [Header("Local Save")]
    [SerializeField, Min(0.1f)] private float localSaveCooldown = 2f;

    // Firestore
    private FirebaseFirestore db;

    // Throttle to avoid save spam (clicker)
    [Header("Cloud Sync Policy")]
    [SerializeField] private CloudSyncMode cloudSyncMode = CloudSyncMode.PeriodicAndLifecycle;
    [SerializeField] private bool forceCloudSyncOnLifecycle = true;
    [SerializeField, Min(30f)] private float cloudSaveCooldown = 900f;
    private float nextCloudSaveTime = 0f;
    private float nextLocalSaveTime = 0f;
    private bool pendingLocalSave;

    [Header("Cloud Save Retry")]
    [SerializeField] private int maxSaveAttempts = 3;
    [SerializeField] private float retryBaseDelaySeconds = 0.5f;
    [SerializeField] private float cloudSaveTimeoutSeconds = 20f;
    [SerializeField] private float cloudCommitTimeoutSeconds = 8f;

    [Header("Debug")]
    [SerializeField] private bool verboseSaveLogs = false;

    // Ready flag
    private bool isReady = false;
    private bool isQuitting = false;
    private Task ongoingSave;
    private float ongoingSaveStartTime;
    private bool pendingSave;
    private bool pendingForce;
    private bool allowSaves;
    private bool allowCloudSave;
    private bool hasLoadedData;
    private bool playtimeActive;
    private float cachedClicks;
    private float cachedDiamonds;
    private bool hasCachedGameplayStats;
    private bool pendingRuntimeStatApply;
    private long lastCloudUpdatedUtcTicks;
    private long lastLocalUpdatedUtcTicks;
    private const string LocalCacheFileName = "local_save.json";
    private const int MaxDisplayNameLength = 16;
    private const int MaxAvatarIdLength = 32;
    private const string DefaultCraftScope = "Default";

    void Awake()
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
        // Delay init until FirebaseBootstrap is ready
        StartCoroutine(InitWhenReady());
    }

    private IEnumerator InitWhenReady()
    {
        // Wait FirebaseBootstrap singleton exists
        yield return new WaitUntil(() => FirebaseBootstrap.Ins != null);

        // Wait Firebase ready or failed
        yield return new WaitUntil(() => FirebaseBootstrap.Ins.IsReady || FirebaseBootstrap.Ins.IsFailed);
        if (FirebaseBootstrap.Ins.IsFailed)
        {
            Debug.LogError($"[Error] DataSaver init aborted: {FirebaseBootstrap.Ins.InitError}");
            yield break;
        }

        db = FirebaseBootstrap.Ins.Db;

        // Subscribe once, only when ready
        blockBreakCounter
            .Where(count => count >= SaveThreshold)
            .Subscribe(_ => SaveDataFn())
            .AddTo(this);

        isReady = true;
        DevLog.Log($"[OK] DataSaver ready. uid={GetUid()}");
        AnalyticsManager.Ins?.UpdateUserProperties(this);
    }

    private string GetUid()
    {
        if (FirebaseBootstrap.Ins == null) return null;
        if (FirebaseBootstrap.Ins.Auth == null) return null;
        if (FirebaseBootstrap.Ins.Auth.CurrentUser == null) return null;
        return FirebaseBootstrap.Ins.Auth.CurrentUser.UserId;
    }

    private bool CanCloudSaveNow(out string uid, bool force, out string reason)
    {
        uid = null;
        reason = null;

        if (isQuitting && !force) { reason = "quitting"; return false; }
        if (!isReady) { reason = "not ready"; return false; }
        if (db == null) { reason = "db null"; return false; }
        if (!allowCloudSave) { reason = "data not loaded"; return false; }

        uid = GetUid();
        if (string.IsNullOrEmpty(uid)) { reason = "uid missing"; return false; }

        if (lastLocalUpdatedUtcTicks <= 0)
        {
            reason = "local updatedAt missing";
            return false;
        }
        if (lastCloudUpdatedUtcTicks > 0 && lastLocalUpdatedUtcTicks <= lastCloudUpdatedUtcTicks)
        {
            reason = "local not newer than cloud";
            return false;
        }
        if (!force && cloudSyncMode == CloudSyncMode.LifecycleOnly)
        {
            reason = "lifecycle-only cloud sync mode";
            return false;
        }

        // Cooldown to avoid write spam
        if (!force)
        {
            if (Time.unscaledTime < nextCloudSaveTime)
            {
                reason = $"cooldown ({(nextCloudSaveTime - Time.unscaledTime):F1}s)";
                return false;
            }
            nextCloudSaveTime = Time.unscaledTime + cloudSaveCooldown;
        }

        return true;
    }

    // Call this on break block/click depending on your game flow.
    public void IncreaseBreakCounter(int amount = 1)
    {
        blockBreakCounter.Value += amount;
    }

    private void QueueLocalSave(bool forceImmediate = false)
    {
        TouchLocalDataUpdated();

        float now = Time.unscaledTime;
        float localCooldown = Mathf.Max(0.1f, localSaveCooldown);
        if (forceImmediate || now >= nextLocalSaveTime)
        {
            SaveLocalCache();
            pendingLocalSave = false;
            nextLocalSaveTime = now + localCooldown;
            return;
        }

        pendingLocalSave = true;
    }

    public void SaveDataFn(bool force = false, bool forceLocalWrite = false)
    {
        if (verboseSaveLogs)
            DevLog.Log($"SaveDataFn called (force={force}).");

        if (!allowSaves)
        {
            if (verboseSaveLogs)
                DevLog.Log("SaveDataFn blocked: initial load not complete.");
            return;
        }

        TryApplyCachedGameplayStatsToRuntime();
        QueueLocalSave(forceLocalWrite || force);

        // Reset counter only when we actually save.
        if (!CanCloudSaveNow(out var uid, force, out var reason))
        {
            if (verboseSaveLogs)
                DevLog.Log($"SaveDataFn blocked: {reason ?? "unknown"}");
            return;
        }

        blockBreakCounter.Value = 0;

        if (ongoingSave != null && !ongoingSave.IsCompleted)
        {
            pendingSave = true;
            pendingForce |= force;
            if (verboseSaveLogs)
                DevLog.Log("SaveDataFn skipped: previous save still running.");
            return;
        }

        ongoingSaveStartTime = Time.unscaledTime;
        ongoingSave = FirebaseTaskTracker.Track(SaveCloudAsync(uid));
    }

    [ContextMenu("Debug/Force Save Now")]
    private void DebugForceSaveNow()
    {
        SaveDataFn(true);
    }

    void Update()
    {
        TryApplyCachedGameplayStatsToRuntime();

        if (playtimeActive)
            TotalPlaytime += Time.unscaledDeltaTime;

        if (pendingLocalSave && Time.unscaledTime >= nextLocalSaveTime)
            QueueLocalSave(forceImmediate: true);

        if (ongoingSave != null && !ongoingSave.IsCompleted)
        {
            float timeout = Mathf.Max(0f, cloudSaveTimeoutSeconds);
            if (timeout > 0f && Time.unscaledTime - ongoingSaveStartTime >= timeout)
            {
                if (verboseSaveLogs)
                    Debug.LogWarning($"SaveDataFn timeout after {timeout:F1}s, allowing next save.");
                ongoingSave = null;
            }
        }

        if (pendingSave && (ongoingSave == null || ongoingSave.IsCompleted))
        {
            bool force = pendingForce;
            pendingSave = false;
            pendingForce = false;
            SaveDataFn(force);
        }
    }

    private async Task SaveCloudAsync(string uid)
    {
        if (db == null || string.IsNullOrEmpty(uid)) return;

        var gameplay = BuildGameplaySaveData();
        var profile = BuildProfileSaveData();
        var inventories = BuildAllInventorySaveData();

        if (verboseSaveLogs)
            DevLog.Log($"SaveCloudAsync start (uid={uid}).");

        long updateTicks = lastLocalUpdatedUtcTicks > 0 ? lastLocalUpdatedUtcTicks : DateTime.UtcNow.Ticks;
        var updatedAt = Timestamp.FromDateTime(new DateTime(updateTicks, DateTimeKind.Utc));
        var leaderboard = BuildLeaderboardPublicData(gameplay, profile, updatedAt);

        bool saveSucceeded = await ExecuteWithRetry(async () =>
        {
            var batch = db.StartBatch();
            var userDoc = db.Collection("users").Document(uid);

            var payload = new Dictionary<string, object>
            {
                ["gameplay"] = gameplay,
                ["profile"] = profile,
                ["meta.updatedAt"] = updatedAt,
                ["meta.rev"] = FieldValue.Increment(1),
                ["meta.saveVersion"] = 1
            };

            batch.Set(userDoc, payload, SetOptions.MergeAll);

            var leaderboardDoc = db.Collection("leaderboards").Document(uid);
            batch.Set(leaderboardDoc, leaderboard, SetOptions.MergeAll);

            foreach (var entry in inventories)
            {
                var invDoc = userDoc.Collection("inventories").Document(entry.Key);
                batch.Set(invDoc, entry.Value);
            }

            await AwaitWithTimeout(batch.CommitAsync(), cloudCommitTimeoutSeconds, "Firestore commit");
        }, maxSaveAttempts, retryBaseDelaySeconds, "SaveCloudAsync");

        if (!saveSucceeded)
        {
            if (verboseSaveLogs)
                Debug.LogWarning("SaveCloudAsync failed. Cloud updatedAt was not advanced.");
            return;
        }

        lastCloudUpdatedUtcTicks = updateTicks;
        if (verboseSaveLogs)
            DevLog.Log("SaveCloudAsync completed.");
    }

    private async Task<bool> ExecuteWithRetry(Func<Task> action, int maxAttempts, float baseDelaySeconds, string opName)
    {
        int attempts = Mathf.Max(1, maxAttempts);
        float delay = Mathf.Max(0.05f, baseDelaySeconds);

        for (int i = 1; i <= attempts; i++)
        {
            try
            {
                await action();
                return true;
            }
            catch (Exception ex)
            {
                if (i >= attempts)
                {
                    Debug.LogError($"[Error] {opName} failed after {attempts} attempts: {ex}");
                    return false;
                }

                if (verboseSaveLogs)
                    Debug.LogWarning($"{opName} attempt {i} failed: {ex.Message}");

                float wait = delay * Mathf.Pow(2f, i - 1);
                await Task.Delay(TimeSpan.FromSeconds(wait));
            }
        }

        return false;
    }

    private async Task AwaitWithTimeout(Task task, float timeoutSeconds, string opName)
    {
        if (timeoutSeconds <= 0f)
        {
            await task;
            return;
        }

        var delay = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        var completed = await Task.WhenAny(task, delay);
        if (completed == delay)
            throw new TimeoutException($"{opName} timed out after {timeoutSeconds:F1}s");

        await task;
    }

    private GameplaySaveData BuildGameplaySaveData()
    {
        TryApplyCachedGameplayStatsToRuntime();

        string blockValue = string.IsNullOrEmpty(currentBlock) ? "Dirt" : currentBlock;
        string locationValue = currentLocation?.ToString();
        string peakValue = PeakLocation?.ToString();

        float clicks = 0f;
        float diamonds = 0f;
        if (pendingRuntimeStatApply && hasCachedGameplayStats)
        {
            clicks = cachedClicks;
            diamonds = cachedDiamonds;
        }
        else if (StatsManager.Ins != null)
        {
            clicks = StatsManager.Ins.Get(StatType.Clicks);
            diamonds = StatsManager.Ins.Get(StatType.Diamond);
            cachedClicks = clicks;
            cachedDiamonds = diamonds;
            hasCachedGameplayStats = true;
            pendingRuntimeStatApply = false;
        }
        else if (hasCachedGameplayStats)
        {
            clicks = cachedClicks;
            diamonds = cachedDiamonds;
        }

        float timeValue = CurrentTime;
        if (TimeSystem.Instance != null)
            timeValue = TimeSystem.Instance.CurrentTime.Value;

        SyncCraftNodeStateToCache(craftNodeManager);
        List<int> currentScopeStates = craftNodeManager != null ? craftNodeManager.GetStates() : null;

        return new GameplaySaveData
        {
            currentBlock = blockValue,
            currentLocation = locationValue,
            peakLocation = peakValue,
            clicks = clicks,
            diamonds = diamonds,
            currentTime = timeValue,
            totalPlaytime = TotalPlaytime,
            craftNodeStatesByBiome = BuildCraftNodeStatesByBiomePayload(),
            // Keep legacy field for backward compatibility/migration safety.
            craftNodeStates = currentScopeStates
        };
    }

    private UserProfileData BuildProfileSaveData()
    {
        return new UserProfileData
        {
            displayName = SanitizeDisplayName(DisplayName),
            avatarId = SanitizeAvatarId(AvatarId)
        };
    }

    private LeaderboardPublicData BuildLeaderboardPublicData(GameplaySaveData gameplay, UserProfileData profile, Timestamp updatedAt)
    {
        return new LeaderboardPublicData
        {
            displayName = SanitizeDisplayName(profile?.displayName),
            avatarId = SanitizeAvatarId(profile?.avatarId),
            clicks = gameplay != null ? gameplay.clicks : 0f,
            totalPlaytime = gameplay != null ? gameplay.totalPlaytime : 0f,
            updatedAt = updatedAt
        };
    }

    private Dictionary<string, InventorySaveData> BuildAllInventorySaveData()
    {
        var dict = new Dictionary<string, InventorySaveData>();
        foreach (var inv in inventoryDatas)
        {
            if (inv == null) continue;
            dict[inv.inventoryType.ToString()] = BuildInventorySaveData(inv);
        }
        return dict;
    }

    private InventorySaveData BuildInventorySaveData(InventoryData inv)
    {
        var saveData = new InventorySaveData { items = new List<InventoryItemSave>() };

        foreach (var invItem in inv.Items)
        {
            var item = invItem?.itemData != null ? invItem.itemData : inv.NullItem;
            saveData.items.Add(new InventoryItemSave
            {
                itemName = item != null ? item.name : "",
                quantity = invItem?.quantity?.Value ?? 0
            });
        }

        return saveData;
    }

    private void SaveLocalCache()
    {
        if (!allowSaves)
            return;

        try
        {
            var local = BuildLocalSaveData();
            string json = JsonUtility.ToJson(local, false);
            File.WriteAllText(GetLocalCachePath(), json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Warn] Failed to write local cache: {ex}");
        }
    }

    private string GetLocalCachePath()
    {
        return Path.Combine(Application.persistentDataPath, LocalCacheFileName);
    }

    private bool TryLoadLocalCache(out LocalSaveData data)
    {
        data = null;
        string path = GetLocalCachePath();
        if (!File.Exists(path)) return false;

        try
        {
            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<LocalSaveData>(json);
            return data != null;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Warn] Failed to read local cache: {ex}");
            return false;
        }
    }

    public IEnumerator LoadFromLocalCache(Action<bool> onComplete = null)
    {
        if (!TryLoadLocalCache(out var local) || local.gameplay == null)
        {
            Debug.LogWarning("[Warn] Local cache missing or invalid.");
            onComplete?.Invoke(false);
            yield break;
        }

        lastLocalUpdatedUtcTicks = local.savedAtUtcTicks;
        if (lastCloudUpdatedUtcTicks <= 0)
            lastCloudUpdatedUtcTicks = lastLocalUpdatedUtcTicks;
        ApplyGameplayData(local.gameplay.ToGameplaySaveData());
        if (local.profile != null)
            ApplyProfileData(local.profile.ToProfileSaveData());

        if (local.inventories != null)
        {
            foreach (var invSave in local.inventories)
            {
                var inv = FindInventory(invSave.inventoryType);
                if (inv == null) continue;
                yield return ApplyInventoryData(inv, invSave.ToInventorySaveData());
            }
        }

        DevLog.Log("[OK] Loaded from local cache.");
        MarkDataLoaded();
        onComplete?.Invoke(true);
    }

    private LocalSaveData BuildLocalSaveData()
    {
        if (lastLocalUpdatedUtcTicks <= 0)
            lastLocalUpdatedUtcTicks = DateTime.UtcNow.Ticks;

        var local = new LocalSaveData
        {
            savedAtUtcTicks = lastLocalUpdatedUtcTicks,
            gameplay = new LocalGameplayData(BuildGameplaySaveData()),
            profile = new LocalProfileData(BuildProfileSaveData())
        };

        foreach (var inv in inventoryDatas)
        {
            if (inv == null) continue;

            var invSave = new LocalInventorySave
            {
                inventoryType = inv.inventoryType.ToString()
            };

            foreach (var invItem in inv.Items)
            {
                var item = invItem?.itemData != null ? invItem.itemData : inv.NullItem;
                invSave.items.Add(new LocalInventoryItem
                {
                    itemName = item != null ? item.name : "",
                    quantity = invItem?.quantity?.Value ?? 0
                });
            }

            local.inventories.Add(invSave);
        }

        return local;
    }

    // ===== LOAD =====

    public IEnumerator LoadAllInventories(string uid, Action<bool> onComplete = null)
    {
        if (db == null || string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning("LoadAllInventories skipped: Firebase DB or uid is unavailable.");
            onComplete?.Invoke(false);
            yield break;
        }

        bool ok = true;
        foreach (var inv in inventoryDatas)
        {
            bool invOk = false;
            yield return StartCoroutine(LoadOneInventory(uid, inv, success => invOk = success));
            if (!invOk) ok = false;
        }

        if (ok)
            MarkDataLoaded();
        if (ok) SaveLocalCache();
        DevLog.Log("[OK] All inventories loaded (Firestore)");
        onComplete?.Invoke(ok);
    }

    private IEnumerator LoadOneInventory(string uid, InventoryData inv, Action<bool> onComplete)
    {
        if (inv == null)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        var task = FirebaseTaskTracker.Track(
            db.Collection("users").Document(uid)
                .Collection("inventories").Document(inv.inventoryType.ToString())
                .GetSnapshotAsync());

        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            Debug.LogError($"[Error] Failed to load {inv.inventoryType}: {task.Exception}");
            onComplete?.Invoke(false);
            yield break;
        }

        var snap = task.Result;

        // No data -> default size
        if (!snap.Exists)
        {
            Debug.LogWarning($"[Warn] No data for {inv.inventoryType}, create default size = {inv.GetSize()}");
            inv.Items.Clear();
            EnsureInventorySize(inv);
            onComplete?.Invoke(true);
            yield break;
        }

        var loadedData = snap.ConvertTo<InventorySaveData>() ?? new InventorySaveData();
        yield return ApplyInventoryData(inv, loadedData);
        onComplete?.Invoke(true);
    }

    public IEnumerator LoadGameplay(string uid, Action<bool> onComplete = null)
    {
        if (db == null || string.IsNullOrEmpty(uid))
        {
            Debug.LogWarning("LoadGameplay skipped: Firebase DB or uid is unavailable.");
            onComplete?.Invoke(false);
            yield break;
        }

        var task = FirebaseTaskTracker.Track(db.Collection("users").Document(uid).GetSnapshotAsync());
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            Debug.LogError($"[Error] Failed to load gameplay doc: {task.Exception}");
            onComplete?.Invoke(false);
            yield break;
        }

        var snap = task.Result;
        if (!snap.Exists)
        {
            Debug.LogWarning("[Warn] No user doc, keep defaults.");
            MarkDataLoaded();
            onComplete?.Invoke(true);
            yield break;
        }

        if (TryGetCloudUpdatedTicks(snap, out var cloudTicks))
        {
            lastCloudUpdatedUtcTicks = cloudTicks;
            lastLocalUpdatedUtcTicks = cloudTicks;
        }

        if (!snap.TryGetValue("gameplay", out GameplaySaveData gameplay) || gameplay == null)
        {
            Debug.LogWarning("[Warn] No gameplay field.");
            onComplete?.Invoke(false);
            yield break;
        }

        ApplyGameplayData(gameplay);
        if (snap.TryGetValue("profile", out UserProfileData profile) && profile != null)
            ApplyProfileData(profile);
        DevLog.Log("[OK] Loaded gameplay (Firestore)");
        MarkDataLoaded();
        onComplete?.Invoke(true);
    }

    private void ApplyGameplayData(GameplaySaveData gameplay)
    {
        if (gameplay == null) return;
        currentBlock = string.IsNullOrEmpty(gameplay.currentBlock) ? "Dirt" : gameplay.currentBlock;

        if (Enum.TryParse(gameplay.currentLocation, out BlockSpawnLocation loc))
            currentLocation = loc;
        else
            currentLocation = null;

        if (Enum.TryParse(gameplay.peakLocation, out BlockSpawnLocation peak))
            PeakLocation = peak;
        else
            PeakLocation = null;

        CacheGameplayStats(gameplay.clicks, gameplay.diamonds);
        TryApplyCachedGameplayStatsToRuntime();

        CurrentTime = gameplay.currentTime;
        TotalPlaytime = Mathf.Max(0f, gameplay.totalPlaytime);
        AnalyticsManager.Ins?.UpdateUserProperties(this);
        LoadCraftNodeStates(gameplay);
    }

    private void CacheGameplayStats(float clicks, float diamonds)
    {
        cachedClicks = clicks;
        cachedDiamonds = diamonds;
        hasCachedGameplayStats = true;
        pendingRuntimeStatApply = true;
    }

    private void TryApplyCachedGameplayStatsToRuntime()
    {
        if (!pendingRuntimeStatApply || !hasCachedGameplayStats)
            return;

        if (StatsManager.Ins == null)
            return;
        if (PlayerController.Instance == null)
            return;

        StatsManager.Ins.Set(StatType.Clicks, cachedClicks);
        StatsManager.Ins.Set(StatType.Diamond, cachedDiamonds);
        pendingRuntimeStatApply = false;
    }

    private void ApplyProfileData(UserProfileData profile)
    {
        if (profile == null) return;
        DisplayName = SanitizeDisplayName(profile.displayName);
        AvatarId = SanitizeAvatarId(profile.avatarId);
    }

    public void RegisterCraftNodeManager(CraftNodeManager manager)
    {
        if (manager == null) return;

        if (craftNodeManager != null && craftNodeManager != manager)
            SyncCraftNodeStateToCache(craftNodeManager);

        craftNodeManager = manager;
        TryApplyCraftNodeStates();
    }

    private void TryApplyCraftNodeStates()
    {
        if (craftNodeManager == null)
            return;

        string scope = GetCraftScope(craftNodeManager);
        if (craftNodeStatesByBiomeCache.TryGetValue(scope, out var scopedStates) &&
            scopedStates != null &&
            scopedStates.Count > 0)
        {
            craftNodeManager.ApplyStates(scopedStates, saveLocal: true);
            return;
        }

        if (pendingLegacyCraftNodeStates == null || pendingLegacyCraftNodeStates.Count == 0)
        {
            // No cloud state for this scope yet: seed cache from current local tree state.
            SyncCraftNodeStateToCache(craftNodeManager);
            return;
        }

        craftNodeManager.ApplyStates(pendingLegacyCraftNodeStates, saveLocal: true);
        craftNodeStatesByBiomeCache[scope] = new List<int>(pendingLegacyCraftNodeStates);
        pendingLegacyCraftNodeStates = null;
    }

    private void LoadCraftNodeStates(GameplaySaveData gameplay)
    {
        craftNodeStatesByBiomeCache.Clear();
        pendingLegacyCraftNodeStates = null;

        if (gameplay.craftNodeStatesByBiome != null)
        {
            foreach (var scopedState in gameplay.craftNodeStatesByBiome)
            {
                if (scopedState == null || scopedState.states == null)
                    continue;

                string scope = NormalizeCraftScope(scopedState.biome);
                craftNodeStatesByBiomeCache[scope] = new List<int>(scopedState.states);
            }
        }

        if (craftNodeStatesByBiomeCache.Count == 0 &&
            gameplay.craftNodeStates != null &&
            gameplay.craftNodeStates.Count > 0)
        {
            pendingLegacyCraftNodeStates = new List<int>(gameplay.craftNodeStates);
        }

        TryApplyCraftNodeStates();
    }

    private List<BiomeCraftNodeState> BuildCraftNodeStatesByBiomePayload()
    {
        if (craftNodeStatesByBiomeCache.Count == 0)
            return null;

        var result = new List<BiomeCraftNodeState>(craftNodeStatesByBiomeCache.Count);
        foreach (var entry in craftNodeStatesByBiomeCache)
        {
            result.Add(new BiomeCraftNodeState
            {
                biome = entry.Key,
                states = entry.Value != null ? new List<int>(entry.Value) : null
            });
        }

        return result;
    }

    private void SyncCraftNodeStateToCache(CraftNodeManager manager)
    {
        if (manager == null)
            return;

        string scope = GetCraftScope(manager);
        var states = manager.GetStates();
        if (states == null)
            return;

        craftNodeStatesByBiomeCache[scope] = new List<int>(states);
    }

    private string GetCraftScope(CraftNodeManager manager)
    {
        if (manager == null)
            return DefaultCraftScope;
        return NormalizeCraftScope(manager.CurrentSaveScope);
    }

    private string NormalizeCraftScope(string scope)
    {
        return string.IsNullOrWhiteSpace(scope) ? DefaultCraftScope : scope.Trim();
    }

    public void SetDisplayName(string displayName, bool forceSave = true)
    {
        string cleaned = SanitizeDisplayName(displayName);
        if (string.Equals(DisplayName, cleaned, StringComparison.Ordinal))
            return;

        DisplayName = cleaned;
        SaveDataFn(forceSave);
    }

    public void SetAvatarId(string avatarId, bool forceSave = true)
    {
        string cleaned = SanitizeAvatarId(avatarId);
        if (string.Equals(AvatarId, cleaned, StringComparison.Ordinal))
            return;

        AvatarId = cleaned;
        SaveDataFn(forceSave);
    }

    public void MarkInitialLoadComplete(bool hasData)
    {
        allowSaves = true;
        if (hasData)
            MarkDataLoaded();

        if (pendingSave && (ongoingSave == null || ongoingSave.IsCompleted))
        {
            bool force = pendingForce;
            pendingSave = false;
            pendingForce = false;
            SaveDataFn(force);
        }
    }

    private void MarkDataLoaded()
    {
        hasLoadedData = true;
        allowCloudSave = true;
        playtimeActive = true;
    }

    private void TouchLocalDataUpdated()
    {
        if (!hasLoadedData)
            return;
        lastLocalUpdatedUtcTicks = DateTime.UtcNow.Ticks;
    }

    private bool TryGetCloudUpdatedTicks(DocumentSnapshot snap, out long utcTicks)
    {
        utcTicks = 0;
        if (snap == null) return false;

        if (snap.TryGetValue("meta.updatedAt", out Timestamp updatedAt))
        {
            utcTicks = updatedAt.ToDateTime().ToUniversalTime().Ticks;
            return utcTicks > 0;
        }

        return false;
    }

    private IEnumerator ApplyInventoryData(InventoryData inv, InventorySaveData loadedData)
    {
        if (loadedData == null)
            loadedData = new InventorySaveData();
        if (loadedData.items == null)
            loadedData.items = new List<InventoryItemSave>();

        inv.Items.Clear();

        foreach (var data in loadedData.items)
        {
            if (string.IsNullOrEmpty(data.itemName))
            {
                inv.Items.Add(new InventoryItem(inv.NullItem, 0));
                continue;
            }

            var handle = Addressables.LoadAssetAsync<Item>(data.itemName);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                inv.Items.Add(new InventoryItem(handle.Result, data.quantity));
            else
            {
                Debug.LogWarning($"[Addressables] Failed to load item: {data.itemName} -> empty slot");
                inv.Items.Add(new InventoryItem(inv.NullItem, 0));
            }
        }

        bool migrated = EnsureInventorySize(inv);
        if (migrated)
        {
            DevLog.Log($"[OK] Migrated {inv.inventoryType} to size {inv.GetSize()}");
            SaveLocalCache();
            if (isReady)
                SaveDataFn(true);
        }
    }

    private InventoryData FindInventory(string typeName)
    {
        foreach (var inv in inventoryDatas)
        {
            if (inv != null && inv.inventoryType.ToString() == typeName)
                return inv;
        }
        return null;
    }

    private bool EnsureInventorySize(InventoryData inv)
    {
        int target = inv.GetSize();
        bool changed = false;

        while (inv.Items.Count < target)
        {
            inv.Items.Add(new InventoryItem(inv.NullItem, 0));
            changed = true;
        }

        while (inv.Items.Count > target)
        {
            inv.Items.RemoveAt(inv.Items.Count - 1);
            changed = true;
        }

        return changed;
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            playtimeActive = false;
        else if (hasLoadedData)
            playtimeActive = true;
        if (isQuitting) return;
        if (paused)
            SaveDataFn(forceCloudSyncOnLifecycle, forceLocalWrite: true);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            playtimeActive = false;
        else if (hasLoadedData)
            playtimeActive = true;
        if (isQuitting) return;
        if (!hasFocus)
            SaveDataFn(forceCloudSyncOnLifecycle, forceLocalWrite: true);
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
        playtimeActive = false;
        if (!allowSaves)
            return;

        SaveDataFn(forceCloudSyncOnLifecycle, forceLocalWrite: true);
    }

    private string SanitizeDisplayName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string trimmed = raw.Trim();
        if (trimmed.Length > MaxDisplayNameLength)
            trimmed = trimmed.Substring(0, MaxDisplayNameLength);

        var sb = new StringBuilder(trimmed.Length);
        foreach (char c in trimmed)
        {
            if (!char.IsControl(c))
                sb.Append(c);
        }

        return sb.ToString();
    }

    private string SanitizeAvatarId(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string trimmed = raw.Trim();
        if (trimmed.Length > MaxAvatarIdLength)
            trimmed = trimmed.Substring(0, MaxAvatarIdLength);

        var sb = new StringBuilder(trimmed.Length);
        foreach (char c in trimmed)
        {
            if (!char.IsControl(c))
                sb.Append(c);
        }

        return sb.ToString();
    }
}

