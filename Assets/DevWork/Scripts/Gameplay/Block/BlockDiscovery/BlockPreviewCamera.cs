using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_RENDER_PIPELINE_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

namespace Game.UI.Dictionary
{
    public class BlockPreviewCamera : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Transform previewRoot;
        [SerializeField] private int previewLayer = 30;
        [SerializeField] private int hiddenLayer = 31;

        [Header("Data")]
        [SerializeField] private BlockUVDatabase blockDb;
        [SerializeField] private Material previewMaterial;
        [SerializeField] private Texture2D atlasTexture;

        [Header("Preview Atlas")]
        [SerializeField] private int previewAtlasSize = 1024;
        [SerializeField] private int previewCellSize = 128;
        [SerializeField] private int previewCellPadding = 4;
        [SerializeField] private int maxRendersPerFrame = 6;
        [SerializeField, Min(0f)] private float previewFps = 10f;

        [Header("UV Atlas")]
        [SerializeField] private int atlasColumns = 6;
        [SerializeField] private int atlasRows = 10;
        [SerializeField] private int blockColumns = 3;
        [SerializeField] private int blockRows = 2;
        private bool flipY = true;
        private Vector2Int[] faceTiles = new Vector2Int[6]
        {
            new Vector2Int(0, 0), // Back
            new Vector2Int(1, 0), // Front
            new Vector2Int(2, 0), // Top
            new Vector2Int(0, 1), // Under
            new Vector2Int(1, 1), // Left
            new Vector2Int(2, 1)  // Right
        };

        [Header("Preview Transform")]
        [SerializeField] private Vector3 previewRotation = new Vector3(25f, 35f, 0f);
        [SerializeField] private float previewScale = 1f;
        [SerializeField] private bool autoPreviewLight = true;
        [SerializeField] private Color previewLightColor = Color.white;
        [SerializeField] private float previewLightIntensity = 1.1f;
        [SerializeField, Min(0f)] private float previewLightBoost = 1.5f;

        [Header("Preview Rotation Sync")]
        [SerializeField] private bool enablePreviewUpdates = true;
        [SerializeField] private bool copyRotationFromSource = false;
        [SerializeField] private Transform rotationSource;
        [SerializeField] private Color previewClearColor = new Color(0f, 0f, 0f, 0f);
        [SerializeField] private bool debugPreview = false;

        private readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        private readonly Stack<PreviewInstance> instancePool = new Stack<PreviewInstance>();
        private readonly List<PreviewInstance> activeInstances = new List<PreviewInstance>();
        private readonly Stack<int> freeSlots = new Stack<int>();
        private readonly Dictionary<int, ActivePreview> activePreviews = new Dictionary<int, ActivePreview>();
        private readonly List<ActivePreview> activePreviewSnapshot = new List<ActivePreview>(128);
        private readonly HashSet<int> queuedSlots = new HashSet<int>();
        private bool warnedMissingCamera;
        private bool warnedMissingDatabase;
        private bool warnedMissingMaterial;
        private bool warnedMissingAtlas;
        private bool warnedMissingPreviewAtlas;
        private bool warnedMissingRotationSource;
        private float nextDebugAt;
        private Quaternion lastDebugRotation = Quaternion.identity;
        private RenderTexture previewAtlas;
        private int previewColumns;
        private int previewRows;
        private int nextSlotIndex;
        private Light previewLight;
        private float nextRenderAt;
        private bool hasNextRenderTime;
#if UNITY_RENDER_PIPELINE_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
        private bool renderHooked;
#endif

#if UNITY_RENDER_PIPELINE_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
        private struct RenderRequest
        {
            public PreviewInstance instance;
            public string blockName;
            public RenderTexture target;
            public Rect viewportRect;
            public float aspect;
            public int slotIndex;
        }
        private readonly Queue<RenderRequest> renderQueue = new Queue<RenderRequest>();
#endif
        private struct ActivePreview
        {
            public PreviewInstance instance;
            public string blockName;
            public PreviewSlot slot;
            public CanvasRenderer canvasRenderer;
        }

