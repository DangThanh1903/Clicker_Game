using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Game.Discovery;
using System.Collections.Generic;
using UniRx;
using TMPro;

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

        [Header("Biome Controls")]
        [SerializeField] private TMP_Text biomeNameText;
        [SerializeField] private Button previousBiomeButton;
        [SerializeField] private Button nextBiomeButton;
        [SerializeField] private Button moveToBiomeButton;
        [SerializeField] private TMP_Text moveToBiomeLabel;
        [SerializeField] private bool autoCreateControlsIfMissing = true;
        [SerializeField] private bool cycleBiomeNavigation = true;
        [SerializeField] private string moveHereText = "Move Here";
        [SerializeField] private string currentBiomeText = "Current";

        private readonly Dictionary<int, BlockDictionaryListItem> activeItems = new Dictionary<int, BlockDictionaryListItem>();
        private readonly Queue<BlockDictionaryListItem> itemPool = new Queue<BlockDictionaryListItem>();
        private readonly List<BlockUVEntry> entries = new List<BlockUVEntry>();
        private readonly List<BlockSpawnLocation> availableLocations = new List<BlockSpawnLocation>();
        private Coroutine initRoutine;
        private bool subscribed;
        private bool buttonListenersBound;
        private BlockDiscoveryService discoveryService;
        private bool warnedMissingPreviewCamera;
        private bool hasViewedLocation;
        private bool createdRuntimeControls;
        private BlockSpawnLocation viewedLocation = BlockSpawnLocation.Plain;
        private RectTransform runtimeControlsRoot;
        private IDisposable locationSubscription;

        private void OnEnable()
        {
            if (initRoutine != null) StopCoroutine(initRoutine);
            initRoutine = StartCoroutine(InitWhenReady());
            if (scrollRect != null)
                scrollRect.onValueChanged.AddListener(OnScrollChanged);

            EnsureBiomeControls();
            BindBiomeButtons();
            RefreshBiomeControls();
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

            UnbindBiomeButtons();
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

            if (!hasViewedLocation)
                viewedLocation = LocationLoader.Ins != null ? LocationLoader.Ins.currentLocation : BlockSpawnLocation.Plain;
            hasViewedLocation = true;

            BuildAvailableLocations();
            EnsureViewedLocationInRange();
            RefreshList();
        }

        public void RefreshList()
        {
            var ds = discoveryService ?? BlockDiscoveryService.Ins;
            entries.Clear();
            if (blockDb != null)
            {
                var location = hasViewedLocation ? viewedLocation : BlockSpawnLocation.Plain;
                entries.AddRange(blockDb.blocks.Where(x =>
                    x != null &&
                    (x.locationCondition == location || x.locationCondition == BlockSpawnLocation.Any)));
            }

            BuildAvailableLocations();
            EnsureViewedLocationInRange();
            EnsureContentHeight();
            ClearActive();
            RefreshVisible(ds);
            RefreshBiomeControls();
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
                .Subscribe(OnCurrentLocationChanged)
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

        private void OnCurrentLocationChanged(BlockSpawnLocation location)
        {
            // Follow current location unless the user is browsing a different biome.
            if (!hasViewedLocation || viewedLocation == location)
            {
                SetViewedLocation(location);
                return;
            }

            RefreshBiomeControls();
        }

        private void SetViewedLocation(BlockSpawnLocation location)
        {
            viewedLocation = location;
            hasViewedLocation = true;

            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;

            RefreshList();
        }

        private void BuildAvailableLocations()
        {
            availableLocations.Clear();
            if (blockDb == null || blockDb.blocks == null)
                return;

            var seen = new HashSet<BlockSpawnLocation>();
            for (int i = 0; i < blockDb.blocks.Count; i++)
            {
                var entry = blockDb.blocks[i];
                if (entry == null) continue;

                var loc = entry.locationCondition;
                if (loc == BlockSpawnLocation.Any) continue;

                if (seen.Add(loc))
                    availableLocations.Add(loc);
            }

            availableLocations.Sort((a, b) => ((int)a).CompareTo((int)b));
        }

        private void EnsureViewedLocationInRange()
        {
            if (availableLocations.Count == 0)
            {
                if (!hasViewedLocation)
                {
                    viewedLocation = LocationLoader.Ins != null ? LocationLoader.Ins.currentLocation : BlockSpawnLocation.Plain;
                    hasViewedLocation = true;
                }
                return;
            }

            if (!hasViewedLocation)
            {
                viewedLocation = availableLocations[0];
                hasViewedLocation = true;
                return;
            }

            if (!availableLocations.Contains(viewedLocation))
                viewedLocation = availableLocations[0];
        }

        private void BindBiomeButtons()
        {
            if (buttonListenersBound) return;

            if (previousBiomeButton != null)
                previousBiomeButton.onClick.AddListener(OnPreviousBiomeClicked);
            if (nextBiomeButton != null)
                nextBiomeButton.onClick.AddListener(OnNextBiomeClicked);
            if (moveToBiomeButton != null)
                moveToBiomeButton.onClick.AddListener(OnMoveToBiomeClicked);

            buttonListenersBound = true;
        }

        private void UnbindBiomeButtons()
        {
            if (!buttonListenersBound) return;

            if (previousBiomeButton != null)
                previousBiomeButton.onClick.RemoveListener(OnPreviousBiomeClicked);
            if (nextBiomeButton != null)
                nextBiomeButton.onClick.RemoveListener(OnNextBiomeClicked);
            if (moveToBiomeButton != null)
                moveToBiomeButton.onClick.RemoveListener(OnMoveToBiomeClicked);

            buttonListenersBound = false;
        }

        private void OnPreviousBiomeClicked()
        {
            StepViewedBiome(-1);
        }

        private void OnNextBiomeClicked()
        {
            StepViewedBiome(1);
        }

        private void StepViewedBiome(int delta)
        {
            BuildAvailableLocations();
            EnsureViewedLocationInRange();

            if (availableLocations.Count == 0)
                return;

            int currentIndex = availableLocations.IndexOf(viewedLocation);
            if (currentIndex < 0)
                currentIndex = 0;

            int nextIndex = currentIndex + delta;
            if (cycleBiomeNavigation)
            {
                nextIndex = (nextIndex % availableLocations.Count + availableLocations.Count) % availableLocations.Count;
            }
            else
            {
                nextIndex = Mathf.Clamp(nextIndex, 0, availableLocations.Count - 1);
            }

            if (nextIndex == currentIndex)
                return;

            SetViewedLocation(availableLocations[nextIndex]);
        }

        private void OnMoveToBiomeClicked()
        {
            if (LocationLoader.Ins == null)
            {
                Debug.LogWarning("[BlockDictionaryUI] Cannot move: LocationLoader.Ins is null.", this);
                return;
            }

            LocationLoader.Ins.SetLocation((int)viewedLocation);
            RefreshBiomeControls();
        }

        private void RefreshBiomeControls()
        {
            if (biomeNameText != null)
                biomeNameText.text = viewedLocation.ToString();

            bool hasChoices = availableLocations.Count > 1;
            if (previousBiomeButton != null)
                previousBiomeButton.interactable = hasChoices;
            if (nextBiomeButton != null)
                nextBiomeButton.interactable = hasChoices;

            bool canMove = LocationLoader.Ins != null && LocationLoader.Ins.currentLocation != viewedLocation;
            if (moveToBiomeButton != null)
                moveToBiomeButton.interactable = canMove;
            if (moveToBiomeLabel != null)
                moveToBiomeLabel.text = canMove ? moveHereText : currentBiomeText;
        }

        private void EnsureBiomeControls()
        {
            if (moveToBiomeLabel == null && moveToBiomeButton != null)
                moveToBiomeLabel = moveToBiomeButton.GetComponentInChildren<TMP_Text>(true);

            bool missingRefs = biomeNameText == null ||
                               previousBiomeButton == null ||
                               nextBiomeButton == null ||
                               moveToBiomeButton == null;

            if (!missingRefs || !autoCreateControlsIfMissing || createdRuntimeControls)
                return;

            CreateRuntimeBiomeControls();
        }

        private void CreateRuntimeBiomeControls()
        {
            var root = new GameObject("BiomeControls", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            runtimeControlsRoot = root.GetComponent<RectTransform>();
            runtimeControlsRoot.SetParent(transform, false);
            runtimeControlsRoot.anchorMin = new Vector2(0.5f, 1f);
            runtimeControlsRoot.anchorMax = new Vector2(0.5f, 1f);
            runtimeControlsRoot.pivot = new Vector2(0.5f, 1f);
            runtimeControlsRoot.anchoredPosition = new Vector2(0f, -48f);
            runtimeControlsRoot.sizeDelta = new Vector2(620f, 72f);

            var background = root.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.45f);
            background.raycastTarget = false;

            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var font = ResolveControlFont();

            previousBiomeButton = CreateRuntimeButton(root.transform, "PrevBiomeButton", "<", font, 64f);
            biomeNameText = CreateRuntimeLabel(root.transform, "BiomeNameText", viewedLocation.ToString(), font, 260f);
            nextBiomeButton = CreateRuntimeButton(root.transform, "NextBiomeButton", ">", font, 64f);
            moveToBiomeButton = CreateRuntimeButton(root.transform, "MoveToBiomeButton", moveHereText, font, 180f);
            moveToBiomeLabel = moveToBiomeButton.GetComponentInChildren<TMP_Text>(true);

            runtimeControlsRoot.SetAsLastSibling();
            createdRuntimeControls = true;
        }

        private TMP_FontAsset ResolveControlFont()
        {
            if (biomeNameText != null && biomeNameText.font != null)
                return biomeNameText.font;

            var anyText = GetComponentInChildren<TMP_Text>(true);
            if (anyText != null && anyText.font != null)
                return anyText.font;

            return TMP_Settings.defaultFontAsset;
        }

        private Button CreateRuntimeButton(Transform parent, string name, string text, TMP_FontAsset font, float width)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(width, 52f);

            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.18f);

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 52f;

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(go.transform, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var label = textGo.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 30f;
            label.color = Color.white;
            if (font != null)
                label.font = font;

            return go.GetComponent<Button>();
        }

        private TMP_Text CreateRuntimeLabel(Transform parent, string name, string text, TMP_FontAsset font, float width)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(width, 52f);

            var layout = go.GetComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 52f;

            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 34f;
            label.color = Color.white;
            label.raycastTarget = false;
            if (font != null)
                label.font = font;

            return label;
        }
    }
}
