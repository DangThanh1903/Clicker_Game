using UnityEngine;
using Firebase.Database;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System;
using UniRx;
using Sirenix.OdinInspector;

public class DataSaver : MonoBehaviour
{
    public static DataSaver Ins { get; private set; }
    public string currentBlock;
    public BlockSpawnLocation? currentLocation;
    public BlockSpawnLocation? PeakLocation;
    public float CurrentTime;
    private IntReactiveProperty blockBreakCounter = new IntReactiveProperty(0);
    private const int SaveThreshold = 10;
    [SerializeField] private List<InventoryData> inventoryDatas = new List<InventoryData>();
    private DatabaseReference dbRef;

    void Awake()
    {
        dbRef = FirebaseDatabase.DefaultInstance.RootReference;
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
        // Subscribe once at Start
        blockBreakCounter
            .Where(count => count >= SaveThreshold)
            .Subscribe(_ =>
            {
                SaveDataFn();
            })
            .AddTo(this);
    }
    public void OnBlockBreak()
    {
        blockBreakCounter.Value++;
    }
    public void SaveDataFn()
    {
        blockBreakCounter.Value = 0;
        string storedId = PlayerPrefs.GetString("UserID", "");
        string userId = string.IsNullOrEmpty(storedId) ? null : storedId;
        if (userId == null)
        {
            Debug.LogError("User id is null!");
            return;
        }

        // for inventory
        foreach (var inv in inventoryDatas)
        {
            InventorySaveData saveData = new InventorySaveData();
            saveData.items = new List<InventoryItemSave>();
            foreach (var invItem in inv.Items)
            {
                InventoryItemSave saveItem = new InventoryItemSave
                {
                    itemName = invItem.itemData.name,
                    quantity = invItem.quantity.Value
                };

                saveData.items.Add(saveItem);
            }
            string json = JsonUtility.ToJson(saveData);
            dbRef.Child("user").Child(userId).Child("inventory").Child(inv.inventoryType.ToString())
                 .SetRawJsonValueAsync(json);
        }

        // For block
        string blockValue = currentBlock.ToString();
        dbRef.Child("user").Child(userId).Child("Gameplay").Child("currentBlock").SetValueAsync(blockValue);

        // For location
        string locationValue = currentLocation.ToString();
        dbRef.Child("user").Child(userId).Child("Gameplay").Child("currentLocation").SetValueAsync(locationValue);

        // For peak location
        string peakLoc = PeakLocation.ToString();
        dbRef.Child("user").Child(userId).Child("Gameplay").Child("PeakLocation").SetValueAsync(peakLoc);

        // For clicks
        string clicks = StatsManager.Ins.Get(StatType.Clicks).ToString();
        dbRef.Child("user").Child(userId).Child("Gameplay").Child("Clicks").SetValueAsync(clicks);

        // For diamond
        string diamonds = StatsManager.Ins.Get(StatType.Diamond).ToString();
        dbRef.Child("user").Child(userId).Child("Gameplay").Child("Diamonds").SetValueAsync(diamonds);

        // For time
        if (TimeSystem.Instance != null)
        {
            string time = TimeSystem.Instance.CurrentTime.ToString();
            dbRef.Child("user").Child(userId).Child("Gameplay").Child("CurrentTime").SetValueAsync(time);
        }
    }
    public IEnumerator LoadAllInventories(string userId)
    {
        foreach (var inv in inventoryDatas)
        {
            yield return StartCoroutine(LoadOneInventory(userId, inv));
        }

        Debug.Log("✅ All inventories loaded");
    }