        public sealed class PreviewInstance
        {
            public GameObject gameObject;
            public MeshFilter meshFilter;
            public MeshRenderer meshRenderer;
        }

        public struct PreviewSlot
        {
            public int index;
            public Rect uvRect;
            public Rect viewportRect;
        }

        public RenderTexture AtlasTexture => previewAtlas;
        private bool ShouldAnimatePreviews => enablePreviewUpdates && copyRotationFromSource;
        private bool UseScriptableRenderPipeline => GraphicsSettings.currentRenderPipeline != null;
#if UNITY_RENDER_PIPELINE_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
        private const bool SupportsSrpHook = true;
#else
        private const bool SupportsSrpHook = false;
#endif

        void Awake()
        {
            EnsurePreviewObjects();
        }

        private void Update()
        {
            if (!ShouldAnimatePreviews || activePreviews.Count == 0) return;
            if (UseScriptableRenderPipeline && SupportsSrpHook) return;

            if (previewFps > 0f)
            {
                float interval = 1f / previewFps;
                float now = Time.unscaledTime;
                if (hasNextRenderTime && now < nextRenderAt) return;
                nextRenderAt = now + interval;
                hasNextRenderTime = true;
            }

            BuildActivePreviewSnapshot();
            int limit = Mathf.Max(1, maxRendersPerFrame);
            int processed = 0;
            for (int i = 0; i < activePreviewSnapshot.Count && processed < limit; i++)
            {
                var preview = activePreviewSnapshot[i];
                if (!IsPreviewVisible(preview))
                    continue;
                RenderBlock(preview.instance, preview.blockName, preview.slot);
                processed++;
            }

            if (debugPreview && Time.unscaledTime >= nextDebugAt)
            {
                nextDebugAt = Time.unscaledTime + 1f;
                var rot = rotationSource != null ? rotationSource.localRotation : Quaternion.identity;
                bool rotChanged = rot != lastDebugRotation;
                lastDebugRotation = rot;
                DevLog.Log($"[BlockPreviewCamera] Update fallback tick. Active={activePreviews.Count}, Limit={limit}, RotChanged={rotChanged}");
            }
        }

        private void OnEnable()
        {
#if UNITY_RENDER_PIPELINE_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
            EnsureRenderHook();
#endif
        }

        private void OnDisable()
        {
#if UNITY_RENDER_PIPELINE_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
            ReleaseRenderHook();
#endif
        }

        private void OnDestroy()
        {
#if UNITY_RENDER_PIPELINE_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
            ReleaseRenderHook();
#endif

            activePreviewSnapshot.Clear();
        }

        public PreviewInstance AcquirePreview()
        {
            EnsurePreviewObjects();
            var instance = instancePool.Count > 0 ? instancePool.Pop() : CreateInstance();
            if (!activeInstances.Contains(instance))
                activeInstances.Add(instance);
            if (instance.gameObject != null)
            {
                instance.gameObject.SetActive(true);
                SetLayerRecursively(instance.gameObject, hiddenLayer);
            }
            return instance;
        }

        public void ReleasePreview(PreviewInstance instance)
        {
            if (instance == null) return;
            activeInstances.Remove(instance);
            UnregisterActivePreview(instance);
            if (instance.gameObject != null)
                instance.gameObject.SetActive(false);
            instancePool.Push(instance);
        }

        public void ReleaseAtlas()
        {
            if (previewAtlas != null)
            {
                previewAtlas.Release();
                previewAtlas = null;
            }
            nextSlotIndex = 0;
            freeSlots.Clear();
            activePreviews.Clear();
            activePreviewSnapshot.Clear();
            queuedSlots.Clear();
#if UNITY_RENDER_PIPELINE_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
            renderQueue.Clear();
#endif
        }

