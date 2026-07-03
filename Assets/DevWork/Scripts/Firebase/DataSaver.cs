using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System;
using System.Text;
using UniRx;

public class DataSaver : MonoBehaviour
{
    // Allowed global owner: persisted player save data and craft state by biome.
    public static DataSaver Ins { get; private set; }
    public bool IsReady => isReady;
    public bool HasLoadedData => hasLoadedData;

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
    private readonly Dictionary<string, int> biomeEssenceEarnedByBiome = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> biomeProgressClaimedLevelByBiome = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private List<int> pendingLegacyCraftNodeStates;

    [Header("Local Save")]
    [SerializeField, Min(0.1f)] private float localSaveCooldown = 2f;

    private float nextLocalSaveTime = 0f;
    private bool pendingLocalSave;

    [Header("Debug")]
    [SerializeField] private bool verboseSaveLogs = false;

    // Ready flag
    private bool isReady = false;
    private bool isQuitting = false;
    private bool allowSaves;
    private bool hasLoadedData;
    private bool playtimeActive;
    // Runtime owner is StatsManager. DataSaver only caches and persists the lifetime snapshot.
    private readonly LifetimeStats cachedLifetimeStats = new LifetimeStats();
    private readonly PlayerProfileRepository playerProfileRepository = new PlayerProfileRepository();
    private bool hasCachedLifetimeStats;
    private bool pendingStatApply;
    private long lastLocalUpdatedUtcTicks;
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
        StartCoroutine(InitLocalSave());
    }

    private IEnumerator InitLocalSave()
    {
        bool loadedLocal = false;
        yield return LoadFromLocalCache(ok => loadedLocal = ok);
        EnsureDefaultGameplayData();
        if (!loadedLocal)
            MarkDataLoaded();

        blockBreakCounter
            .Where(count => count >= SaveThreshold)
            .Subscribe(_ => SaveDataFn())
            .AddTo(this);

        allowSaves = true;
        isReady = true;
        if (!loadedLocal)
            QueueLocalSave(forceImmediate: true);

        DevLog.Log($"[OK] DataSaver ready (local). loadedLocal={loadedLocal}");
        AnalyticsManager.Ins?.UpdateUserProperties(this);
    }

    // Call this on break block/click depending on your game flow.
    public void IncreaseBreakCounter(int amount = 1)
    {
        blockBreakCounter.Value += amount;
    }

    public int GetBiomeEssenceEarned(BlockSpawnLocation biome)
    {
        string key = NormalizeBiomeProgressKey(biome);
        return biomeEssenceEarnedByBiome.TryGetValue(key, out int amount)
            ? Mathf.Max(0, amount)
            : 0;
    }

    public void AddBiomeEssenceEarned(BlockSpawnLocation biome, int amount, bool queueSave = true)
    {
        if (amount <= 0)
            return;

        string key = NormalizeBiomeProgressKey(biome);
        int current = biomeEssenceEarnedByBiome.TryGetValue(key, out int existing) ? Mathf.Max(0, existing) : 0;
        biomeEssenceEarnedByBiome[key] = current + amount;

        if (queueSave)
            SaveDataFn();
    }

    public int GetBiomeProgressClaimedLevel(BlockSpawnLocation biome)
    {
        string key = NormalizeBiomeProgressKey(biome);
        return biomeProgressClaimedLevelByBiome.TryGetValue(key, out int level)
            ? Mathf.Max(-1, level)
            : -1;
    }

    public void SetBiomeProgressClaimedLevel(BlockSpawnLocation biome, int claimedLevel, bool queueSave = true)
    {
        string key = NormalizeBiomeProgressKey(biome);
        int safeLevel = Mathf.Max(-1, claimedLevel);
        if (biomeProgressClaimedLevelByBiome.TryGetValue(key, out int existing) && existing == safeLevel)
            return;

        biomeProgressClaimedLevelByBiome[key] = safeLevel;

        if (queueSave)
            SaveDataFn();
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

        TryApplyCachedStats();
        QueueLocalSave(forceLocalWrite || force);

        blockBreakCounter.Value = 0;
    }

    [ContextMenu("Debug/Force Save Now")]
    private void DebugForceSaveNow()
    {
        SaveDataFn(true);
    }

    void Update()
    {
        TryApplyCachedStats();

        if (playtimeActive)
            TotalPlaytime += Time.unscaledDeltaTime;

        if (pendingLocalSave && Time.unscaledTime >= nextLocalSaveTime)
            QueueLocalSave(forceImmediate: true);
    }

    private GameplaySaveData BuildGameplaySaveData()
    {
        TryApplyCachedStats();
        EnsureDefaultGameplayData();

        string blockValue = string.IsNullOrEmpty(currentBlock) ? "Dirt" : currentBlock;
        string locationValue = currentLocation?.ToString();
        string peakValue = PeakLocation?.ToString();

        LifetimeStats lifetimeStats = ResolveLifetimeStatsForSave();

        float timeValue = CurrentTime;
        if (TimeSystem.Instance != null)
            timeValue = TimeSystem.Instance.CurrentTime.Value;

        SyncCraftNodeStateToCache(craftNodeManager);
        List<int> currentScopeStates = craftNodeManager != null ? craftNodeManager.GetStates() : null;

        var gameplay = new GameplaySaveData
        {
            currentBlock = blockValue,
            currentLocation = locationValue,
            peakLocation = peakValue,
            currentTime = timeValue
        };
        LifetimeStatsSection.Write(gameplay, lifetimeStats);
        CraftSection.Write(gameplay, BuildCraftNodeStatesByBiomePayload(), currentScopeStates);
        BiomeProgressSection.Write(gameplay, BuildBiomeEssenceEarnedPayload(), BuildBiomeProgressClaimPayload());
        return gameplay;
    }

    private UserProfileData BuildProfileSaveData()
    {
        return new UserProfileData
        {
            displayName = SanitizeDisplayName(DisplayName),
            avatarId = SanitizeAvatarId(AvatarId)
        };
    }

    private void SaveLocalCache()
    {
        if (!allowSaves)
            return;

        if (!playerProfileRepository.Save(BuildLocalSaveData()))
            Debug.LogWarning("[Warn] Failed to write local cache.");
    }

    public IEnumerator LoadFromLocalCache(Action<bool> onComplete = null)
    {
        if (!playerProfileRepository.TryLoad(out var local) || local.gameplay == null)
        {
            if (playerProfileRepository.Exists())
                Debug.LogWarning("[Warn] Local cache invalid.");
            else if (verboseSaveLogs)
                DevLog.Log("Local cache missing. Starting with default data.");

            onComplete?.Invoke(false);
            yield break;
        }

        lastLocalUpdatedUtcTicks = local.savedAtUtcTicks;
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

        EnsureDefaultGameplayData();
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
            profile = new LocalProfileData(BuildProfileSaveData()),
            inventories = InventorySaveSection.Build(inventoryDatas)
        };

        return local;
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

        CurrentTime = gameplay.currentTime;
        LifetimeStats lifetimeStats = LifetimeStatsSection.Read(gameplay);
        TotalPlaytime = lifetimeStats.totalTimePlayed;
        CacheLifetimeStats(
            lifetimeStats.clicks,
            lifetimeStats.diamonds,
            lifetimeStats.totalBlockBreaked,
            lifetimeStats.totalDamageDealed,
            lifetimeStats.totalTimePlayed);
        TryApplyCachedStats();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DevLog.Log($"[SaveLoad] Loaded gameplay block='{currentBlock}', location='{currentLocation}', time={CurrentTime}.");
