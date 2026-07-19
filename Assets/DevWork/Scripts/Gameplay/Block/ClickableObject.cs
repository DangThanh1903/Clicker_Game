using Sirenix.OdinInspector;
using UnityEngine;
using UniRx;
using System;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;
using UnityEngine.Rendering;

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
    private static readonly int BaseMapID = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
    private static readonly int EmissionMapID = Shader.PropertyToID("_EmissionMap");
    private static readonly int ScaleID = Shader.PropertyToID("_Scale");
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private static readonly int GlowIntensityID = Shader.PropertyToID("_GlowIntensity");
    private static readonly int EmissionStrengthID = Shader.PropertyToID("_EmissionStrength");
    private static readonly int GlowPowerID = Shader.PropertyToID("_GlowPower");
    private static readonly int SurfaceID = Shader.PropertyToID("_Surface");
    private static readonly int BlendID = Shader.PropertyToID("_Blend");
    private static readonly int SrcBlendID = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendID = Shader.PropertyToID("_DstBlend");
    private static readonly int SrcBlendAlphaID = Shader.PropertyToID("_SrcBlendAlpha");
    private static readonly int DstBlendAlphaID = Shader.PropertyToID("_DstBlendAlpha");
    private static readonly int ZWriteID = Shader.PropertyToID("_ZWrite");
    private static readonly int AlphaClipID = Shader.PropertyToID("_AlphaClip");
    private static readonly int CutoffID = Shader.PropertyToID("_Cutoff");
    private static readonly int CullID = Shader.PropertyToID("_Cull");
    private MeshRenderer cubeRenderer;
    private float accumulatedHoldTime = 0f;
    private readonly float timeHoldReset = 0.1f;
    private readonly float timeIdleReset = 1f;
    bool isDyingEffect;
    private bool breakFinalized;

    [Header("Shared Config")]
    [SerializeField] private ClickableBlockConfig config;
    public ClickableBlockConfig Config => config;

    private int crackLevels => config != null ? config.CrackLevels : 9;
    private int atlasColumns => config != null ? config.AtlasColumns : 6;
    private int atlasRows => config != null ? config.AtlasRows : 10;
    private int blockColumns => config != null ? config.BlockColumns : 3;
    private int blockRows => config != null ? config.BlockRows : 2;
    private bool flipY => config == null || config.FlipY;
    private Texture2D textureAtlas => config != null ? config.TextureAtlas : null;
    private Texture2D emissionAtlas => config != null ? config.EmissionAtlas : null;
    private Material cubeMaterial => config != null ? config.CubeMaterial : null;
    public BlockUVDatabase blockUVDatabase => config != null ? config.BlockUVDatabase : null;
    public BlockUVDatabase BlockDatabase => blockUVDatabase;
    public Texture2D AtlasTexture => textureAtlas;
    public Texture2D EmissionAtlasTexture => ResolveEmissionAtlasTexture();
    public int AtlasColumns => atlasColumns;
    public int AtlasRows => atlasRows;
    public bool AtlasFlipY => flipY;
    private int outlineMaterialIndex => config != null ? config.OutlineMaterialIndex : 2;
    private bool applyBaseGlowFromOutline => config == null || config.ApplyBaseGlowFromOutline;
    private bool useEmissionOverlay => config == null || config.UseEmissionOverlay;
    private float emissionOverlayScale => config != null ? config.EmissionOverlayScale : 1.001f;
    private float emissionOverlayIntensityScale => config != null ? config.EmissionOverlayIntensityScale : 0.15f;
    private float emissionOverlayBonus => config != null ? config.EmissionOverlayBonus : 0.1f;
    private float emissionOverlayGlowBoost => config != null ? config.EmissionOverlayGlowBoost : 2.5f;
    private float emissionOverlayCutoff => config != null ? config.EmissionOverlayCutoff : 0.01f;
    private bool applyPointLightFromGlow => config == null || config.ApplyPointLightFromGlow;
    private bool pointLightUseOutlineColor => config == null || config.PointLightUseOutlineColor;
    private float pointLightMinIntensity => config != null ? config.PointLightMinIntensity : 4f;
    private float pointLightMaxIntensity => config != null ? config.PointLightMaxIntensity : 15f;
    private float pointLightGlowAtMaxIntensity => config != null ? config.PointLightGlowAtMaxIntensity : 1f;
    private float pointLightRange => config != null ? config.PointLightRange : 7f;
    private bool enableAura => config == null || config.EnableAura;
    private float auraWeightThreshold => config != null ? config.AuraWeightThreshold : 10f;
    private float auraMinIntensity => config != null ? config.AuraMinIntensity : 1f;
    private float auraMaxIntensity => config != null ? config.AuraMaxIntensity : 2f;
    private float auraGlowAtMaxIntensity => config != null ? config.AuraGlowAtMaxIntensity : 4f;
    private float auraMinimumVisibleColor => config != null ? config.AuraMinimumVisibleColor : 0.05f;
    private float baseBlockScale => config != null ? config.BaseBlockScale : 2f;
    private float fullHealthScaleMultiplier => config != null ? config.FullHealthScaleMultiplier : 0.82f;
    private float growNearDeathMaxScale => config != null ? config.GrowNearDeathMaxScale : 1.2f;
    private float nearDeathGrowthExponent => config != null ? config.NearDeathGrowthExponent : 2.2f;
    private float growThenExplodeBurstScale => config != null ? config.GrowThenExplodeBurstScale : 1.32f;
    private float growThenExplodeBurstDuration => config != null ? config.GrowThenExplodeBurstDuration : 0.1f;
    private Ease growThenExplodeBurstEase => config != null ? config.GrowThenExplodeBurstEase : Ease.OutBack;
    private bool scaleHitPitchByRemainingHealth => config == null || config.ScaleHitPitchByRemainingHealth;
    private float hitPitchAtFullHealth => config != null ? config.HitPitchAtFullHealth : 1f;
    private float hitPitchAtZeroHealth => config != null ? config.HitPitchAtZeroHealth : 1.35f;
    private float hitPitchCurvePower => config != null ? config.HitPitchCurvePower : 1.35f;

    private readonly Vector2Int[] faceTiles =
    {
        new Vector2Int(0, 0), // Back
        new Vector2Int(1, 0), // Front
        new Vector2Int(2, 0), // Top
        new Vector2Int(0, 1), // Under
        new Vector2Int(1, 1), // Left
        new Vector2Int(2, 1)  // Right
    };
    [Header("Cracking Layer")]
    [SerializeField] private MeshRenderer crackMeshRenderer;
    private MaterialPropertyBlock crackPropertyBlock;
    private MaterialPropertyBlock outlinePropertyBlock;
    private MaterialPropertyBlock baseGlowPropertyBlock;
    [Header("Scene References")]
    [SerializeField] private Light blockPointLight;
    [SerializeField] private BlockAuraController auraView;
    [SerializeField] private BlockAnimationController animCtrl;

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
    private MeshFilter emissionOverlayMeshFilter;
    private MeshRenderer emissionOverlayRenderer;
    private Material emissionOverlayMaterial;
    private readonly Vector2[] cubeUvBuffer = new Vector2[24];
    private Tween deathFlowTween;
    private BlockMomentumSpinDriver momentumSpinDriver;
    private bool warnedMissingPointLight;
    private bool warnedMissingBaseTextureProperty;
    private bool warnedMissingEmissionMapProperty;
    private bool warnedMissingEmissionColorProperty;
    private bool warnedInvalidPointLight;
    private bool warnedMissingEmissionOverlayShader;
    private Color currentOutlineColor = Color.black;
    private MaterialPropertyBlock emissionOverlayPropertyBlock;
    private CompositeDisposable runtimeSubs;
    void Awake()
    {
        crackPropertyBlock = new MaterialPropertyBlock();
        outlinePropertyBlock = new MaterialPropertyBlock();
        baseGlowPropertyBlock = new MaterialPropertyBlock();
        emissionOverlayPropertyBlock = new MaterialPropertyBlock();
        cubeRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        momentumSpinDriver = GetComponent<BlockMomentumSpinDriver>();
        blockPointLight = ResolveBlockPointLight(blockPointLight);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (config == null)
            Debug.LogError("[ClickableObject] Missing ClickableBlockConfig.", this);
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
        StopAura();
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

        if (emissionOverlayMaterial != null)
        {
            Destroy(emissionOverlayMaterial);
            emissionOverlayMaterial = null;
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
        UpdateAuraFromBlock();
        OnAppear();
    }
    public void SetClickableBlockByCondition(BlockSpawnLocation blockSpawnLocation, TimeState timeState, NormalWeatherName normalWeatherName, SpecialWeatherName specialWeatherName)
    {
        BlockUVEntry entry = blockUVDatabase.GetRandomBlockByConditions(
            blockSpawnLocation,
            timeState,
            normalWeatherName,
            specialWeatherName,
            StatsManager.Ins.Get(StatType.Lucky));

        if (entry == null || string.IsNullOrWhiteSpace(entry.blockName))
        {
            Debug.LogWarning($"[ClickableObject] No valid block found for {blockSpawnLocation}/{timeState}/{normalWeatherName}/{specialWeatherName}. Falling back to Dirt.", this);
            SetClickableBlock("Dirt");
            return;
        }

        SetClickableBlock(entry.blockName);
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
        ApplyBaseMaterialTextures();
        SyncEmissionOverlayMesh();
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

    void ApplyBaseMaterialTextures()
    {
        if (!TryGetBaseMaterial(out var baseMat))
            return;

        Texture2D resolvedEmissionAtlas = ResolveEmissionAtlasTexture();

        cubeRenderer.GetPropertyBlock(baseGlowPropertyBlock, 0);

        bool hasBaseTextureProperty = false;
        if (baseMat.HasProperty(BaseMapID))
        {
            baseGlowPropertyBlock.SetTexture(BaseMapID, textureAtlas);
            hasBaseTextureProperty = true;
        }

        if (baseMat.HasProperty(MainTexID))
        {
            baseGlowPropertyBlock.SetTexture(MainTexID, textureAtlas);
            hasBaseTextureProperty = true;
        }

        if (!hasBaseTextureProperty)
            WarnMissingBaseTextureProperty();

        if (baseMat.HasProperty(EmissionMapID))
        {
            baseGlowPropertyBlock.SetTexture(EmissionMapID, resolvedEmissionAtlas);
        }
        else if (resolvedEmissionAtlas != null)
        {
            WarnMissingEmissionMapProperty();
        }

        cubeRenderer.SetPropertyBlock(baseGlowPropertyBlock, 0);
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
        if (!TryGetBaseMaterial(out var baseMat))
            return;

        ApplyBaseMaterialTextures();

        if (ShouldUseEmissionOverlay())
        {
            DisableBaseMaterialEmission(baseMat);
            ApplyEmissionOverlayFromDatabase();
            return;
        }

        SetEmissionOverlayActive(false);

        Texture2D resolvedEmissionAtlas = ResolveEmissionAtlasTexture();
        bool expectsEmissionColor = resolvedEmissionAtlas != null;
        if (applyBaseGlowFromOutline && blockUVDatabase != null)
            expectsEmissionColor |= blockUVDatabase.GetGlowIntensity(blockName) > 0.0001f;

        if (!baseMat.HasProperty(EmissionColorID))
        {
            if (expectsEmissionColor)
                WarnMissingEmissionColorProperty();
            return;
        }

        Color emission = Color.black;
        if (applyBaseGlowFromOutline && blockUVDatabase != null)
        {
            Color tint = blockUVDatabase.GetOutlineColor(blockName);
            float dbIntensity = blockUVDatabase.GetGlowIntensity(blockName);
            float strength = Mathf.Max(0f, dbIntensity);
            if (resolvedEmissionAtlas == null)
                strength = Mathf.Min(strength, 0.35f);

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

    void ApplyEmissionOverlayFromDatabase()
    {
        if (!ShouldUseEmissionOverlay() || blockUVDatabase == null)
        {
            SetEmissionOverlayActive(false);
            return;
        }

        float glow = Mathf.Max(0f, blockUVDatabase.GetGlowIntensity(blockName));
        if (glow <= 0.0001f)
        {
            SetEmissionOverlayActive(false);
            return;
        }

        Texture2D resolvedEmissionAtlas = ResolveEmissionAtlasTexture();
        if (resolvedEmissionAtlas == null)
        {
            SetEmissionOverlayActive(false);
            return;
        }

        if (!EnsureEmissionOverlayRenderer())
        {
            SetEmissionOverlayActive(false);
            return;
        }

        Color tint = blockUVDatabase.GetOutlineColor(blockName);
        float strength = emissionOverlayBonus + (Mathf.Max(1f, glow) * emissionOverlayIntensityScale);
        float glowStrength = strength * emissionOverlayGlowBoost;
        Color overlayColor = new Color(tint.r * glowStrength, tint.g * glowStrength, tint.b * glowStrength, 1f);

        emissionOverlayRenderer.GetPropertyBlock(emissionOverlayPropertyBlock);
        emissionOverlayPropertyBlock.Clear();
        emissionOverlayPropertyBlock.SetTexture(BaseMapID, resolvedEmissionAtlas);
        emissionOverlayPropertyBlock.SetTexture(MainTexID, resolvedEmissionAtlas);
        emissionOverlayPropertyBlock.SetColor(BaseColorID, overlayColor);
        emissionOverlayPropertyBlock.SetColor(ColorID, overlayColor);
        emissionOverlayRenderer.SetPropertyBlock(emissionOverlayPropertyBlock);
        emissionOverlayRenderer.enabled = true;
    }

    void ApplyPointLightFromDatabase()
    {
        if (blockPointLight != null && !IsUsableBlockPointLight(blockPointLight))
            blockPointLight = ResolveBlockPointLight(blockPointLight);

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

    private Light ResolveBlockPointLight(Light candidate)
    {
        if (IsUsableBlockPointLight(candidate))
            return candidate;

        Light[] lights = GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (IsUsableBlockPointLight(lights[i]))
                return lights[i];
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (candidate != null && !warnedInvalidPointLight)
        {
            warnedInvalidPointLight = true;
            Debug.LogWarning("[ClickableObject] blockPointLight must be a local Point or Spot light on this block prefab. Ignoring invalid reference.", this);
        }
#endif
        return null;
    }

    private bool IsUsableBlockPointLight(Light light)
    {
        if (light == null)
            return false;

        if (light.type == LightType.Directional)
            return false;

        return light.transform == transform || light.transform.IsChildOf(transform);
    }

    private bool ShouldUseEmissionOverlay()
    {
        return useEmissionOverlay && ResolveEmissionAtlasTexture() != null;
    }

    private Texture2D ResolveEmissionAtlasTexture()
    {
        if (emissionAtlas != null)
            return emissionAtlas;

        if (cubeMaterial == null || !cubeMaterial.HasProperty(EmissionMapID))
            return null;

        return cubeMaterial.GetTexture(EmissionMapID) as Texture2D;
    }

    private void DisableBaseMaterialEmission(Material baseMat)
    {
        if (baseMat == null)
            return;

        baseMat.DisableKeyword("_EMISSION");
        cubeRenderer.GetPropertyBlock(baseGlowPropertyBlock, 0);
        baseGlowPropertyBlock.SetColor(EmissionColorID, Color.black);
        cubeRenderer.SetPropertyBlock(baseGlowPropertyBlock, 0);
    }

    private void SyncEmissionOverlayMesh()
    {
        if (emissionOverlayMeshFilter == null || generatedCubeMesh == null)
            return;

        if (emissionOverlayMeshFilter.sharedMesh != generatedCubeMesh)
            emissionOverlayMeshFilter.sharedMesh = generatedCubeMesh;

        Transform overlayTransform = emissionOverlayRenderer != null ? emissionOverlayRenderer.transform : null;
        if (overlayTransform != null)
        {
            overlayTransform.localPosition = Vector3.zero;
            overlayTransform.localRotation = Quaternion.identity;
            overlayTransform.localScale = Vector3.one * emissionOverlayScale;
        }
    }

    private bool EnsureEmissionOverlayRenderer()
    {
        if (!ShouldUseEmissionOverlay())
            return false;

        if (!EnsureEmissionOverlayMaterial())
            return false;

        if (emissionOverlayRenderer == null || emissionOverlayMeshFilter == null)
        {
            Transform overlayTransform = transform.Find("EmissionOverlay");
            GameObject overlayObject;
            if (overlayTransform != null)
            {
                overlayObject = overlayTransform.gameObject;
            }
            else
            {
                overlayObject = new GameObject("EmissionOverlay");
                overlayObject.transform.SetParent(transform, false);
                overlayObject.layer = gameObject.layer;
            }

            emissionOverlayMeshFilter = overlayObject.GetComponent<MeshFilter>();
            if (emissionOverlayMeshFilter == null)
                emissionOverlayMeshFilter = overlayObject.AddComponent<MeshFilter>();

            emissionOverlayRenderer = overlayObject.GetComponent<MeshRenderer>();
            if (emissionOverlayRenderer == null)
                emissionOverlayRenderer = overlayObject.AddComponent<MeshRenderer>();

            emissionOverlayRenderer.sharedMaterial = emissionOverlayMaterial;
            emissionOverlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            emissionOverlayRenderer.receiveShadows = false;
            emissionOverlayRenderer.lightProbeUsage = LightProbeUsage.Off;
            emissionOverlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            emissionOverlayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            emissionOverlayRenderer.allowOcclusionWhenDynamic = false;
        }

        if (emissionOverlayRenderer.sharedMaterial != emissionOverlayMaterial)
            emissionOverlayRenderer.sharedMaterial = emissionOverlayMaterial;

        SyncEmissionOverlayMesh();
        return true;
    }

    private bool EnsureEmissionOverlayMaterial()
    {
        if (emissionOverlayMaterial != null)
            return true;

        Shader overlayShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (overlayShader == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!warnedMissingEmissionOverlayShader)
            {
                warnedMissingEmissionOverlayShader = true;
                Debug.LogWarning("[ClickableObject] Missing URP Unlit shader. Emission overlay cannot be created.", this);
            }
#endif
            return false;
        }

        emissionOverlayMaterial = new Material(overlayShader)
        {
            name = "BlockEmissionOverlay (Runtime)",
            renderQueue = (int)RenderQueue.Transparent + 10
        };

        emissionOverlayMaterial.SetFloat(SurfaceID, 1f);
        emissionOverlayMaterial.SetFloat(BlendID, 0f);
        emissionOverlayMaterial.SetFloat(SrcBlendID, (float)BlendMode.One);
        emissionOverlayMaterial.SetFloat(DstBlendID, (float)BlendMode.One);
        emissionOverlayMaterial.SetFloat(SrcBlendAlphaID, (float)BlendMode.One);
        emissionOverlayMaterial.SetFloat(DstBlendAlphaID, (float)BlendMode.One);
        emissionOverlayMaterial.SetFloat(ZWriteID, 0f);
        emissionOverlayMaterial.SetFloat(CullID, (float)CullMode.Back);
        emissionOverlayMaterial.SetFloat(AlphaClipID, 1f);
        emissionOverlayMaterial.SetFloat(CutoffID, emissionOverlayCutoff);
        emissionOverlayMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        emissionOverlayMaterial.EnableKeyword("_ALPHATEST_ON");
        return true;
    }

    private void SetEmissionOverlayActive(bool active)
    {
        if (emissionOverlayRenderer == null)
            return;

        emissionOverlayRenderer.enabled = active;
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

    bool TryGetBaseMaterial(out Material baseMat)
    {
        baseMat = null;

        if (cubeRenderer == null)
            return false;

        var mats = cubeRenderer.sharedMaterials;
        if (mats == null || mats.Length == 0)
            return false;

        baseMat = mats[0];
        return baseMat != null;
    }

    void WarnMissingBaseTextureProperty()
    {
        if (warnedMissingBaseTextureProperty)
            return;

        warnedMissingBaseTextureProperty = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning("[ClickableObject] Base material slot 0 is missing _BaseMap/_MainTex texture property.", this);
#endif
    }

    void WarnMissingEmissionMapProperty()
    {
        if (warnedMissingEmissionMapProperty)
            return;

        warnedMissingEmissionMapProperty = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning("[ClickableObject] Base material slot 0 is missing _EmissionMap. Falling back to uniform emission color.", this);
#endif
    }

    void WarnMissingEmissionColorProperty()
    {
        if (warnedMissingEmissionColorProperty)
            return;

        warnedMissingEmissionColorProperty = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning("[ClickableObject] Base material slot 0 is missing _EmissionColor. Block emission cannot be applied.", this);
#endif
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







