using System;
using System.Collections.Generic;

[Serializable]
public class LocalSaveData
{
    public LocalGameplayData gameplay = new LocalGameplayData();
    public List<LocalInventorySave> inventories = new List<LocalInventorySave>();
    public long savedAtUtcTicks;
}

[Serializable]
public class LocalGameplayData
{
    public string currentBlock;
    public string currentLocation;
    public string peakLocation;
    public float clicks;
    public float diamonds;
    public float currentTime;

    public LocalGameplayData() { }

    public LocalGameplayData(GameplaySaveData src)
    {
        if (src == null) return;
        currentBlock = src.currentBlock;
        currentLocation = src.currentLocation;
        peakLocation = src.peakLocation;
        clicks = src.clicks;
        diamonds = src.diamonds;
        currentTime = src.currentTime;
    }

    public GameplaySaveData ToGameplaySaveData()
    {
        return new GameplaySaveData
        {
            currentBlock = currentBlock,
            currentLocation = currentLocation,
            peakLocation = peakLocation,
            clicks = clicks,
            diamonds = diamonds,
            currentTime = currentTime
        };
    }
}

[Serializable]
public class LocalInventorySave
{
    public string inventoryType;
    public List<LocalInventoryItem> items = new List<LocalInventoryItem>();

    public InventorySaveData ToInventorySaveData()
    {
        var data = new InventorySaveData { items = new List<InventoryItemSave>() };
        foreach (var item in items)
        {
            data.items.Add(new InventoryItemSave
            {
                itemName = item.itemName,
                quantity = item.quantity
            });
        }
        return data;
    }
}

[Serializable]
public class LocalInventoryItem
{
    public string itemName;
    public int quantity;
}