#endif
        AnalyticsManager.Ins?.UpdateUserProperties(this);
        LoadCraftNodeStates(gameplay);
        LoadBiomeProgressData(gameplay);
    }

    private void EnsureDefaultGameplayData()
    {
        if (string.IsNullOrEmpty(currentBlock))
            currentBlock = "Dirt";

        if (!currentLocation.HasValue || currentLocation.Value == BlockSpawnLocation.Any)
            currentLocation = BlockSpawnLocation.Plain;

        if (!PeakLocation.HasValue || PeakLocation.Value == BlockSpawnLocation.Any || PeakLocation.Value < BlockSpawnLocation.Plain)
            PeakLocation = BlockSpawnLocation.Plain;

        if (currentLocation.HasValue && PeakLocation.Value < currentLocation.Value)
            PeakLocation = currentLocation.Value;
    }

    private void CacheLifetimeStats(
        float clicks,
        float diamonds,
        float totalBlockBreaked,
        float totalDamageDealed,
        float totalTimePlayed)
    {
        cachedLifetimeStats.Set(
            clicks,
            diamonds,
            totalBlockBreaked,
            totalDamageDealed,
            totalTimePlayed);
        hasCachedLifetimeStats = true;
        pendingStatApply = true;
    }

    private void TryApplyCachedStats()
    {
        if (!pendingStatApply || !hasCachedLifetimeStats)
            return;

        if (StatsManager.Ins == null)
            return;
        if (PlayerController.Instance == null)
            return;

        cachedLifetimeStats.ApplyToRuntime(StatsManager.Ins, TotalPlaytime);
        TotalPlaytime = cachedLifetimeStats.totalTimePlayed;
        pendingStatApply = false;
    }

    private void ApplyProfileData(UserProfileData profile)
    {
        if (profile == null) return;
        DisplayName = SanitizeDisplayName(profile.displayName);
        AvatarId = SanitizeAvatarId(profile.avatarId);
    }

    public void BindCraftNodeManager(CraftNodeManager manager)
    {
        if (manager == null) return;

        if (craftNodeManager != null && craftNodeManager != manager)
            SyncCraftNodeStateToCache(craftNodeManager);

        craftNodeManager = manager;
        ApplyCraftNodeStates();
    }

    private void ApplyCraftNodeStates()
    {
        if (craftNodeManager == null)
            return;

        string scope = GetCraftScope(craftNodeManager);
        if (craftNodeStatesByBiomeCache.TryGetValue(scope, out var scopedStates) &&
            scopedStates != null &&
            scopedStates.Count > 0)
        {
            craftNodeManager.ApplyStates(scopedStates, saveLocal: true);
            craftNodeManager.DeleteLegacyPlayerPrefsStates();
            return;
        }

        if (pendingLegacyCraftNodeStates == null || pendingLegacyCraftNodeStates.Count == 0)
        {
            if (craftNodeManager.TryLoadLegacyPlayerPrefsStates(out var legacyStates) &&
                legacyStates != null &&
                legacyStates.Count > 0)
            {
                craftNodeManager.ApplyStates(legacyStates, saveLocal: false);
                craftNodeStatesByBiomeCache[scope] = new List<int>(legacyStates);
                craftNodeManager.DeleteLegacyPlayerPrefsStates();
                SaveDataFn(force: true, forceLocalWrite: true);
                return;
            }

            // New scopes must start from a clean tree, not whatever biome was bound before.
            craftNodeManager.ResetStates(saveLocal: false);
            SyncCraftNodeStateToCache(craftNodeManager);
            return;
        }

        craftNodeManager.ApplyStates(pendingLegacyCraftNodeStates, saveLocal: true);
        craftNodeStatesByBiomeCache[scope] = new List<int>(pendingLegacyCraftNodeStates);
        pendingLegacyCraftNodeStates = null;
        craftNodeManager.DeleteLegacyPlayerPrefsStates();
    }

    private void LoadCraftNodeStates(GameplaySaveData gameplay)
    {
        craftNodeStatesByBiomeCache.Clear();
        pendingLegacyCraftNodeStates = null;

        var scopedStates = CraftSection.ReadScopedStates(gameplay);
        if (scopedStates != null)
        {
            foreach (var scopedState in scopedStates)
            {
                if (scopedState == null || scopedState.states == null)
                    continue;

                string scope = NormalizeCraftScope(scopedState.biome);
                craftNodeStatesByBiomeCache[scope] = new List<int>(scopedState.states);
            }
        }

        var legacyScopeStates = CraftSection.ReadLegacyScopeStates(gameplay);
        if (craftNodeStatesByBiomeCache.Count == 0 &&
            legacyScopeStates != null &&
            legacyScopeStates.Count > 0)
        {
            pendingLegacyCraftNodeStates = new List<int>(legacyScopeStates);
        }

        ApplyCraftNodeStates();
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

    private List<BiomeEssenceEarnedState> BuildBiomeEssenceEarnedPayload()
    {
        if (biomeEssenceEarnedByBiome.Count == 0)
            return null;

        var result = new List<BiomeEssenceEarnedState>(biomeEssenceEarnedByBiome.Count);
        foreach (var entry in biomeEssenceEarnedByBiome)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value <= 0)
                continue;

            result.Add(new BiomeEssenceEarnedState
            {
                biome = entry.Key,
                amount = Mathf.Max(0, entry.Value)
            });
        }

        return result;
    }

    private List<BiomeProgressClaimState> BuildBiomeProgressClaimPayload()
    {
        if (biomeProgressClaimedLevelByBiome.Count == 0)
            return null;

        var result = new List<BiomeProgressClaimState>(biomeProgressClaimedLevelByBiome.Count);
        foreach (var entry in biomeProgressClaimedLevelByBiome)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 0)
                continue;

            result.Add(new BiomeProgressClaimState
            {
                biome = entry.Key,
                claimedLevel = Mathf.Max(-1, entry.Value)
            });
        }

        return result;
    }

    private void LoadBiomeProgressData(GameplaySaveData gameplay)
    {
        biomeEssenceEarnedByBiome.Clear();
        biomeProgressClaimedLevelByBiome.Clear();

        if (gameplay == null)
            return;

        var essenceStates = BiomeProgressSection.ReadEssence(gameplay);
        if (essenceStates != null)
        {
            foreach (var state in essenceStates)
            {
                if (state == null)
                    continue;

                string key = NormalizeBiomeProgressKey(state.biome);
                biomeEssenceEarnedByBiome[key] = Mathf.Max(0, state.amount);
            }
        }

        var claimStates = BiomeProgressSection.ReadClaims(gameplay);
        if (claimStates != null)
        {
            foreach (var state in claimStates)
            {
                if (state == null)
                    continue;

                string key = NormalizeBiomeProgressKey(state.biome);
                biomeProgressClaimedLevelByBiome[key] = Mathf.Max(-1, state.claimedLevel);
            }
        }
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

    private LifetimeStats ResolveLifetimeStatsForSave()
    {
        float totalTimePlayed = Mathf.Max(0f, TotalPlaytime);

        if (pendingStatApply && hasCachedLifetimeStats)
        {
            cachedLifetimeStats.ClampPlaytime(totalTimePlayed);
        }
        else if (StatsManager.Ins != null)
        {
            cachedLifetimeStats.SyncFromRuntime(StatsManager.Ins, totalTimePlayed);
            hasCachedLifetimeStats = true;
            pendingStatApply = false;
        }
        else if (hasCachedLifetimeStats)
        {
            cachedLifetimeStats.ClampPlaytime(totalTimePlayed);
        }
        else
        {
            cachedLifetimeStats.Set(0f, 0f, 0f, 0f, totalTimePlayed);
            hasCachedLifetimeStats = true;
        }

        TotalPlaytime = cachedLifetimeStats.totalTimePlayed;
        return cachedLifetimeStats;
    }

    private static string NormalizeBiomeProgressKey(BlockSpawnLocation biome)
    {
        return biome.ToString();
    }

    private static string NormalizeBiomeProgressKey(string biome)
    {
        return string.IsNullOrWhiteSpace(biome)
            ? BlockSpawnLocation.Plain.ToString()
            : biome.Trim();
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
    }

    private void MarkDataLoaded()
    {
        hasLoadedData = true;
        playtimeActive = true;
    }

    private void TouchLocalDataUpdated()
    {
        if (!hasLoadedData)
            return;
        lastLocalUpdatedUtcTicks = DateTime.UtcNow.Ticks;
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
            SaveDataFn(force: true, forceLocalWrite: true);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            playtimeActive = false;
        else if (hasLoadedData)
            playtimeActive = true;
        if (isQuitting) return;
        if (!hasFocus)
            SaveDataFn(force: true, forceLocalWrite: true);
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
        playtimeActive = false;
        if (!allowSaves)
            return;

        SaveDataFn(force: true, forceLocalWrite: true);
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

