using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(fileName = "ClickableBlockConfig", menuName = "Block/Clickable Block Config")]
public class ClickableBlockConfig : ScriptableObject
{
    [Header("Atlas Settings")]
    [SerializeField] private int crackLevels = 9;
    [SerializeField] private int atlasColumns = 9;
    [SerializeField] private int atlasRows = 12;
    [SerializeField] private int blockColumns = 3;
    [SerializeField] private int blockRows = 2;
    [SerializeField] private bool flipY = true;

    [Header("Material & Atlas")]
    [SerializeField] private Texture2D textureAtlas;
    [SerializeField] private Texture2D emissionAtlas;
    [SerializeField] private Material cubeMaterial;
    [SerializeField] private BlockUVDatabase blockUVDatabase;

    [Header("Outline")]
    [SerializeField, Min(0)] private int outlineMaterialIndex = 2;

    [Header("Base Glow (Material Slot 0)")]
    [SerializeField] private bool applyBaseGlowFromOutline = true;

    [Header("Emission Overlay")]
    [SerializeField] private bool useEmissionOverlay = true;
    [SerializeField, Min(1f)] private float emissionOverlayScale = 1.001f;
    [SerializeField, Min(0f)] private float emissionOverlayIntensityScale = 0.15f;
    [SerializeField, Min(0f)] private float emissionOverlayBonus = 0.1f;
    [SerializeField, Min(1f)] private float emissionOverlayGlowBoost = 2.5f;
    [SerializeField, Range(0f, 1f)] private float emissionOverlayCutoff = 0.01f;

    [Header("Point Light")]
    [SerializeField] private bool applyPointLightFromGlow = true;
    [SerializeField] private bool pointLightUseOutlineColor = true;
    [SerializeField, Min(0f)] private float pointLightMinIntensity = 4f;
    [SerializeField, Min(0f)] private float pointLightMaxIntensity = 15f;
    [SerializeField, Min(0.0001f)] private float pointLightGlowAtMaxIntensity = 1f;
    [SerializeField, Min(0f)] private float pointLightRange = 7f;

    [Header("Aura")]
    [SerializeField] private bool enableAura = true;
    [SerializeField, Min(0f)] private float auraWeightThreshold = 10f;
    [SerializeField, Min(0f)] private float auraMinIntensity = 1f;
    [SerializeField, Min(0f)] private float auraMaxIntensity = 2f;
    [SerializeField, Min(0.0001f)] private float auraGlowAtMaxIntensity = 4f;
    [SerializeField, Range(0f, 1f)] private float auraMinimumVisibleColor = 0.05f;

    [Header("Animation")]
    [SerializeField, Min(1f)] private float baseBlockScale = 2.5f;

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

    public int CrackLevels => crackLevels;
    public int AtlasColumns => atlasColumns;
    public int AtlasRows => atlasRows;
    public int BlockColumns => blockColumns;
    public int BlockRows => blockRows;
    public bool FlipY => flipY;
    public Texture2D TextureAtlas => textureAtlas;
    public Texture2D EmissionAtlas => emissionAtlas;
    public Material CubeMaterial => cubeMaterial;
    public BlockUVDatabase BlockUVDatabase => blockUVDatabase;
    public int OutlineMaterialIndex => outlineMaterialIndex;
    public bool ApplyBaseGlowFromOutline => applyBaseGlowFromOutline;
    public bool UseEmissionOverlay => useEmissionOverlay;
    public float EmissionOverlayScale => emissionOverlayScale;
    public float EmissionOverlayIntensityScale => emissionOverlayIntensityScale;
    public float EmissionOverlayBonus => emissionOverlayBonus;
    public float EmissionOverlayGlowBoost => emissionOverlayGlowBoost;
    public float EmissionOverlayCutoff => emissionOverlayCutoff;
    public bool ApplyPointLightFromGlow => applyPointLightFromGlow;
    public bool PointLightUseOutlineColor => pointLightUseOutlineColor;
    public float PointLightMinIntensity => pointLightMinIntensity;
    public float PointLightMaxIntensity => pointLightMaxIntensity;
    public float PointLightGlowAtMaxIntensity => pointLightGlowAtMaxIntensity;
    public float PointLightRange => pointLightRange;
    public bool EnableAura => enableAura;
    public float AuraWeightThreshold => auraWeightThreshold;
    public float AuraMinIntensity => auraMinIntensity;
    public float AuraMaxIntensity => auraMaxIntensity;
    public float AuraGlowAtMaxIntensity => auraGlowAtMaxIntensity;
    public float AuraMinimumVisibleColor => auraMinimumVisibleColor;
    public float BaseBlockScale => baseBlockScale;
    public float FullHealthScaleMultiplier => fullHealthScaleMultiplier;
    public float GrowNearDeathMaxScale => growNearDeathMaxScale;
    public float NearDeathGrowthExponent => nearDeathGrowthExponent;
    public float GrowThenExplodeBurstScale => growThenExplodeBurstScale;
    public float GrowThenExplodeBurstDuration => growThenExplodeBurstDuration;
    public Ease GrowThenExplodeBurstEase => growThenExplodeBurstEase;
    public bool ScaleHitPitchByRemainingHealth => scaleHitPitchByRemainingHealth;
    public float HitPitchAtFullHealth => hitPitchAtFullHealth;
    public float HitPitchAtZeroHealth => hitPitchAtZeroHealth;
    public float HitPitchCurvePower => hitPitchCurvePower;
}
