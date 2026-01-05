using UnityEngine;
using Firebase.Firestore;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System;
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

    // Ready flag
    private bool isReady = false;

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

        // Wait Auth + CurrentUser + Db ready
        yield return new WaitUntil(() =>
            FirebaseBootstrap.Ins.Auth != null &&
            FirebaseBootstrap.Ins.Auth.CurrentUser != null &&
            FirebaseBootstrap.Ins.Db != null &&
            !string.IsNullOrEmpty(FirebaseBootstrap.Ins.Auth.CurrentUser.UserId)
        );

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

    private bool CanCloudSaveNow(out string uid)
    {
        uid = null;

        if (!isReady) return false;
        if (db == null) return false;

        uid = GetUid();
        if (string.IsNullOrEmpty(uid)) return false;

        // cooldown để không spam write
        if (Time.unscaledTime < nextCloudSaveTime) return false;
        nextCloudSaveTime = Time.unscaledTime + cloudSaveCooldown;

        return true;
    }

    // Bạn gọi cái này mỗi khi break block/click... tuỳ logic
    public void IncreaseBreakCounter(int amount = 1)
    {
        blockBreakCounter.Value += amount;
    }

    public void SaveDataFn()
    {
        // reset counter (nhưng chỉ reset khi thật sự save)
        if (!CanCloudSaveNow(out var uid))
            return;

        blockBreakCounter.Value = 0;

        SaveGameplay(uid);
        SaveAllInventories(uid);
    }

    private void SaveGameplay(string uid)
    {
        // Guard null string
        string blockValue = string.IsNullOrEmpty(currentBlock) ? "Dirt" : currentBlock;

        string locationValue = currentLocation?.ToString();
        string peakValue = PeakLocation?.ToString();

        float clicks = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.Clicks) : 0f;
        float diamonds = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.Diamond) : 0f;

        float timeValue = CurrentTime;
        if (TimeSystem.Instance != null)
            timeValue = TimeSystem.Instance.CurrentTime.Value;

        var gameplay = new GameplaySaveData
        {
            currentBlock = blockValue,
            currentLocation = locationValue,
            peakLocation = peakValue,
            clicks = clicks,
            diamonds = diamonds,
            currentTime = timeValue
        };

        var userDoc = db.Collection("users").Document(uid);

        var payload = new Dictionary<string, object>
        {
            ["gameplay"] = gameplay,
            ["meta.updatedAt"] = Timestamp.GetCurrentTimestamp(),
            ["meta.rev"] = FieldValue.Increment(1),
            ["meta.saveVersion"] = 1
        };

        userDoc.SetAsync(payload, SetOptions.MergeAll);
    }

    private void SaveAllInventories(string uid)
    {
        foreach (var inv in inventoryDatas)
        {
            SaveOneInventory(uid, inv);
        }
    }

    private void SaveOneInventory(string uid, InventoryData inv)
    {
        InventorySaveData saveData = new InventorySaveData { items = new List<InventoryItemSave>() };

        foreach (var invItem in inv.Items)
        {
            var item = invItem?.itemData != null ? invItem.itemData : inv.NullItem;

            saveData.items.Add(new InventoryItemSave
            {
                itemName = item != null ? item.name : "",
                quantity = invItem?.quantity?.Value ?? 0
            });
        }

        db.Collection("users").Document(uid)
          .Collection("inventories").Document(inv.inventoryType.ToString())
          .SetAsync(saveData);
    }

    // ===== LOAD =====

    public IEnumerator LoadAllInventories(string uid)
    {
        foreach (var inv in inventoryDatas)
            yield return StartCoroutine(LoadOneInventory(uid, inv));

        Debug.Log("✅ All inventories loaded (Firestore)");
    }

    private IEnumerator LoadOneInventory(string uid, InventoryData inv)
    {
        var task = db.Collection("users").Document(uid)
            .Collection("inventories").Document(inv.inventoryType.ToString())
            .GetSnapshotAsync();

        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            Debug.LogError($"❌ Failed to load {inv.inventoryType}: {task.Exception}");
            yield break;
        }

        var snap = task.Result;

        // No data -> default size
        if (!snap.Exists)
        {
            Debug.LogWarning($"⚠️ No data for {inv.inventoryType}, create default size = {inv.GetSize()}");
            inv.Items.Clear();
            EnsureInventorySize(inv);
            yield break;
        }

        var loadedData = snap.ConvertTo<InventorySaveData>() ?? new InventorySaveData();

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
            Debug.Log($"✅ Migrated {inv.inventoryType} to size {inv.GetSize()} -> saving back to Firestore");
            SaveOneInventory(uid, inv);
        }
    }

    public IEnumerator LoadGameplay(string uid, Action onComplete = null)
    {
        // Guard: db not ready
        yield return new WaitUntil(() => db != null);

        var task = db.Collection("users").Document(uid).GetSnapshotAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            Debug.LogError($"❌ Failed to load gameplay doc: {task.Exception}");
            yield break;
        }

        var snap = task.Result;
        if (!snap.Exists)
        {
            Debug.LogWarning("⚠️ No user doc, keep defaults.");
            onComplete?.Invoke();
            yield break;
        }

        if (!snap.TryGetValue("gameplay", out GameplaySaveData gameplay) || gameplay == null)
        {
            Debug.LogWarning("⚠️ No gameplay field.");
            onComplete?.Invoke();
            yield break;
        }

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

        Debug.Log("✅ Loaded gameplay (Firestore)");
        onComplete?.Invoke();
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
        if (paused && isReady && db != null && !string.IsNullOrEmpty(GetUid()))
            SaveDataFn();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && isReady && db != null && !string.IsNullOrEmpty(GetUid()))
            SaveDataFn();
    }

    void OnApplicationQuit()
    {
        if (isReady && db != null && !string.IsNullOrEmpty(GetUid()))
            SaveDataFn();
    }
}
