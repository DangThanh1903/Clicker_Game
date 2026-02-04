using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Discovery;

public class BlockDiscoveryPopupView : PopupView
{
    [Header("UI")]
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text appears;
    [SerializeField] private TMP_Text drops;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button viewDetailsButton;

    private string _blockName;

    // optional hook to open dictionary
    public System.Action<string> OnViewDetailsRequested;

    public void Bind(BlockUVEntry entry)
    {
        _blockName = entry.blockName;

        title.text = $"{entry.blockName}";
        appears.text = BuildAppearsText(entry);

        drops.text = BuildDropsPreview(entry);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => PopupController.Instance.CloseTop());
        }

        if (viewDetailsButton != null)
        {
            viewDetailsButton.onClick.RemoveAllListeners();
            viewDetailsButton.onClick.AddListener(() =>
            {
                OnViewDetailsRequested?.Invoke(_blockName);
                PopupController.Instance.CloseTop();
            });
        }
    }

    private string BuildAppearsText(BlockUVEntry entry)
    {
        if (entry == null)
            return "Appears: Any";

        var parts = new System.Collections.Generic.List<string>();

        if (entry.locationCondition != BlockSpawnLocation.Any)
            parts.Add(entry.locationCondition.ToString());
        if (entry.timeStateCondition != TimeState.Any)
            parts.Add(entry.timeStateCondition.ToString());
        if (entry.normalWeatherCondition != NormalWeatherName.Any)
            parts.Add(entry.normalWeatherCondition.ToString());
        if (entry.specialWeatherCondition != SpecialWeatherName.Any)
            parts.Add(entry.specialWeatherCondition.ToString());

        if (parts.Count == 0)
            return "Appears: Any";

        return "Appears: " + string.Join(" • ", parts);
    }

    private string BuildDropsPreview(BlockUVEntry entry)
    {
        // Practical preview with your current ItemDrop:
        // - show up to 2 drops already discovered
        // - if none discovered, show "Drops: ???" (safer, no spoilers)
        var ds = BlockDiscoveryService.Ins;

        int shown = 0;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("Drops: ");

        foreach (var d in entry.drops)
        {
            if (shown >= 2) break;
            if (d == null || d.item == null) continue;

            string itemId = BlockDiscoveryService.GetItemId(d.item);
            bool discovered = ds != null && ds.IsDropDiscovered(entry.blockName, itemId);

            if (!discovered) continue;

            if (shown > 0) sb.Append(", ");
            sb.Append(d.item.itemName);
            shown++;
        }

        if (shown == 0)
            return "Drops: ???";

        return sb.ToString();
    }
}
