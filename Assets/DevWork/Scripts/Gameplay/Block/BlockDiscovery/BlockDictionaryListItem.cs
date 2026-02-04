using System.Collections.Generic;
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

        private readonly List<BlockDropIconItem> activeDropIcons = new List<BlockDropIconItem>();
        private readonly Stack<BlockDropIconItem> dropIconPool = new Stack<BlockDropIconItem>();
        private BlockPreviewCamera.PreviewInstance previewInstance;
        private BlockPreviewCamera.PreviewSlot previewSlot;
        private bool hasSlot;
        private string lastBlockName;
        private BlockPreviewCamera lastPreviewCamera;

        public void Bind(BlockUVEntry entry, bool discovered, BlockDiscoveryService ds, BlockPreviewCamera preview)
        {
            if (label != null)
                label.text = discovered ? entry.blockName : "???";

            BuildDropList(entry, ds);
            BuildPreview(entry, preview, discovered);
        }

        public void Unbind(BlockPreviewCamera preview)
        {
            ReleasePreview(preview);
            ClearDropIcons();
            lastBlockName = null;
            lastPreviewCamera = null;
            if (preview != null && previewInstance != null)
                preview.ReleasePreview(previewInstance);
            previewInstance = null;
        }

        private void BuildDropList(BlockUVEntry entry, BlockDiscoveryService ds)
        {
            if (dropsRoot == null || dropIconPrefab == null || entry == null) return;

            ClearDropIcons();

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

                var icon = GetDropIcon();
                icon.Bind(drop.item.icon, discovered);
                activeDropIcons.Add(icon);
            }
        }

        private void BuildPreview(BlockUVEntry entry, BlockPreviewCamera preview, bool discovered)
        {
            if (previewImage == null || preview == null || entry == null) return;

            ReleasePreview(preview);

            // Keep preview texture even if undiscovered, just tint it darker
            previewImage.color = discovered ? Color.white : Color.black;
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
            preview.RenderBlock(previewInstance, entry.blockName, previewSlot, previewImage.canvasRenderer);

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

        private BlockDropIconItem GetDropIcon()
        {
            BlockDropIconItem icon = dropIconPool.Count > 0 ? dropIconPool.Pop() : Instantiate(dropIconPrefab);
            icon.transform.SetParent(dropsRoot, false);
            icon.gameObject.SetActive(true);
            return icon;
        }

        private void RecycleDropIcon(BlockDropIconItem icon)
        {
            if (icon == null) return;
            icon.gameObject.SetActive(false);
            icon.transform.SetParent(dropsRoot, false);
            dropIconPool.Push(icon);
        }

        private void ClearDropIcons()
        {
            for (int i = 0; i < activeDropIcons.Count; i++)
                RecycleDropIcon(activeDropIcons[i]);
            activeDropIcons.Clear();
        }
    }
}
