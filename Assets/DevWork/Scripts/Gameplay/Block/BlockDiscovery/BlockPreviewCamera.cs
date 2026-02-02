using System.Collections.Generic;
using UnityEngine;
#if UNITY_RENDER_PIPELINE_URP
using UnityEngine.Rendering;
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
        [SerializeField] private Vector3 previewLightRotation = new Vector3(50f, -30f, 0f);
        [SerializeField] private float previewLightRange = 10f;

        private readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        private readonly Stack<PreviewInstance> instancePool = new Stack<PreviewInstance>();
        private readonly List<PreviewInstance> activeInstances = new List<PreviewInstance>();
        private readonly Stack<int> freeSlots = new Stack<int>();
        private bool warnedMissingCamera;
        private bool warnedMissingDatabase;
        private bool warnedMissingMaterial;
        private bool warnedMissingAtlas;
        private bool warnedMissingPreviewAtlas;
        private RenderTexture previewAtlas;
        private int previewColumns;
        private int previewRows;
        private int nextSlotIndex;
        private Light previewLight;

#if UNITY_RENDER_PIPELINE_URP
        private struct RenderRequest
        {
            public PreviewInstance instance;
            public string blockName;
            public RenderTexture target;
            public Rect viewportRect;
            public float aspect;
        }
        private readonly Queue<RenderRequest> renderQueue = new Queue<RenderRequest>();
#endif

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

        void Awake()
        {
            EnsurePreviewObjects();
        }

        private void OnEnable()
        {
#if UNITY_RENDER_PIPELINE_URP
            RenderPipelineManager.endFrameRendering += OnEndFrameRendering;
#endif
        }

        private void OnDisable()
        {
#if UNITY_RENDER_PIPELINE_URP
            RenderPipelineManager.endFrameRendering -= OnEndFrameRendering;
#endif
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
#if UNITY_RENDER_PIPELINE_URP
            renderQueue.Clear();
#endif
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
        }

        public void RenderBlock(PreviewInstance instance, string blockName, PreviewSlot slot)
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

            EnsurePreviewAtlas();
            if (previewAtlas == null) return;

#if UNITY_RENDER_PIPELINE_URP
            float cellWidth = slot.viewportRect.width * previewAtlas.width;
            float cellHeight = slot.viewportRect.height * previewAtlas.height;
            renderQueue.Enqueue(new RenderRequest
            {
                instance = instance,
                blockName = blockName,
                target = previewAtlas,
                viewportRect = slot.viewportRect,
                aspect = Mathf.Approximately(cellHeight, 0f) ? 1f : cellWidth / cellHeight
            });
            return;
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

            SetActiveInstance(instance);
            ClearSlot(previewAtlas, slot.viewportRect);
            previewCamera.targetTexture = previewAtlas;
            previewCamera.Render();
            previewCamera.targetTexture = null;
            previewCamera.rect = prevRect;
            previewCamera.aspect = prevAspect;
            HideInstance(instance);
        }

#if UNITY_RENDER_PIPELINE_URP
        private void OnEndFrameRendering(ScriptableRenderContext context, Camera[] cameras)
        {
            if (renderQueue.Count == 0) return;
            if (!TryEnsureCamera()) return;
            if (blockDb == null) return;

            EnsurePreviewObjects();

            var prevRect = previewCamera.rect;
            var prevAspect = previewCamera.aspect;
            int processed = 0;
            int limit = Mathf.Max(1, maxRendersPerFrame);

            while (renderQueue.Count > 0 && processed < limit)
            {
                var req = renderQueue.Dequeue();
                if (req.instance == null || req.target == null) continue;
                processed++;

                req.instance.meshFilter.sharedMesh = GetMesh(req.blockName);
                if (req.instance.meshRenderer != null && req.instance.meshRenderer.sharedMaterial != null && atlasTexture != null)
                    req.instance.meshRenderer.sharedMaterial.mainTexture = atlasTexture;

                previewCamera.rect = req.viewportRect;
                previewCamera.aspect = req.aspect;

                SetActiveInstance(req.instance);
                ClearSlot(req.target, req.viewportRect);
                previewCamera.targetTexture = req.target;
                UniversalRenderPipeline.RenderSingleCamera(context, previewCamera);
                previewCamera.targetTexture = null;
                HideInstance(req.instance);
            }

            previewCamera.rect = prevRect;
            previewCamera.aspect = prevAspect;
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
                previewCamera.clearFlags = CameraClearFlags.Nothing;
                previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
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

                ClearAtlas(previewAtlas);
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
            if (previewCamera == null)
                previewCamera = GetComponentInChildren<Camera>(true);
            if (previewCamera == null)
                previewCamera = GetComponent<Camera>();
            return previewCamera != null;
        }

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
            previewLight.intensity = previewLightIntensity;
            previewLight.range = previewLightRange;
            previewLight.shadows = LightShadows.None;
            previewLight.transform.localRotation = Quaternion.Euler(previewLightRotation);
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

        private static void ClearAtlas(RenderTexture target)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
            RenderTexture.active = prev;
        }

        private static void ClearSlot(RenderTexture target, Rect viewportRect)
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
            GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
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