        private void BuildActivePreviewSnapshot()
        {
            activePreviewSnapshot.Clear();
            foreach (var preview in activePreviews.Values)
                activePreviewSnapshot.Add(preview);
        }

        public bool TryAcquireSlot(out PreviewSlot slot)
        {
            EnsurePreviewAtlas();
            slot = default;
            if (previewAtlas == null)
            {
                if (!warnedMissingPreviewAtlas)
                {
                    Debug.LogWarning("[BlockPreviewCamera] Preview atlas is missing.", this);
                    warnedMissingPreviewAtlas = true;
                }
                return false;
            }

            int maxSlots = Mathf.Max(1, previewColumns * previewRows);
            int index;
            if (freeSlots.Count > 0)
            {
                index = freeSlots.Pop();
            }
            else if (nextSlotIndex < maxSlots)
            {
                index = nextSlotIndex++;
            }
            else
            {
                Debug.LogWarning("[BlockPreviewCamera] Preview atlas is full. Increase atlas size or reduce visible items.", this);
                return false;
            }

            slot = BuildSlot(index);
            return true;
        }

        public void ReleaseSlot(PreviewSlot slot)
        {
            if (slot.index < 0) return;
            freeSlots.Push(slot.index);
            UnregisterActivePreview(slot.index);
        }

        public void RenderBlock(PreviewInstance instance, string blockName, PreviewSlot slot, CanvasRenderer canvasRenderer = null)
        {
            if (instance == null) return;
            if (!TryEnsureCamera())
            {
                if (!warnedMissingCamera)
                {
                    Debug.LogWarning("[BlockPreviewCamera] RenderBlock skipped: preview camera is missing.", this);
                    warnedMissingCamera = true;
                }
                return;
            }
            if (blockDb == null)
            {
                if (!warnedMissingDatabase)
                {
                    Debug.LogWarning("[BlockPreviewCamera] RenderBlock skipped: BlockUVDatabase is missing.", this);
                    warnedMissingDatabase = true;
                }
                return;
            }

            if (enablePreviewUpdates)
                RegisterActivePreview(instance, blockName, slot, canvasRenderer);
            EnsurePreviewAtlas();
            if (previewAtlas == null) return;

#if UNITY_RENDER_PIPELINE_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
            if (UseScriptableRenderPipeline)
            {
                EnsureRenderHook();
                float cellWidth = slot.viewportRect.width * previewAtlas.width;
                float cellHeight = slot.viewportRect.height * previewAtlas.height;
                EnqueuePreview(new ActivePreview
                {
                    instance = instance,
                    blockName = blockName,
                    slot = slot
                }, cellWidth, cellHeight);
                return;
            }
#endif

            EnsurePreviewObjects();

            instance.meshFilter.sharedMesh = GetMesh(blockName);
            if (instance.meshRenderer != null && instance.meshRenderer.sharedMaterial == null && previewMaterial == null && !warnedMissingMaterial)
            {
                Debug.LogWarning("[BlockPreviewCamera] Preview material is missing.", this);
                warnedMissingMaterial = true;
            }
            if (instance.meshRenderer != null && instance.meshRenderer.sharedMaterial != null && atlasTexture == null && !warnedMissingAtlas)
            {
                Debug.LogWarning("[BlockPreviewCamera] Atlas texture is missing.", this);
                warnedMissingAtlas = true;
            }
            if (instance.meshRenderer != null && instance.meshRenderer.sharedMaterial != null && atlasTexture != null)
                instance.meshRenderer.sharedMaterial.mainTexture = atlasTexture;

            var prevRect = previewCamera.rect;
            var prevAspect = previewCamera.aspect;
            previewCamera.rect = slot.viewportRect;
            previewCamera.aspect = Mathf.Approximately(slot.viewportRect.height, 0f) ? prevAspect : slot.viewportRect.width / slot.viewportRect.height;

            ApplyPreviewRotation(instance);
            SetActiveInstance(instance);
            ClearSlot(previewAtlas, slot.viewportRect, previewClearColor);
            previewCamera.targetTexture = previewAtlas;
            previewCamera.Render();
            previewCamera.targetTexture = null;
            previewCamera.rect = prevRect;
            previewCamera.aspect = prevAspect;
            HideInstance(instance);
        }

#if UNITY_RENDER_PIPELINE_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
        private void OnEndFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            if (!UseScriptableRenderPipeline) return;
            if (renderQueue.Count == 0 && (!ShouldAnimatePreviews || activePreviews.Count == 0)) return;
            if (!TryEnsureCamera()) return;
            if (blockDb == null) return;

