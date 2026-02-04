using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Game.Discovery;
using System.Collections.Generic;
using UniRx;

namespace Game.UI.Dictionary
{
    public class BlockDictionaryUI : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private BlockUVDatabase blockDb;

        [Header("List UI")]
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private BlockDictionaryListItem itemPrefab;
        [SerializeField] private float itemHeight = 0f;
        [SerializeField] private int buffer = 2;
        [SerializeField, Min(1)] private int maxPoolSize = 7;
        [SerializeField, Min(0f)] private float bottomPadding = 50f;
        [SerializeField] private BlockPreviewCamera previewCamera;

        private readonly Dictionary<int, BlockDictionaryListItem> activeItems = new Dictionary<int, BlockDictionaryListItem>();
        private readonly Queue<BlockDictionaryListItem> itemPool = new Queue<BlockDictionaryListItem>();
        private readonly List<BlockUVEntry> entries = new List<BlockUVEntry>();
        private Coroutine initRoutine;
        private bool subscribed;
        private BlockDiscoveryService discoveryService;
        private bool warnedMissingPreviewCamera;
        private IDisposable locationSubscription;

        private void OnEnable()
        {
            if (initRoutine != null) StopCoroutine(initRoutine);
            initRoutine = StartCoroutine(InitWhenReady());
            if (scrollRect != null)
                scrollRect.onValueChanged.AddListener(OnScrollChanged);
        }

        private void OnDisable()
        {
            if (initRoutine != null)
            {
                StopCoroutine(initRoutine);
                initRoutine = null;
            }
            if (scrollRect != null)
                scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
            if (previewCamera != null)
                previewCamera.ReleaseAtlas();
            locationSubscription?.Dispose();
            locationSubscription = null;
            UnsubscribeDiscovery();
            ClearActive();
        }

        private System.Collections.IEnumerator InitWhenReady()
        {
            yield return new WaitUntil(() => BlockDiscoveryService.Ins != null);
            discoveryService = BlockDiscoveryService.Ins;
            if (previewCamera == null)
                previewCamera = FindObjectOfType<BlockPreviewCamera>(true);
            if (previewCamera == null && !warnedMissingPreviewCamera)
            {
                Debug.LogWarning("[BlockDictionaryUI] Preview camera is missing. Assign BlockPreviewCamera in inspector or add one to the scene.", this);
                warnedMissingPreviewCamera = true;
            }
            SubscribeLocation();
            SubscribeDiscovery();
            RefreshList();
        }

        public void RefreshList()
        {
            var ds = discoveryService ?? BlockDiscoveryService.Ins;
            entries.Clear();
            if (blockDb != null)
            {
                var location = LocationLoader.Ins != null ? LocationLoader.Ins.currentLocation : BlockSpawnLocation.Plain;
                entries.AddRange(blockDb.blocks.Where(x => x != null && x.locationCondition == location));
            }

            EnsureContentHeight();
            ClearActive();
            RefreshVisible(ds);
        }

        private void OnScrollChanged(Vector2 _)
        {
            RefreshVisible(discoveryService ?? BlockDiscoveryService.Ins);
        }

        private void RefreshVisible(BlockDiscoveryService ds)
        {
            if (listRoot == null || scrollRect == null || itemPrefab == null) return;
            if (entries.Count == 0) return;

            EnsureItemHeight();
            float viewportHeight = scrollRect.viewport.rect.height;
            float contentY = listRoot.anchoredPosition.y;
            int firstIndex = Mathf.FloorToInt(contentY / Mathf.Max(1f, itemHeight)) - buffer;
            int visibleCount = Mathf.CeilToInt(viewportHeight / Mathf.Max(1f, itemHeight)) + buffer * 2;
            visibleCount = Mathf.Min(visibleCount, Mathf.Max(1, maxPoolSize));

            int start = Mathf.Clamp(firstIndex, 0, Mathf.Max(0, entries.Count - 1));
            int end = Mathf.Clamp(start + visibleCount - 1, 0, entries.Count - 1);

            var keys = new List<int>(activeItems.Keys);
            foreach (var idx in keys)
            {
                if (idx < start || idx > end)
                    RecycleItem(idx);
            }

            for (int i = start; i <= end; i++)
            {
                if (activeItems.ContainsKey(i)) continue;
                var item = GetItem();
                var rt = item.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.SetParent(listRoot, false);
                    rt.anchoredPosition = new Vector2(0f, -i * itemHeight);
                }
                var entry = entries[i];
                bool discovered = ds != null && ds.IsBlockDiscovered(entry.blockName);
                item.Bind(entry, discovered, ds, previewCamera);
                activeItems[i] = item;
            }
        }

        private BlockDictionaryListItem GetItem()
        {
            BlockDictionaryListItem item = itemPool.Count > 0 ? itemPool.Dequeue() : Instantiate(itemPrefab, listRoot);
            item.gameObject.SetActive(true);
            return item;
        }

        private void RecycleItem(int index)
        {
            if (!activeItems.TryGetValue(index, out var item)) return;
            item.Unbind(previewCamera);
            item.gameObject.SetActive(false);
            if (itemPool.Count < Mathf.Max(1, maxPoolSize))
                itemPool.Enqueue(item);
            else
                Destroy(item.gameObject);
            activeItems.Remove(index);
        }

        private void ClearActive()
        {
            var keys = new List<int>(activeItems.Keys);
            foreach (var idx in keys)
                RecycleItem(idx);
            TrimPool();
        }

        private void TrimPool()
        {
            int limit = Mathf.Max(1, maxPoolSize);
            while (itemPool.Count > limit)
            {
                var item = itemPool.Dequeue();
                if (item != null)
                    Destroy(item.gameObject);
            }
        }

        private void EnsureContentHeight()
        {
            if (listRoot == null) return;
            EnsureItemHeight();
            var size = listRoot.sizeDelta;
            size.y = entries.Count * itemHeight + bottomPadding;
            listRoot.sizeDelta = size;
        }

        private void EnsureItemHeight()
        {
            if (itemHeight > 0f) return;
            var rt = itemPrefab != null ? itemPrefab.GetComponent<RectTransform>() : null;
            if (rt != null)
                itemHeight = Mathf.Max(1f, rt.rect.height);
            if (itemHeight <= 0f)
                itemHeight = 140f;
        }

        private void SubscribeDiscovery()
        {
            if (subscribed || discoveryService == null) return;
            discoveryService.OnBlockDiscovered += OnDiscoveryChanged;
            discoveryService.OnDropDiscovered += OnDropDiscoveryChanged;
            subscribed = true;
        }

        private void SubscribeLocation()
        {
            if (locationSubscription != null) return;
            if (LocationLoader.Ins == null || LocationLoader.Ins.ReactiveLocation == null) return;
            locationSubscription = LocationLoader.Ins.ReactiveLocation
                .DistinctUntilChanged()
                .Subscribe(_ => RefreshList())
                .AddTo(this);
        }

        private void UnsubscribeDiscovery()
        {
            if (!subscribed || discoveryService == null) return;
            discoveryService.OnBlockDiscovered -= OnDiscoveryChanged;
            discoveryService.OnDropDiscovered -= OnDropDiscoveryChanged;
            subscribed = false;
        }

        private void OnDiscoveryChanged(string _)
        {
            RefreshList();
        }

        private void OnDropDiscoveryChanged(string _, string __)
        {
            RefreshVisible(discoveryService);
        }

    }
}
