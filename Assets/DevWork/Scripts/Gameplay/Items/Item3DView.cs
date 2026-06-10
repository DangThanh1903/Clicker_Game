using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Item3DView : MonoBehaviour
{
    private static readonly Material[] EmptyMaterials = new Material[0];
    private const string ShadowCasterName = "ShadowCaster";

    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Material defaultSideMaterial;
    [SerializeField] private Material shadowCasterMaterial;

    private MeshFilter shadowMeshFilter;
    private MeshRenderer shadowMeshRenderer;
    private Material[] visualMaterials = EmptyMaterials;
    private Material[] shadowMaterials = EmptyMaterials;

    public MeshFilter MeshFilter
    {
        get
        {
            CacheComponents();
            return meshFilter;
        }
    }

    public MeshRenderer MeshRenderer
    {
        get
        {
            CacheComponents();
            return meshRenderer;
        }
    }

    private void Awake()
    {
        CacheComponents();
        ConfigureRenderer();
    }

    private void Reset()
    {
        CacheComponents();
        ConfigureRenderer();
    }

    private void OnValidate()
    {
        CacheComponents();
        ConfigureRenderer();
    }

    public void SetVisual(Mesh mesh, Material frontMaterial, Material sideMaterial = null)
    {
        CacheComponents();

        if (meshFilter != null)
            meshFilter.sharedMesh = mesh;

        if (meshRenderer == null)
            return;

        if (mesh == null || frontMaterial == null)
        {
            meshRenderer.sharedMaterials = EmptyMaterials;
            ClearShadowVisual();
            return;
        }

        Material resolvedSideMaterial = sideMaterial != null ? sideMaterial : defaultSideMaterial;
        int materialCount = resolvedSideMaterial != null ? 2 : 1;
        if (visualMaterials == null || visualMaterials.Length != materialCount)
            visualMaterials = new Material[materialCount];

        visualMaterials[0] = frontMaterial;
        if (materialCount > 1)
            visualMaterials[1] = resolvedSideMaterial;

        meshRenderer.sharedMaterials = visualMaterials;

        SetShadowVisual(mesh);
    }

    public void ClearVisual()
    {
        CacheComponents();

        if (meshFilter != null)
            meshFilter.sharedMesh = null;
        if (meshRenderer != null)
            meshRenderer.sharedMaterials = EmptyMaterials;
        ClearShadowVisual();
    }

    public void SetDefaultSideMaterial(Material material)
    {
        defaultSideMaterial = material;
    }

    private void CacheComponents()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
    }

    private void ConfigureRenderer()
    {
        if (meshRenderer == null)
            return;

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        if (shadowMeshRenderer != null)
            ConfigureShadowRenderer();
    }

    private void SetShadowVisual(Mesh mesh)
    {
        if (mesh == null || shadowCasterMaterial == null)
        {
            ClearShadowVisual();
            return;
        }

        EnsureShadowRenderer();

        if (shadowMeshFilter == null || shadowMeshRenderer == null)
            return;

        shadowMeshFilter.sharedMesh = mesh;

        int materialCount = Mathf.Max(1, mesh.subMeshCount);
        if (shadowMaterials == null || shadowMaterials.Length != materialCount)
            shadowMaterials = new Material[materialCount];

        for (int i = 0; i < materialCount; i++)
            shadowMaterials[i] = shadowCasterMaterial;

        shadowMeshRenderer.sharedMaterials = shadowMaterials;
        shadowMeshRenderer.enabled = true;
    }

    private void ClearShadowVisual()
    {
        if (shadowMeshFilter != null)
            shadowMeshFilter.sharedMesh = null;

        if (shadowMeshRenderer != null)
        {
            shadowMeshRenderer.sharedMaterials = EmptyMaterials;
            shadowMeshRenderer.enabled = false;
        }
    }

    private void EnsureShadowRenderer()
    {
        if (shadowMeshFilter != null && shadowMeshRenderer != null)
            return;

        Transform shadowChild = transform.Find(ShadowCasterName);
        if (shadowChild == null)
        {
            var shadowObject = new GameObject(ShadowCasterName);
            shadowChild = shadowObject.transform;
            shadowChild.SetParent(transform, false);
        }

        shadowChild.gameObject.layer = gameObject.layer;
        shadowMeshFilter = shadowChild.GetComponent<MeshFilter>();
        if (shadowMeshFilter == null)
            shadowMeshFilter = shadowChild.gameObject.AddComponent<MeshFilter>();

        shadowMeshRenderer = shadowChild.GetComponent<MeshRenderer>();
        if (shadowMeshRenderer == null)
            shadowMeshRenderer = shadowChild.gameObject.AddComponent<MeshRenderer>();

        ConfigureShadowRenderer();
    }

    private void ConfigureShadowRenderer()
    {
        if (shadowMeshRenderer == null)
            return;

        shadowMeshRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        shadowMeshRenderer.receiveShadows = false;
        shadowMeshRenderer.lightProbeUsage = LightProbeUsage.Off;
        shadowMeshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        shadowMeshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        shadowMeshRenderer.renderingLayerMask = meshRenderer != null ? meshRenderer.renderingLayerMask : 1u;
    }
}