            if (previewFps > 0f)
            {
                float interval = 1f / previewFps;
                float now = Time.unscaledTime;
                if (hasNextRenderTime && now < nextRenderAt) return;
                nextRenderAt = now + interval;
                hasNextRenderTime = true;
            }

            if (renderQueue.Count == 0 && ShouldAnimatePreviews)
            {
                foreach (var preview in activePreviews.Values)
                    EnqueuePreview(preview, -1f, -1f);
            }

            EnsurePreviewObjects();

            var prevRect = previewCamera.rect;
            var prevAspect = previewCamera.aspect;
            int processed = 0;
            int limit = Mathf.Max(1, maxRendersPerFrame);

            while (renderQueue.Count > 0 && processed < limit)
            {
                var req = renderQueue.Dequeue();
                queuedSlots.Remove(req.slotIndex);
                if (req.instance == null || req.target == null) continue;
                processed++;

                req.instance.meshFilter.sharedMesh = GetMesh(req.blockName);
                if (req.instance.meshRenderer != null && req.instance.meshRenderer.sharedMaterial != null && atlasTexture != null)
                    req.instance.meshRenderer.sharedMaterial.mainTexture = atlasTexture;

                previewCamera.rect = req.viewportRect;
                previewCamera.aspect = req.aspect;

                ApplyPreviewRotation(req.instance);
                SetActiveInstance(req.instance);
                ClearSlot(req.target, req.viewportRect, previewClearColor);
                previewCamera.targetTexture = req.target;
                UniversalRenderPipeline.RenderSingleCamera(context, previewCamera);
                previewCamera.targetTexture = null;
                HideInstance(req.instance);
            }

            previewCamera.rect = prevRect;
            previewCamera.aspect = prevAspect;

            if (debugPreview && Time.unscaledTime >= nextDebugAt)
            {
                nextDebugAt = Time.unscaledTime + 1f;
                var rot = rotationSource != null ? rotationSource.localRotation : Quaternion.identity;
                bool rotChanged = rot != lastDebugRotation;
                lastDebugRotation = rot;
                DevLog.Log($"[BlockPreviewCamera] SRP tick. Queue={renderQueue.Count}, Active={activePreviews.Count}, RotChanged={rotChanged}");
            }
        }
