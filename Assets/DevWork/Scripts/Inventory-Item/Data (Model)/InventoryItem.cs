using Sirenix.OdinInspector;
using UnityEngine;
using UniRx;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[System.Serializable]
public class InventoryItem
{
    [AssetSelector]
    [InlineEditor(InlineEditorModes.GUIAndPreview)]
    [SerializeField] private Item _itemData;

    public ReactiveProperty<int> quantity;

    public ItemPrefix prefix = ItemPrefix.None;
    private static Item _none;
    private static bool _loadingNone = false;

    public static void LoadNoneItem(System.Action onLoaded = null)
    {
        if (_none != null)
        {
            onLoaded?.Invoke();
            return;
        }

        if (_loadingNone) return;
        _loadingNone = true;

        Addressables.LoadAssetAsync<Item>("None").Completed += handle =>
        {
            _loadingNone = false;
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _none = handle.Result;
                DevLog.Log("Loaded 'None' item.");
                onLoaded?.Invoke();
            }
            else
            {
                Debug.LogError("Failed to load 'None' item from Addressables.");
            }
        };
    }

    public Item itemData
    {
        get => _itemData ?? _none;
        set => _itemData = value ?? _none;
    }

    public InventoryItem(Item itemData, int quantity)
    {
        this.itemData = itemData == null ? _none : itemData;
        this.quantity = new ReactiveProperty<int>((itemData == null || itemData.Type == ItemType.None) ? 0 : quantity);
    }

    public bool CanStackWith(InventoryItem other) =>
        other != null && itemData == other.itemData;

    public int AddQuantity(int amount)
    {
        int stackLimit = itemData.MaxStack;
        int space = stackLimit - quantity.Value;
        int toAdd = Mathf.Min(space, amount);
        quantity.Value += toAdd;
        return toAdd;
    }
}

