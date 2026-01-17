using UnityEngine;
using Firebase.Firestore;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System;
using System.IO;
using System.Threading.Tasks;
using UniRx;

public class DataSaver : MonoBehaviour
{
    public static DataSaver Ins { get; private set; }

    [Header("Gameplay")]
    public string currentBlock;
    public BlockSpawnLocation? currentLocation;
    public BlockSpawnLocation? PeakLocation;
    public float CurrentTime;

    [Header("Autosave")]
    private IntReactiveProperty blockBreakCounter = new IntReactiveProperty(0);
    private const int SaveThreshold = 10;

    [SerializeField] private List<InventoryData> inventoryDatas = new List<InventoryData>();

    // Firestore
    private FirebaseFirestore db;

    // Throttle chống spam write (clicker)
    [SerializeField] private float cloudSaveCooldown = 15f;
    private float nextCloudSaveTime = 0f;

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
    private const string LocalCacheFileName = "local_save.json";

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
        // ✅ Delay init until FirebaseBootstrap is ready
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
            Debug.LogError($"❌ DataSaver init aborted: {FirebaseBootstrap.Ins.InitError}");
            yield break;
        }

        db = FirebaseBootstrap.Ins.Db;

        // Subscribe once, only when ready
        blockBreakCounter
            .Where(count => count >= SaveThreshold)
            .Subscribe(_ => SaveDataFn())
            .AddTo(this);

        isReady = true;
        Debug.Log($"✅ DataSaver ready. uid={GetUid()}");
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

        uid = GetUid();
        if (string.IsNullOrEmpty(uid)) { reason = "uid missing"; return false; }

        // cooldown để không spam write
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

    // Bạn gọi cái này mỗi khi break block/click... tuỳ logic
    public void IncreaseBreakCounter(int amount = 1)
    {
        blockBreakCounter.Value += amount;
    }

    public void SaveDataFn(bool force = false)
    {
        if (verboseSaveLogs)
            Debug.Log($"SaveDataFn called (force={force}).");

        SaveLocalCache();

        // reset counter (nhưng chỉ reset khi thật sự save)
        if (!CanCloudSaveNow(out var uid, force, out var reason))
        {
            if (verboseSaveLogs)
                Debug.Log($"SaveDataFn blocked: {reason ?? "unknown"}");
            return;
        }

        blockBreakCounter.Value = 0;

        if (ongoingSave != null && !ongoingSave.IsCompleted)
        {
            pendingSave = true;
            pendingForce |= force;
            if (verboseSaveLogs)
                Debug.Log("SaveDataFn skipped: previous save still running.");
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
        var inventories = BuildAllInventorySaveData();

        if (verboseSaveLogs)
            Debug.Log($"SaveCloudAsync start (uid={uid}).");

        await ExecuteWithRetry(async () =>
        {
            var batch = db.StartBatch();
            var userDoc = db.Collection("users").Document(uid);

            var payload = new Dictionary<string, object>
            {
                ["gameplay"] = gameplay,
                ["meta.updatedAt"] = Timestamp.GetCurrentTimestamp(),
                ["meta.rev"] = FieldValue.Increment(1),
                ["meta.saveVersion"] = 1
            };

            batch.Set(userDoc, payload, SetOptions.MergeAll);

            foreach (var entry in inventories)
            {
                var invDoc = userDoc.Collection("inventories").Document(entry.Key);
                batch.Set(invDoc, entry.Value);
            }

            await AwaitWithTimeout(batch.CommitAsync(), cloudCommitTimeoutSeconds, "Firestore commit");
        }, maxSaveAttempts, retryBaseDelaySeconds, "SaveCloudAsync");

        if (verboseSaveLogs)
            Debug.Log("SaveCloudAsync completed.");
    }

    private async Task ExecuteWithRetry(Func<Task> action, int maxAttempts, float baseDelaySeconds, string opName)
    {
        int attempts = Mathf.Max(1, maxAttempts);
        float delay = Mathf.Max(0.05f, baseDelaySeconds);

        for (int i = 1; i <= attempts; i++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex)
            {
                if (i >= attempts)
                {
                    Debug.LogError($"❌ {opName} failed after {attempts} attempts: {ex}");
                    return;
                }

                if (verboseSaveLogs)
                    Debug.LogWarning($"{opName} attempt {i} failed: {ex.Message}");

                float wait = delay * Mathf.Pow(2f, i - 1);
                await Task.Delay(TimeSpan.FromSeconds(wait));
            }
        }
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
        string blockValue = string.IsNullOrEmpty(currentBlock) ? "Dirt" : currentBlock;
        string locationValue = currentLocation?.ToString();
        string peakValue = PeakLocation?.ToString();

        float clicks = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.Clicks) : 0f;
        float diamonds = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.Diamond) : 0f;

        float timeValue = CurrentTime;
        if (TimeSystem.Instance != null)
            timeValue = TimeSystem.Instance.CurrentTime.Value;

        return new GameplaySaveData
        {
            currentBlock = blockValue,
            currentLocation = locationValue,
            peakLocation = peakValue,
            clicks = clicks,
            diamonds = diamonds,
            currentTime = timeValue
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
        try
        {
            var local = BuildLocalSaveData();
            string json = JsonUtility.ToJson(local, false);
            File.WriteAllText(GetLocalCachePath(), json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"⚠️ Failed to write local cache: {ex}");
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
            Debug.LogWarning($"⚠️ Failed to read local cache: {ex}");
            return false;
        }
    }

    public IEnumerator LoadFromLocalCache(Action<bool> onComplete = null)
    {
        if (!TryLoadLocalCache(out var local) || local.gameplay == null)
        {
            Debug.LogWarning("⚠️ Local cache missing or invalid.");
            onComplete?.Invoke(false);
            yield break;
        }

        ApplyGameplayData(local.gameplay.ToGameplaySaveData());

        if (local.inventories != null)
        {
            foreach (var invSave in local.inventories)
            {
                var inv = FindInventory(invSave.inventoryType);
                if (inv == null) continue;
                yield return ApplyInventoryData(inv, invSave.ToInventorySaveData());
            }
        }

        Debug.Log("✅ Loaded from local cache.");
        onComplete?.Invoke(true);
    }

    private LocalSaveData BuildLocalSaveData()
    {
        var local = new LocalSaveData
        {
            savedAtUtcTicks = DateTime.UtcNow.Ticks,
            gameplay = new LocalGameplayData(BuildGameplaySaveData())
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
        bool ok = true;
        foreach (var inv in inventoryDatas)
        {
            bool invOk = false;
            yield return StartCoroutine(LoadOneInventory(uid, inv, success => invOk = success));
            if (!invOk) ok = false;
        }

        if (ok) SaveLocalCache();
        Debug.Log("✅ All inventories loaded (Firestore)");
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
            Debug.LogError($"❌ Failed to load {inv.inventoryType}: {task.Exception}");
            onComplete?.Invoke(false);
            yield break;
        }

        var snap = task.Result;

        // No data -> default size
        if (!snap.Exists)
        {
            Debug.LogWarning($"⚠️ No data for {inv.inventoryType}, create default size = {inv.GetSize()}");
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
        // Guard: db not ready
        yield return new WaitUntil(() => db != null);

        var task = FirebaseTaskTracker.Track(db.Collection("users").Document(uid).GetSnapshotAsync());
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            Debug.LogError($"❌ Failed to load gameplay doc: {task.Exception}");
            onComplete?.Invoke(false);
            yield break;
        }

        var snap = task.Result;
        if (!snap.Exists)
        {
            Debug.LogWarning("⚠️ No user doc, keep defaults.");
            onComplete?.Invoke(false);
            yield break;
        }

        if (!snap.TryGetValue("gameplay", out GameplaySaveData gameplay) || gameplay == null)
        {
            Debug.LogWarning("⚠️ No gameplay field.");
            onComplete?.Invoke(false);
            yield break;
        }

        ApplyGameplayData(gameplay);
        Debug.Log("✅ Loaded gameplay (Firestore)");
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

        if (StatsManager.Ins != null)
        {
            StatsManager.Ins.Set(StatType.Clicks, gameplay.clicks);
            StatsManager.Ins.Set(StatType.Diamond, gameplay.diamonds);
        }

        CurrentTime = gameplay.currentTime;
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
            Debug.Log($"✅ Migrated {inv.inventoryType} to size {inv.GetSize()}");
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
        if (isQuitting) return;
        if (paused && isReady && db != null && !string.IsNullOrEmpty(GetUid()))
            SaveDataFn();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (isQuitting) return;
        if (!hasFocus && isReady && db != null && !string.IsNullOrEmpty(GetUid()))
            SaveDataFn();
    }

    void OnApplicationQuit()
    {
        isQuitting = true;
    }
}