    private IEnumerator LoadOneInventory(string userId, InventoryData inv)
    {
        var dataTask = dbRef.Child("user").Child(userId).Child("inventory").Child(inv.inventoryType.ToString()).GetValueAsync();
        yield return new WaitUntil(() => dataTask.IsCompleted);

        if (dataTask.Exception != null)
        {
            Debug.LogError($"❌ Failed to load {inv.inventoryType}: {dataTask.Exception}");
            yield break;
        }

        var snapshot = dataTask.Result;
        if (!snapshot.Exists || string.IsNullOrEmpty(snapshot.GetRawJsonValue()))
        {
            Debug.LogWarning($"⚠️ No data for {inv.inventoryType}");
            yield break;
        }

        string json = snapshot.GetRawJsonValue();
        var loadedData = JsonUtility.FromJson<InventorySaveData>(json);
        inv.Items.Clear();

        foreach (var data in loadedData.items)
        {
            var handle = Addressables.LoadAssetAsync<Item>(data.itemName);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                var itemSO = handle.Result;
                inv.Items.Add(new InventoryItem(itemSO, data.quantity));
            }
            else
            {
                Debug.LogWarning($"[Addressables] Failed to load item: {data.itemName}");
            }
        }
    }

    public IEnumerator LoadCurrentBlock(string userId, Action<string> onComplete = null)
    {
        var dataTask = dbRef.Child("user").Child(userId).Child("Gameplay").Child("currentBlock").GetValueAsync();
        yield return new WaitUntil(() => dataTask.IsCompleted);

        if (dataTask.Exception != null)
        {
            Debug.LogError($"❌ Failed to load currentBlock: {dataTask.Exception}");
            yield break;
        }

        var snapshot = dataTask.Result;
        if (!snapshot.Exists || string.IsNullOrEmpty(snapshot.Value.ToString()))
        {
            Debug.LogWarning("⚠️ No currentBlock saved.");
            currentBlock = "Dirt"; // For not null
            yield break;
        }

        string blockName = snapshot.Value.ToString();
        Debug.Log($"✅ Loaded currentBlock: {blockName}");

        // Assign as string
        currentBlock = blockName;

        onComplete?.Invoke(blockName);
    }


    public IEnumerator LoadCurrentLocation(string userId, Action<BlockSpawnLocation?> onComplete = null)
    {
        var dataTask = dbRef.Child("user").Child(userId).Child("Gameplay").Child("currentLocation").GetValueAsync();
        yield return new WaitUntil(() => dataTask.IsCompleted);

        var dataTask2 = dbRef.Child("user").Child(userId).Child("Gameplay").Child("PeakLocation").GetValueAsync();
        yield return new WaitUntil(() => dataTask2.IsCompleted);

        if (dataTask.Exception != null)
        {
            Debug.LogError($"❌ Failed to load currentLocation: {dataTask.Exception}");
            yield break;
        }

        if (dataTask2.Exception != null)
        {
            Debug.LogError($"❌ Failed to load PeakLocation: {dataTask2.Exception}");
            yield break;
        }

        var snapshot = dataTask.Result;
        if (!snapshot.Exists || string.IsNullOrEmpty(snapshot.Value.ToString()))
        {
            Debug.LogWarning("⚠️ No currentLocation saved.");
            currentLocation = null;
            onComplete?.Invoke(null);
            yield break;
        }

        var snapshot2 = dataTask2.Result;
        if (!snapshot2.Exists || string.IsNullOrEmpty(snapshot2.Value.ToString()))
        {
            Debug.LogWarning("⚠️ No PeakLocation saved.");
            PeakLocation = null;
            onComplete?.Invoke(null);
            yield break;
        }

        string locationString = snapshot.Value.ToString();
        Debug.Log($"✅ Loaded currentLocation: {locationString}");

        if (Enum.TryParse(locationString, out BlockSpawnLocation parsedLocation))
        {
            currentLocation = parsedLocation;
            onComplete?.Invoke(parsedLocation);
        }
        else
        {
            Debug.LogWarning($"⚠️ Unknown location: {locationString}");
            currentLocation = null;
            onComplete?.Invoke(null);
        }

        string peakString = snapshot2.Value.ToString();
        Debug.Log($"✅ Loaded peakLocation: {peakString}");

        if (Enum.TryParse(peakString, out BlockSpawnLocation parsedPeakLocation))
        {
            PeakLocation = parsedPeakLocation;
            onComplete?.Invoke(parsedPeakLocation);
        }
        else
        {
            Debug.LogWarning($"⚠️ Unknown location: {peakString}");
            PeakLocation = null;
            onComplete?.Invoke(null);
        }
    }
    public IEnumerator LoadTime(string userId, Action<string> onComplete = null)
    {
        var dataTask = dbRef.Child("user").Child(userId).Child("Gameplay").Child("CurrentTime").GetValueAsync();
        yield return new WaitUntil(() => dataTask.IsCompleted);

        if (dataTask.Exception != null)
        {
            Debug.LogError($"❌ Failed to load Time: {dataTask.Exception}");
            yield break;
        }
        var snapshot = dataTask.Result;
        if (!snapshot.Exists || string.IsNullOrEmpty(snapshot.Value.ToString()))
        {
            Debug.LogWarning("⚠️ No Time saved.");
            onComplete?.Invoke(null);
            yield break;
        }

        CurrentTime = Convert.ToSingle(snapshot.Value);
        Debug.Log($"✅ Loaded Time: {CurrentTime}");
    }

    public IEnumerator LoadSomeStat(string userId, Action<string> onComplete = null)
    {
        var dataTask = dbRef.Child("user").Child(userId).Child("Gameplay").Child("Clicks").GetValueAsync();
        yield return new WaitUntil(() => dataTask.IsCompleted);

        var dataTask2 = dbRef.Child("user").Child(userId).Child("Gameplay").Child("Diamonds").GetValueAsync();
        yield return new WaitUntil(() => dataTask2.IsCompleted);

        if (dataTask.Exception != null)
        {
            Debug.LogError($"❌ Failed to load Clicks: {dataTask.Exception}");
            yield break;
        }

        var snapshot = dataTask.Result;
        if (!snapshot.Exists || string.IsNullOrEmpty(snapshot.Value.ToString()))
        {
            Debug.LogWarning("⚠️ No Clicks saved.");
            onComplete?.Invoke(null);
            yield break;
        }

        if (dataTask2.Exception != null)
        {
            Debug.LogError($"❌ Failed to load Diamonds: {dataTask.Exception}");
            yield break;
        }

        var snapshot2 = dataTask2.Result;
        if (!snapshot2.Exists || string.IsNullOrEmpty(snapshot2.Value.ToString()))
        {
            Debug.LogWarning("⚠️ No Diamonds saved.");
            onComplete?.Invoke(null);
            yield break;
        }

        float clicks = Convert.ToSingle(snapshot.Value);
        Debug.Log($"✅ Loaded Clicks: {clicks}");

        float diamonds = Convert.ToSingle(snapshot2.Value);
        Debug.Log($"✅ Loaded Diamonds: {diamonds}");

        StatsManager.Ins.Set(StatType.Clicks, clicks);
        StatsManager.Ins.Set(StatType.Diamond, diamonds);
    }
    void OnApplicationPause(bool paused)
    {
        if (paused) SaveDataFn(); // app going background
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) SaveDataFn(); // lost focus (alt-tab etc.)
    }

    void OnApplicationQuit()
    {
        SaveDataFn(); // good to call, but not your only save
    }
}
