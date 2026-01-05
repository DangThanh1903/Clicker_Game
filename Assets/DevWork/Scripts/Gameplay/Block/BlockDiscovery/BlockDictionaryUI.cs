using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Discovery;

namespace Game.UI.Dictionary
{
    public class BlockDictionaryUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private BlockUVDatabase blockDb;

        [Header("List UI")]
        [SerializeField] private Transform listRoot;
        [SerializeField] private BlockDictionaryListItem itemPrefab;

        [Header("Details UI")]
        [SerializeField] private TMP_Text detailsTitle;
        [SerializeField] private TMP_Text detailsSpawn;
        [SerializeField] private Transform dropsRoot;
        [SerializeField] private BlockDropRow dropRowPrefab;

        private void OnEnable()
        {
            RefreshList();
        }

        public void RefreshList()
        {
            foreach (Transform c in listRoot) Destroy(c.gameObject);

            var ds = BlockDiscoveryService.Ins;

            foreach (var b in blockDb.blocks.Where(x => x != null))
            {
                bool discovered = ds != null && ds.IsBlockDiscovered(b.blockName);
                var w = Instantiate(itemPrefab, listRoot);
                w.Bind(b.blockName, discovered, () => ShowDetails(b.blockName));
            }
        }

        public void ShowDetails(string blockName)
        {
            var ds = BlockDiscoveryService.Ins;
            bool discovered = ds != null && ds.IsBlockDiscovered(blockName);

            var entry = blockDb.GetByName(blockName);

            detailsTitle.text = discovered ? blockName : "???";
            detailsSpawn.text = discovered && entry != null
                ? $"Appears: {entry.locationCondition} • {entry.timeStateCondition} • {entry.normalWeatherCondition} • {entry.specialWeatherCondition}"
                : "Not discovered.";

            foreach (Transform c in dropsRoot) Destroy(c.gameObject);
            if (!discovered || entry == null) return;

            foreach (var d in entry.drops)
            {
                if (d?.item == null) continue;

                string itemId = BlockDiscoveryService.GetItemId(d.item);
                bool dropDiscovered = ds != null && ds.IsDropDiscovered(blockName, itemId);

                string label = dropDiscovered ? d.item.itemName : "??? (Obtain to reveal)";
                var row = Instantiate(dropRowPrefab, dropsRoot);
                row.Bind(label);
            }
        }
    }
}