#endif

        private void EnsurePreviewObjects()
        {
            if (hiddenLayer == previewLayer)
                hiddenLayer = (previewLayer + 1) % 32;

            if (previewRoot == null)
            {
                var root = new GameObject("PreviewRoot");
                root.transform.SetParent(transform, false);
                previewRoot = root.transform;
            }

            if (previewRoot != null)
                previewRoot.gameObject.layer = previewLayer;
            EnsurePreviewLight();
            if (previewCamera != null)
            {
                previewCamera.enabled = false;
                previewCamera.clearFlags = CameraClearFlags.SolidColor;
                previewCamera.backgroundColor = previewClearColor;
                previewCamera.forceIntoRenderTexture = true;
                previewCamera.allowMSAA = false;
                previewCamera.allowHDR = false;
                previewCamera.cullingMask = 1 << previewLayer;
            }
        }

        private void EnsurePreviewAtlas()
        {
            int size = Mathf.Max(128, previewAtlasSize);
            int cell = Mathf.Clamp(previewCellSize, 8, size);
            int stride = Mathf.Max(1, cell + Mathf.Max(0, previewCellPadding));
            int cols = Mathf.Max(1, (size + Mathf.Max(0, previewCellPadding)) / stride);
            int rows = Mathf.Max(1, (size + Mathf.Max(0, previewCellPadding)) / stride);

            bool needsRebuild = previewAtlas == null || !previewAtlas.IsCreated() || previewAtlas.width != size || previewAtlas.height != size;
            if (needsRebuild)
            {
                if (previewAtlas != null)
                    previewAtlas.Release();

                previewAtlas = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                previewAtlas.Create();

                previewColumns = cols;
                previewRows = rows;
                nextSlotIndex = 0;
                freeSlots.Clear();

                ClearAtlas(previewAtlas, previewClearColor);
            }
            else
            {
                previewColumns = cols;
                previewRows = rows;
            }
        }

        private PreviewSlot BuildSlot(int index)
        {
            int size = Mathf.Max(128, previewAtlasSize);
            int cell = Mathf.Clamp(previewCellSize, 8, size);
            int stride = Mathf.Max(1, cell + Mathf.Max(0, previewCellPadding));
            int col = index % Mathf.Max(1, previewColumns);
            int row = index / Mathf.Max(1, previewColumns);

            float x = col * stride;
            float y = row * stride;
            var uv = new Rect(x / size, y / size, cell / (float)size, cell / (float)size);
            return new PreviewSlot
            {
                index = index,
                uvRect = uv,
                viewportRect = uv
            };
        }

        private bool TryEnsureCamera()
        {
            if (previewCamera != null)
                return true;
            if (previewCamera == null)
                previewCamera = GetComponentInChildren<Camera>(true);
            if (previewCamera == null)
                previewCamera = GetComponent<Camera>();
            return previewCamera != null;
        }

#if UNITY_RENDER_PIPELINE_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
        private void EnsureRenderHook()
        {
            if (renderHooked) return;
            RenderPipelineManager.endFrameRendering += OnEndFrameRendering;
            renderHooked = true;
        }

        private void ReleaseRenderHook()
        {
            if (!renderHooked) return;
            RenderPipelineManager.endFrameRendering -= OnEndFrameRendering;
            renderHooked = false;
        }
#endif

        private PreviewInstance CreateInstance()
        {
            var meshGo = new GameObject("PreviewMesh");
            meshGo.transform.SetParent(previewRoot, false);
            meshGo.transform.localRotation = Quaternion.Euler(previewRotation);
            meshGo.transform.localScale = Vector3.one * previewScale;

            var meshFilter = meshGo.AddComponent<MeshFilter>();
            var meshRenderer = meshGo.AddComponent<MeshRenderer>();
            if (previewMaterial != null)
            {
                var instanceMaterial = new Material(previewMaterial);
                if (atlasTexture != null)
                    instanceMaterial.mainTexture = atlasTexture;
                instanceMaterial.mainTextureScale = Vector2.one;
                instanceMaterial.mainTextureOffset = Vector2.zero;
                meshRenderer.sharedMaterial = instanceMaterial;
            }

            return new PreviewInstance
            {
                gameObject = meshGo,
                meshFilter = meshFilter,
                meshRenderer = meshRenderer
            };
        }

        private void ApplyPreviewRotation(PreviewInstance instance)
        {
            if (!copyRotationFromSource || instance == null || instance.gameObject == null)
                return;
            if (rotationSource == null)
            {
                if (!warnedMissingRotationSource)
                {
                    Debug.LogWarning("[BlockPreviewCamera] Copy rotation enabled but rotation source is missing.", this);
                    warnedMissingRotationSource = true;
                }
                return;
            }

            instance.gameObject.transform.localRotation = rotationSource.localRotation;
        }

        private void RegisterActivePreview(PreviewInstance instance, string blockName, PreviewSlot slot, CanvasRenderer canvasRenderer)
        {
            if (instance == null || slot.index < 0) return;
            if (canvasRenderer == null && activePreviews.TryGetValue(slot.index, out var existing))
                canvasRenderer = existing.canvasRenderer;
            activePreviews[slot.index] = new ActivePreview
            {
                instance = instance,
                blockName = blockName,
                slot = slot,
                canvasRenderer = canvasRenderer
            };
        }

        private void UnregisterActivePreview(int slotIndex)
        {
            if (slotIndex < 0) return;
            activePreviews.Remove(slotIndex);
            queuedSlots.Remove(slotIndex);
        }

        private void UnregisterActivePreview(PreviewInstance instance)
        {
            if (instance == null || activePreviews.Count == 0) return;
            int foundIndex = -1;
            foreach (var kvp in activePreviews)
            {
                if (kvp.Value.instance == instance)
                {
                    foundIndex = kvp.Key;
                    break;
                }
            }
            if (foundIndex >= 0)
                UnregisterActivePreview(foundIndex);
        }

