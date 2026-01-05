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

        title.text = $"Discovered: {entry.blockName}";
        appears.text =
            $"Appears: {entry.locationCondition} • {entry.timeStateCondition} • {entry.normalWeatherCondition} • {entry.specialWeatherCondition}";

        drops.text = BuildDropsPreview(entry);

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => PopupController.Instance.CloseTop());

        viewDetailsButton.onClick.RemoveAllListeners();
        viewDetailsButton.onClick.AddListener(() =>
        {
            OnViewDetailsRequested?.Invoke(_blockName);
            PopupController.Instance.CloseTop();
        });
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
