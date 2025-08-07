using UnityEngine;
using Firebase.Database;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System;
using Sirenix.OdinInspector;

public class DataSaver : MonoBehaviour
{
    public static DataSaver Ins { get; private set; }
    public string currentBlock;
    public BlockSpawnLocation? currentLocation;
    public List<InventoryData> inventoryDatas = new List<InventoryData>();
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

    public void SaveDataFn()
    {
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

        if (dataTask.Exception != null)
        {
            Debug.LogError($"❌ Failed to load currentLocation: {dataTask.Exception}");
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
    }

}