#if UNITY_RENDER_PIPELINE_URP || UNITY_RENDER_PIPELINE_UNIVERSAL
        private void EnqueuePreview(ActivePreview preview, float cellWidth, float cellHeight)
        {
            int slotIndex = preview.slot.index;
            if (slotIndex < 0) return;
            if (!IsPreviewVisible(preview)) return;
            if (!queuedSlots.Add(slotIndex)) return;

            float width = cellWidth;
            float height = cellHeight;
            if (width <= 0f || height <= 0f)
            {
                EnsurePreviewAtlas();
                if (previewAtlas != null)
                {
                    width = preview.slot.viewportRect.width * previewAtlas.width;
                    height = preview.slot.viewportRect.height * previewAtlas.height;
                }
            }

            renderQueue.Enqueue(new RenderRequest
            {
                instance = preview.instance,
                blockName = preview.blockName,
                target = previewAtlas,
                viewportRect = preview.slot.viewportRect,
                aspect = Mathf.Approximately(height, 0f) ? 1f : width / height,
                slotIndex = slotIndex
            });
        }
#endif

        private static bool IsPreviewVisible(ActivePreview preview)
        {
            var renderer = preview.canvasRenderer;
            return renderer == null || (renderer.gameObject.activeInHierarchy && !renderer.cull);
        }

        private void EnsurePreviewLight()
        {
            if (!autoPreviewLight || previewRoot == null)
                return;

            if (previewLight == null)
            {
                var lightGo = new GameObject("PreviewLight");
                lightGo.transform.SetParent(previewRoot, false);
                previewLight = lightGo.AddComponent<Light>();
            }

            previewLight.type = LightType.Directional;
            previewLight.color = previewLightColor;
            previewLight.intensity = previewLightIntensity * previewLightBoost;
            previewLight.shadows = LightShadows.None;
            previewLight.cullingMask = 1 << previewLayer;
        }

        private void SetActiveInstance(PreviewInstance instance)
        {
            for (int i = 0; i < activeInstances.Count; i++)
            {
                var other = activeInstances[i];
                if (other == null || other.gameObject == null) continue;
                SetLayerRecursively(other.gameObject, hiddenLayer);
            }

            if (instance != null && instance.gameObject != null)
                SetLayerRecursively(instance.gameObject, previewLayer);
        }

        private void HideInstance(PreviewInstance instance)
        {
            if (instance == null || instance.gameObject == null) return;
            SetLayerRecursively(instance.gameObject, hiddenLayer);
        }

        private Mesh GetMesh(string blockName)
        {
            if (string.IsNullOrEmpty(blockName)) blockName = string.Empty;
            if (meshCache.TryGetValue(blockName, out var mesh) && mesh != null)
                return mesh;

            mesh = BuildMesh(blockName);
            meshCache[blockName] = mesh;
            return mesh;
        }

        private Mesh BuildMesh(string blockName)
        {
            EnsureFaceTiles();

            float tileSizeX = 1f / atlasColumns;
            float tileSizeY = 1f / atlasRows;

            var verts = new Vector3[24];
            var uvs = new Vector2[24];
            var tris = new int[36];

            Vector3[][] cubeFaces =
            {
                new Vector3[] { new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f),
                                new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f)},
                new Vector3[] { new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f),
                                new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f)},
                new Vector3[] { new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f),
                                new Vector3(-0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f)},
                new Vector3[] { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f),
                                new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f)},
                new Vector3[] { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f,  0.5f),
                                new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f,  0.5f)},
                new Vector3[] { new Vector3( 0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f, -0.5f),
                                new Vector3( 0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f, -0.5f)},
            };

            for (int i = 0; i < 6; i++)
            {
                int vi = i * 4;
                Vector2Int tile = faceTiles[i] + SetMapByName(blockName);
                if (flipY) tile.y = atlasRows - 1 - tile.y;

                Vector2 uvOffset = new Vector2(tile.x * tileSizeX, tile.y * tileSizeY);

                verts[vi + 0] = cubeFaces[i][0];
                verts[vi + 1] = cubeFaces[i][1];
                verts[vi + 2] = cubeFaces[i][2];
                verts[vi + 3] = cubeFaces[i][3];

                uvs[vi + 0] = uvOffset + new Vector2(0, 0) * new Vector2(tileSizeX, tileSizeY);
                uvs[vi + 1] = uvOffset + new Vector2(1, 0) * new Vector2(tileSizeX, tileSizeY);
                uvs[vi + 2] = uvOffset + new Vector2(0, 1) * new Vector2(tileSizeX, tileSizeY);
                uvs[vi + 3] = uvOffset + new Vector2(1, 1) * new Vector2(tileSizeX, tileSizeY);

                tris[i * 6 + 0] = vi + 0;
                tris[i * 6 + 1] = vi + 1;
                tris[i * 6 + 2] = vi + 2;
                tris[i * 6 + 3] = vi + 2;
                tris[i * 6 + 4] = vi + 1;
                tris[i * 6 + 5] = vi + 3;
            }

            var mesh = new Mesh
            {
                name = $"PreviewCube_{blockName}",
                vertices = verts,
                uv = uvs,
                triangles = tris
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        private void EnsureFaceTiles()
        {
            if (faceTiles != null && faceTiles.Length == 6)
                return;

            faceTiles = new[]
            {
                new Vector2Int(0, 0), // Back
                new Vector2Int(1, 0), // Front
                new Vector2Int(2, 0), // Top
                new Vector2Int(0, 1), // Under
                new Vector2Int(1, 1), // Left
                new Vector2Int(2, 1)  // Right
            };
        }

        private Vector2Int SetMapByID(int index)
        {
            return new Vector2Int(
                blockColumns * (index % (atlasColumns / blockColumns)),
                blockRows * (index / (atlasColumns / blockColumns))
            );
        }

        private Vector2Int SetMapByName(string name)
        {
            int atlasIndex = blockDb != null ? blockDb.GetAtlasIndex(name) : -1;
            if (atlasIndex < 0) atlasIndex = 0;
            return SetMapByID(atlasIndex);
        }

        private static void ClearAtlas(RenderTexture target, Color clearColor)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, clearColor);
            RenderTexture.active = prev;
        }

        private static void ClearSlot(RenderTexture target, Rect viewportRect, Color clearColor)
        {
            if (target == null) return;
            var prev = RenderTexture.active;
            RenderTexture.active = target;
            var pixelRect = new Rect(
                viewportRect.x * target.width,
                viewportRect.y * target.height,
                viewportRect.width * target.width,
                viewportRect.height * target.height
            );
            GL.Viewport(pixelRect);
            GL.Clear(true, true, clearColor);
            GL.Viewport(new Rect(0, 0, target.width, target.height));
            RenderTexture.active = prev;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform)
                SetLayerRecursively(t.gameObject, layer);
        }
    }
}

