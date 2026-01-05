using System.Collections.Generic;
using Firebase.Firestore;

[FirestoreData]
public class InventoryItemSave
{
    [FirestoreProperty] public string itemName { get; set; }
    [FirestoreProperty] public int quantity { get; set; }
}

[FirestoreData]
public class InventorySaveData
{
    [FirestoreProperty] public List<InventoryItemSave> items { get; set; } = new List<InventoryItemSave>();
}

[FirestoreData]
public class GameplaySaveData
{
    [FirestoreProperty] public string currentBlock { get; set; }
    [FirestoreProperty] public string currentLocation { get; set; }
    [FirestoreProperty] public string peakLocation { get; set; }
    [FirestoreProperty] public float clicks { get; set; }
    [FirestoreProperty] public float diamonds { get; set; }
    [FirestoreProperty] public float currentTime { get; set; }
}
