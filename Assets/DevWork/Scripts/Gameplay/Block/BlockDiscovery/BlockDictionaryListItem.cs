using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Discovery;

namespace Game.UI.Dictionary
{
    public class BlockDictionaryListItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private RawImage previewImage;
        [SerializeField] private Transform dropsRoot;
        [SerializeField] private BlockDropIconItem dropIconPrefab;

        private BlockPreviewCamera.PreviewInstance previewInstance;
        private BlockPreviewCamera.PreviewSlot previewSlot;
        private bool hasSlot;
        private string lastBlockName;
        private BlockPreviewCamera lastPreviewCamera;

        public void Bind(BlockUVEntry entry, bool discovered, BlockDiscoveryService ds, BlockPreviewCamera preview)
        {
            if (label != null)
                label.text = discovered ? entry.blockName : "??? (Not discovered)";

            BuildDropList(entry, ds);
            BuildPreview(entry, preview);
        }

        public void Unbind(BlockPreviewCamera preview)
        {
            ReleasePreview(preview);
            lastBlockName = null;
            lastPreviewCamera = null;
            if (preview != null && previewInstance != null)
                preview.ReleasePreview(previewInstance);
            previewInstance = null;
        }

        private void BuildDropList(BlockUVEntry entry, BlockDiscoveryService ds)
        {
            if (dropsRoot == null || dropIconPrefab == null || entry == null) return;

            foreach (Transform c in dropsRoot)
                Destroy(c.gameObject);

            if (entry.drops == null || entry.drops.Count == 0) return;

            var ordered = entry.drops
                .Where(d => d != null && d.item != null)
                .OrderByDescending(d => (int)d.item.rarity)
                .ThenBy(d => d.item.itemName);

            foreach (var drop in ordered)
            {
                string itemId = BlockDiscoveryService.GetItemId(drop.item);
                bool discovered = ds != null && ds.IsDropDiscovered(entry.blockName, itemId);

                if (drop.isSecret && !discovered)
                    continue;

                var icon = Instantiate(dropIconPrefab, dropsRoot);
                icon.Bind(drop.item.icon, discovered);
            }
        }

        private void BuildPreview(BlockUVEntry entry, BlockPreviewCamera preview)
        {
            if (previewImage == null || preview == null || entry == null) return;

            ReleasePreview(preview);
            if (previewInstance == null)
                previewInstance = preview.AcquirePreview();

            if (!preview.TryAcquireSlot(out previewSlot))
            {
                previewImage.texture = null;
                previewImage.uvRect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            hasSlot = true;
            previewImage.texture = preview.AtlasTexture;
            previewImage.uvRect = previewSlot.uvRect;
            preview.RenderBlock(previewInstance, entry.blockName, previewSlot);

            lastBlockName = entry.blockName;
            lastPreviewCamera = preview;
        }

        private void ReleasePreview(BlockPreviewCamera preview)
        {
            if (previewImage != null)
            {
                previewImage.texture = null;
                previewImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            }

            if (preview != null && hasSlot)
                preview.ReleaseSlot(previewSlot);
            hasSlot = false;
        }
    }
}
