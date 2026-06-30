using Sirenix.OdinInspector;
using UnityEngine;
using UniRx;
using System;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;

[RequireComponent(typeof(DamageTargetRegistrant))]
[RequireComponent(typeof(BlockMomentumSpinDriver))]
public partial class ClickableObject : MonoBehaviour, IDamageReceiver, IPointerHitContext, ISpinHitContext
{
    private static readonly Vector3[][] CubeFaces =
    {
        new[] { new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f) }, // Front
        new[] { new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f) }, // Back
        new[] { new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f) }, // Top
        new[] { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f) }, // Bottom
        new[] { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f,  0.5f) }, // Left
        new[] { new Vector3( 0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f, -0.5f), new Vector3( 0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f, -0.5f) }  // Right
    };
    private static readonly int[] CubeTriangles =
    {
         0,  1,  2,  2,  1,  3,
         4,  5,  6,  6,  5,  7,
         8,  9, 10, 10,  9, 11,
        12, 13, 14, 14, 13, 15,
        16, 17, 18, 18, 17, 19,
        20, 21, 22, 22, 21, 23
    };
    private static Camera cachedMainCamera;
    private static int cachedMainCameraFrame = -1;

    [Header("Information")]
    [ReadOnly, SerializeField]
    private string blockName;
    public string BlockName => blockName;
    public float MaxHealth { get; private set; }
    public ReactiveProperty<float> CurrentHealth { get; private set; } = new ReactiveProperty<float>();
    public BlockMomentumSpinDriver MomentumSpinDriver => momentumSpinDriver;
    public int InputPriority => 0;
    public bool CanReceiveDamage =>
        isActiveAndEnabled &&
        MaxHealth > 0f &&
        CurrentHealth != null &&
        CurrentHealth.Value > 0f;
    public float BlockWeight;
    private static readonly int CrackIndexID = Shader.PropertyToID("_CrackIndex");
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ScaleID = Shader.PropertyToID("_Scale");
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly int GlowIntensityID = Shader.PropertyToID("_GlowIntensity");
    private static readonly int EmissionStrengthID = Shader.PropertyToID("_EmissionStrength");
    private static readonly int GlowPowerID = Shader.PropertyToID("_GlowPower");
    private MeshRenderer cubeRenderer;
    private float accumulatedHoldTime = 0f;
    private readonly float timeHoldReset = 0.1f;
    private readonly float timeIdleReset = 1f;
    bool isDyingEffect;
    private bool breakFinalized;

    [Header("Settings")]
    [SerializeField] private int crackLevels = 9;

    [Header("Atlas Settings")]
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

    [Header("Material & Atlas")]
    public Texture2D textureAtlas;
    public UnityEngine.Material cubeMaterial;
    public BlockUVDatabase blockUVDatabase;
    public Texture2D AtlasTexture => textureAtlas;
    public int AtlasColumns => atlasColumns;
    public int AtlasRows => atlasRows;
    public bool AtlasFlipY => flipY;
    [Header("Cracking layer")]
    [SerializeField] private MeshRenderer crackMeshRenderer;
    private MaterialPropertyBlock crackPropertyBlock;
    private MaterialPropertyBlock outlinePropertyBlock;
    private MaterialPropertyBlock baseGlowPropertyBlock;
    [Header("Outline")]
    [SerializeField, Min(0), Tooltip("Material slot index (0-based). Used as fallback when auto-detect cannot find outline material.")]
    private int outlineMaterialIndex = 2;
    [Header("Base Glow (Material Slot 0)")]
    [SerializeField] private bool applyBaseGlowFromOutline = true;
    [Header("Point Light (Optional)")]
    [SerializeField] private bool applyPointLightFromGlow = true;
    [SerializeField] private Light blockPointLight;
    [SerializeField] private bool pointLightUseOutlineColor = true;
    [SerializeField, Min(0f)] private float pointLightMinIntensity = 4f;
    [SerializeField, Min(0f)] private float pointLightMaxIntensity = 15f;
    [SerializeField, Min(0.0001f)] private float pointLightGlowAtMaxIntensity = 1f;
    [SerializeField, Min(0f)] private float pointLightRange = 7f;
    [Header("Animation")]
    [SerializeField] private BlockAnimationController animCtrl;
    [SerializeField, Min(1f)] private float baseBlockScale = 2f;

    [Header("Death Flow - Grow Then Explode")]
    [SerializeField, Range(0.2f, 1f)] private float fullHealthScaleMultiplier = 0.82f;
    [SerializeField, Min(1f)] private float growNearDeathMaxScale = 1.2f;
    [SerializeField, Min(1f)] private float nearDeathGrowthExponent = 2.2f;
    [SerializeField, Min(1f)] private float growThenExplodeBurstScale = 1.32f;
    [SerializeField, Min(0.01f)] private float growThenExplodeBurstDuration = 0.1f;
    [SerializeField] private Ease growThenExplodeBurstEase = Ease.OutBack;
    [Header("Hit SFX Pitch by Health")]
    [SerializeField] private bool scaleHitPitchByRemainingHealth = true;
    [SerializeField, Range(0.1f, 3f)] private float hitPitchAtFullHealth = 1f;
    [SerializeField, Range(0.1f, 3f)] private float hitPitchAtZeroHealth = 1.35f;
    [SerializeField, Min(0.1f)] private float hitPitchCurvePower = 1.35f;

    private Vector2 onClickPos;
    private float blockSpawnTime;
    private bool isReady;
    private Vector3 authoredBaseScale;
    private Vector3 baseAliveScale;
    private Vector3 lastClickWorldPoint;
    private bool hasLastClickWorldPoint;
    private Vector3 lastPointerRayDirection;
    private bool hasLastPointerRayDirection;
    private int lastPointerHitFrame = -1;
    private float lastDamageRatioNormalized;
    private int lastDamageFrame = -1;
    private Mesh generatedCubeMesh;
    private MeshFilter meshFilter;
    private readonly Vector2[] cubeUvBuffer = new Vector2[24];
    private Tween deathFlowTween;
    private BlockMomentumSpinDriver momentumSpinDriver;
    private bool warnedMissingPointLight;
    private Color currentOutlineColor = Color.black;

    private CompositeDisposable runtimeSubs;
    void Awake()
    {
        crackPropertyBlock = new MaterialPropertyBlock();
        outlinePropertyBlock = new MaterialPropertyBlock();
        baseGlowPropertyBlock = new MaterialPropertyBlock();
        cubeRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        momentumSpinDriver = GetComponent<BlockMomentumSpinDriver>();
        if (blockPointLight == null)
            blockPointLight = GetComponentInChildren<Light>(true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (momentumSpinDriver == null)
            Debug.LogError("[ClickableObject] Missing BlockMomentumSpinDriver. Add it on prefab/scene.", this);
#endif
        if (animCtrl == null) animCtrl = GetComponent<BlockAnimationController>();
        authoredBaseScale = transform.localScale;
        baseAliveScale = Vector3.one * baseBlockScale;
    }

    void OnEnable()
    {
        if (isReady)
            ListenRuntime();
    }

    void OnDisable()
    {
        KillDeathFlowTween();
        runtimeSubs?.Dispose();
        runtimeSubs = null;

        // If pooled/disabled during death, finalize once so discovery + drops still fire.
        if (isDyingEffect && !breakFinalized)
            FinalizeBreak();
    }

    void OnDestroy()
    {
        if (generatedCubeMesh != null)
        {
            Destroy(generatedCubeMesh);
            generatedCubeMesh = null;
        }
    }

    #region SETUP ---------------------------------------------------------------------------------------------
    void ListenRuntime()
    {
        runtimeSubs?.Dispose();
        runtimeSubs = new CompositeDisposable();

        // Cracking listen
        CurrentHealth
            .DistinctUntilChanged()
            .Subscribe(newHealth =>
            {
                UpdateCrackVisual(newHealth);
                if (newHealth < MaxHealth)
                    PlayHittingSound();
                if (newHealth <= 0f)
                    OnDisappear();
            })
            .AddTo(runtimeSubs);
    }

    public void SetClickableBlock(string name)
    {
        blockName = name;
        DataSaver.Ins.currentBlock = name;
        CacheClickVfxOutlineColor();
        MaxHealth = blockUVDatabase.GetHealth(name);
        CurrentHealth.Value = blockUVDatabase.GetHealth(name);
        BlockWeight = blockUVDatabase.GetWeight(name);
        isDyingEffect = false;
        breakFinalized = false;
        accumulatedHoldTime = 0f;
        isReady = true;
        ListenRuntime();
        GenerateCube();
        ApplyOutlineColorFromDatabase();
        ApplyBaseGlowFromDatabase();
        ApplyPointLightFromDatabase();
        OnAppear();
    }
    public void SetClickableBlockByCondition(BlockSpawnLocation blockSpawnLocation, TimeState timeState, NormalWeatherName normalWeatherName, SpecialWeatherName specialWeatherName)
    {
        SetClickableBlock(
            blockUVDatabase.GetRandomBlockByConditions(
            blockSpawnLocation,
            timeState,
            normalWeatherName,
            specialWeatherName,
            StatsManager.Ins.Get(StatType.Lucky)).blockName
        );
    }
    #endregion
    #region CLICK_LOGIC -------------------------------------------------------------------------------------
    /// <summary>
    /// Convert world pos -> RectTransform anchored position
    /// </summary>
    private Vector2 GetUIPosition(Camera cam, Vector3 worldPos)
    {
        Vector2 screenPos = cam.WorldToScreenPoint(worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            Toaster.Ins.canvas.transform as RectTransform,
            screenPos,
            Toaster.Ins.canvas.worldCamera,
            out Vector2 localPoint
        );
        return localPoint;
    }

    public void SetPointerHit(Vector3 worldPoint)
    {
        var cam = ResolveMainCamera();
        if (cam == null)
            return;

        onClickPos = GetUIPosition(cam, worldPoint);
        lastClickWorldPoint = worldPoint;
        hasLastClickWorldPoint = true;
        Vector3 rayLikeDirection = worldPoint - cam.transform.position;
        if (rayLikeDirection.sqrMagnitude <= 0.000001f)
        {
            hasLastPointerRayDirection = false;
            return;
        }

        lastPointerRayDirection = rayLikeDirection.normalized;
        hasLastPointerRayDirection = true;
        lastPointerHitFrame = Time.frameCount;
    }

    public bool TryGetPointerScreenDirectionFromCenter(out Vector2 direction, out float distancePx, int maxAgeFrames = -1)
    {
        direction = Vector2.zero;
        distancePx = 0f;

        if (!HasPointerHitWithinFrames(maxAgeFrames))
            return false;

        var cam = ResolveMainCamera();
        if (cam == null)
            return false;

        Vector3 centerScreen = cam.WorldToScreenPoint(transform.position);
        Vector3 hitScreen = cam.WorldToScreenPoint(lastClickWorldPoint);
        if (centerScreen.z <= 0f || hitScreen.z <= 0f)
            return false;

        Vector2 delta = new Vector2(hitScreen.x - centerScreen.x, hitScreen.y - centerScreen.y);
        distancePx = delta.magnitude;
        if (distancePx <= 0.0001f)
            return false;

        direction = delta / distancePx;
        return true;
    }

    public bool TryGetPointerTorqueWorldAxis(out Vector3 worldAxis, int maxAgeFrames = -1)
    {
        worldAxis = Vector3.zero;
        if (!HasPointerHitWithinFrames(maxAgeFrames))
            return false;

        if (TryGetCameraCenterTorqueWorldAxis(out worldAxis, maxAgeFrames))
            return true;

        // Physical torque from a ray "push": tau = r x F
        // r: vector from center of mass to hit point, F: ray direction into scene.
        if (!hasLastPointerRayDirection || lastPointerRayDirection.sqrMagnitude <= 0.000001f)
            return false;

        Vector3 forceDir = lastPointerRayDirection;

        Vector3 r = lastClickWorldPoint - transform.position;
        if (r.sqrMagnitude <= 0.000001f)
            return false;

        Vector3 torqueWorld = Vector3.Cross(r, forceDir);
        if (torqueWorld.sqrMagnitude <= 0.000001f)
            return false;

        worldAxis = torqueWorld.normalized;
        return true;
    }

    bool TryGetCameraCenterTorqueWorldAxis(out Vector3 worldAxis, int maxAgeFrames)
    {
        worldAxis = Vector3.zero;

        if (!TryGetPointerScreenDirectionFromCenter(out Vector2 screenDirection, out _, maxAgeFrames))
            return false;

        var cam = ResolveMainCamera();
        if (cam == null)
            return false;

        Vector3 screenOffsetWorld =
            cam.transform.right * screenDirection.x +
            cam.transform.up * screenDirection.y;

        if (screenOffsetWorld.sqrMagnitude <= 0.000001f)
            return false;

        Vector3 torqueWorld = Vector3.Cross(screenOffsetWorld.normalized, cam.transform.forward);
        if (torqueWorld.sqrMagnitude <= 0.000001f)
            return false;

        worldAxis = torqueWorld.normalized;
        return true;
    }

    private bool HasPointerHitWithinFrames(int maxAgeFrames)
    {
        if (!hasLastClickWorldPoint || lastPointerHitFrame < 0)
            return false;

        if (maxAgeFrames < 0)
            return true;

        return Time.frameCount - lastPointerHitFrame <= maxAgeFrames;
    }

    public bool TryGetRecentDamageRatioNormalized(out float ratio01, int maxAgeFrames = 2)
    {
        ratio01 = 0f;

        if (lastDamageFrame < 0)
            return false;

        if (maxAgeFrames >= 0 && Time.frameCount - lastDamageFrame > maxAgeFrames)
            return false;

        ratio01 = Mathf.Clamp01(lastDamageRatioNormalized);
        return true;
    }

    #endregion

    #region CUBE_ANIM -------------------------------------------------------------------------------------
    

    #endregion

    #region TEXTURE -------------------------------------------------------------------------------------
    void GenerateCube()
    {
        if (faceTiles.Length != 6)
        {
            Debug.LogError("Please assign 6 tile coordinates (one per face).");
            return;
        }

        EnsureGeneratedCubeMesh();
        UpdateCubeUv();

        if (cubeRenderer == null) cubeRenderer = gameObject.AddComponent<MeshRenderer>();
        ApplyBaseMaterialPreserveSlots();

        if (cubeMaterial != null && textureAtlas != null && cubeMaterial.mainTexture != textureAtlas)
        {
            cubeMaterial.mainTexture = textureAtlas;
        }
    }

    void ApplyBaseMaterialPreserveSlots()
    {
        if (cubeRenderer == null || cubeMaterial == null)
            return;

        var mats = cubeRenderer.sharedMaterials;
        if (mats == null || mats.Length == 0)
        {
            cubeRenderer.sharedMaterial = cubeMaterial;
            return;
        }

        if (mats[0] == cubeMaterial)
            return;

        mats[0] = cubeMaterial;
        cubeRenderer.sharedMaterials = mats;
    }

    void ApplyOutlineColorFromDatabase()
    {
        if (cubeRenderer == null || blockUVDatabase == null)
            return;

        if (!TryGetOutlineSlot(out int slotIndex, out var outlineMat))
            return;

        Color outlineColor = currentOutlineColor;
        float glowIntensity = Mathf.Max(1f, blockUVDatabase.GetGlowIntensity(blockName));
        cubeRenderer.GetPropertyBlock(outlinePropertyBlock, slotIndex);
        if (TryGetColorPropertyId(outlineMat, out int colorPropertyId))
            outlinePropertyBlock.SetColor(colorPropertyId, outlineColor);
        if (outlineMat != null && outlineMat.HasProperty(ScaleID))
        {
            float baseScale = outlineMat.GetFloat(ScaleID);
            float appliedScale = outlineColor.a <= 0.001f ? 0f : baseScale;
            outlinePropertyBlock.SetFloat(ScaleID, appliedScale);
        }
        if (TryGetGlowPropertyId(outlineMat, out int glowPropertyId))
            outlinePropertyBlock.SetFloat(glowPropertyId, glowIntensity);
        cubeRenderer.SetPropertyBlock(outlinePropertyBlock, slotIndex);
    }

    void CacheClickVfxOutlineColor()
    {
        currentOutlineColor = blockUVDatabase != null
            ? blockUVDatabase.GetOutlineColor(blockName)
            : Color.black;
    }

    void ApplyBaseGlowFromDatabase()
    {
        if (cubeRenderer == null)
            return;

        var mats = cubeRenderer.sharedMaterials;
        if (mats == null || mats.Length == 0)
            return;

        var baseMat = mats[0];
        if (baseMat == null || !baseMat.HasProperty(EmissionColorID))
            return;

        Color emission = Color.black;
        if (applyBaseGlowFromOutline && blockUVDatabase != null)
        {
            Color tint = blockUVDatabase.GetOutlineColor(blockName);
            float dbIntensity = blockUVDatabase.GetGlowIntensity(blockName);
            if (Mathf.Approximately(dbIntensity, 1f))
                dbIntensity = 0f;

            float strength = Mathf.Clamp(dbIntensity, 0f, 0.35f);
            emission = new Color(tint.r * strength, tint.g * strength, tint.b * strength, 1f);

            if (strength > 0.0001f)
                baseMat.EnableKeyword("_EMISSION");
            else
                baseMat.DisableKeyword("_EMISSION");
        }
        else
            baseMat.DisableKeyword("_EMISSION");

        cubeRenderer.GetPropertyBlock(baseGlowPropertyBlock, 0);
        baseGlowPropertyBlock.SetColor(EmissionColorID, emission);
        cubeRenderer.SetPropertyBlock(baseGlowPropertyBlock, 0);
    }

    void ApplyPointLightFromDatabase()
    {
        if (blockPointLight == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (applyPointLightFromGlow && !warnedMissingPointLight)
            {
                warnedMissingPointLight = true;
                Debug.LogWarning("[ClickableObject] Point Light is not assigned. Assign one (or child Light) to use per-block glow lighting.", this);
            }
#endif
            return;
        }

        if (!applyPointLightFromGlow || blockUVDatabase == null)
        {
            blockPointLight.intensity = 0f;
            return;
        }

        float glow = Mathf.Max(0f, blockUVDatabase.GetGlowIntensity(blockName));
        float maxGlow = Mathf.Max(0.0001f, pointLightGlowAtMaxIntensity);
        float t = Mathf.Clamp01(glow / maxGlow);

        blockPointLight.intensity = glow <= 0f
            ? 0f
            : Mathf.Lerp(pointLightMinIntensity, pointLightMaxIntensity, t);

        blockPointLight.range = Mathf.Max(0f, pointLightRange);

        if (pointLightUseOutlineColor)
        {
            Color c = blockUVDatabase.GetOutlineColor(blockName);
            c.a = 1f;
            blockPointLight.color = c;
        }
    }

    bool TryGetOutlineSlot(out int slotIndex, out Material outlineMaterial)
    {
        slotIndex = -1;
        outlineMaterial = null;

        if (cubeRenderer == null)
            return false;

        var mats = cubeRenderer.sharedMaterials;
        if (mats == null || mats.Length == 0)
            return false;

        for (int i = 0; i < mats.Length; i++)
        {
            var mat = mats[i];
            if (mat == null)
                continue;

            string shaderName = mat.shader != null ? mat.shader.name : string.Empty;
            bool looksLikeOutline =
                mat.name.IndexOf("outline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderName.IndexOf("outline", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!looksLikeOutline)
                continue;

            if (TryGetColorPropertyId(mat, out _))
            {
                slotIndex = i;
                outlineMaterial = mat;
                return true;
            }
        }

        if (outlineMaterialIndex >= 0 &&
            outlineMaterialIndex < mats.Length &&
            TryGetColorPropertyId(mats[outlineMaterialIndex], out _))
        {
            slotIndex = outlineMaterialIndex;
            outlineMaterial = mats[outlineMaterialIndex];
            return true;
        }

        return false;
    }

    bool TryGetColorPropertyId(Material mat, out int colorPropertyId)
    {
        colorPropertyId = ColorID;

        if (mat == null)
            return false;

        if (mat.HasProperty(ColorID))
        {
            colorPropertyId = ColorID;
            return true;
        }

        if (mat.HasProperty(BaseColorID))
        {
            colorPropertyId = BaseColorID;
            return true;
        }

        return false;
    }

    bool TryGetGlowPropertyId(Material mat, out int glowPropertyId)
    {
        glowPropertyId = GlowIntensityID;

        if (mat == null)
            return false;

        if (mat.HasProperty(GlowIntensityID))
        {
            glowPropertyId = GlowIntensityID;
            return true;
        }

        if (mat.HasProperty(EmissionStrengthID))
        {
            glowPropertyId = EmissionStrengthID;
            return true;
        }

        if (mat.HasProperty(GlowPowerID))
        {
            glowPropertyId = GlowPowerID;
            return true;
        }

        return false;
    }

    void EnsureGeneratedCubeMesh()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();

        if (generatedCubeMesh == null)
        {
            generatedCubeMesh = new Mesh { name = "GeneratedCube" };

            var verts = new Vector3[24];
            for (int face = 0; face < CubeFaces.Length; face++)
            {
                int vi = face * 4;
                verts[vi + 0] = CubeFaces[face][0];
                verts[vi + 1] = CubeFaces[face][1];
                verts[vi + 2] = CubeFaces[face][2];
                verts[vi + 3] = CubeFaces[face][3];
            }

            generatedCubeMesh.vertices = verts;
            generatedCubeMesh.triangles = CubeTriangles;
            generatedCubeMesh.RecalculateNormals();
        }

        if (meshFilter.sharedMesh != generatedCubeMesh)
            meshFilter.sharedMesh = generatedCubeMesh;
    }

    void UpdateCubeUv()
    {
        if (generatedCubeMesh == null)
            return;

        float tileSizeX = 1f / atlasColumns;
        float tileSizeY = 1f / atlasRows;
        Vector2 tileScale = new Vector2(tileSizeX, tileSizeY);
        Vector2Int mapOffset = SetMapByName(blockName);

        for (int i = 0; i < 6; i++)
        {
            int vi = i * 4;
            Vector2Int tile = faceTiles[i] + mapOffset;
            if (flipY)
                tile.y = atlasRows - 1 - tile.y;

            Vector2 uvOffset = new Vector2(tile.x * tileSizeX, tile.y * tileSizeY);

            cubeUvBuffer[vi + 0] = uvOffset + Vector2.Scale(new Vector2(0f, 0f), tileScale);
            cubeUvBuffer[vi + 1] = uvOffset + Vector2.Scale(new Vector2(1f, 0f), tileScale);
            cubeUvBuffer[vi + 2] = uvOffset + Vector2.Scale(new Vector2(0f, 1f), tileScale);
            cubeUvBuffer[vi + 3] = uvOffset + Vector2.Scale(new Vector2(1f, 1f), tileScale);
        }

        generatedCubeMesh.uv = cubeUvBuffer;
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
        return SetMapByID(blockUVDatabase.GetAtlasIndex(name));
    }

    public bool TryGetRandomFaceTile(out Vector2Int tile)
    {
        tile = Vector2Int.zero;

        if (faceTiles == null || faceTiles.Length == 0)
            return false;

        int faceIndex = UnityEngine.Random.Range(0, faceTiles.Length);
        tile = faceTiles[faceIndex] + SetMapByName(blockName);
        return true;
    }

    #endregion
    #region HELPER --------------------------------------------------------------------------------------------
    static Camera ResolveMainCamera()
    {
        if (cachedMainCamera != null && cachedMainCamera.isActiveAndEnabled)
            return cachedMainCamera;

        if (cachedMainCameraFrame == Time.frameCount)
            return cachedMainCamera;

        cachedMainCameraFrame = Time.frameCount;
        cachedMainCamera = Camera.main;
        return cachedMainCamera;
    }

    #endregion
}







