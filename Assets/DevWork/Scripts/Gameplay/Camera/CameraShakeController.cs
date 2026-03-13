using UnityEngine;

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class CameraShakeController : MonoBehaviour
{
    public const string CameraShakeEnabledKey = "CameraShakeEnabled";
    private const float LegacyBlockBreakTrauma = 0.08f;
    private const float LegacyMaxPositionOffset = 0.045f;
    private const float LegacyMaxRollOffset = 0.55f;
    private const float LegacyTraumaRecoveryPerSecond = 5f;

    public static CameraShakeController Ins { get; private set; }

    [Header("Target")]
    [SerializeField] private Transform shakeTarget;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Block Break Shake (Very Light)")]
    [SerializeField, Range(0f, 1f)] private float blockBreakTrauma = 0.22f;
    [SerializeField, Min(0f)] private float maxPositionOffset = 0.08f;
    [SerializeField, Min(0f)] private float maxRollOffset = 1.2f;
    [SerializeField, Min(0.1f)] private float noiseFrequency = 22f;
    [SerializeField, Min(0.1f)] private float traumaRecoveryPerSecond = 4.5f;

    private float trauma;
    private Vector3 appliedPositionOffset;
    private Quaternion appliedRotationOffset = Quaternion.identity;
    private float seedX;
    private float seedY;
    private float seedR;

    private static bool hasCachedEnabled;
    private static bool cachedEnabled;
    private static bool hasLoggedMissingInstance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Ins = null;
        hasCachedEnabled = false;
        cachedEnabled = true;
        hasLoggedMissingInstance = false;
    }

    public static bool IsEnabled()
    {
        if (!hasCachedEnabled)
        {
            cachedEnabled = PlayerPrefs.GetInt(CameraShakeEnabledKey, 1) == 1;
            hasCachedEnabled = true;
        }

        return cachedEnabled;
    }

    public static void SetEnabled(bool enabled)
    {
        cachedEnabled = enabled;
        hasCachedEnabled = true;
        PlayerPrefs.SetInt(CameraShakeEnabledKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (!enabled && Ins != null)
            Ins.ResetShakeImmediate();
    }

    public static void TriggerBlockBreakShake(float multiplier = 1f)
    {
        if (!IsEnabled())
            return;

        CameraShakeController instance = ResolveExisting();
        if (instance == null)
            return;

        float traumaAmount = instance.blockBreakTrauma * Mathf.Max(0f, multiplier);
        instance.AddTrauma(traumaAmount);
    }

    private static CameraShakeController ResolveExisting()
    {
        if (Ins != null)
            return Ins;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!hasLoggedMissingInstance)
        {
            hasLoggedMissingInstance = true;
            Debug.LogError("[CameraShakeController] No instance bound in scene. Add CameraShakeController to the active camera.");
        }
#endif
        return null;
    }

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(this);
            return;
        }

        Ins = this;
        hasLoggedMissingInstance = false;
        if (shakeTarget == null)
            shakeTarget = transform;
        UpgradeLegacySerializedValues();

        seedX = Random.value * 100f;
        seedY = Random.value * 100f;
        seedR = Random.value * 100f;
    }

    private void OnDisable()
    {
        ResetShakeImmediate();
    }

    private void LateUpdate()
    {
        if (shakeTarget == null)
            return;

        // Remove previously-applied offsets so camera systems can continue to drive the base pose.
        shakeTarget.localPosition -= appliedPositionOffset;
        shakeTarget.localRotation = shakeTarget.localRotation * Quaternion.Inverse(appliedRotationOffset);
        appliedPositionOffset = Vector3.zero;
        appliedRotationOffset = Quaternion.identity;

        if (!IsEnabled() || trauma <= 0f)
            return;

        float now = useUnscaledTime ? Time.unscaledTime : Time.time;
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        // Linear response keeps low trauma visible for subtle one-shot events.
        float shake01 = trauma;

        float noiseTime = now * noiseFrequency;
        float nx = Mathf.PerlinNoise(seedX, noiseTime) * 2f - 1f;
        float ny = Mathf.PerlinNoise(seedY, noiseTime) * 2f - 1f;
        float nr = Mathf.PerlinNoise(seedR, noiseTime) * 2f - 1f;

        appliedPositionOffset = new Vector3(nx, ny, 0f) * (maxPositionOffset * shake01);
        appliedRotationOffset = Quaternion.Euler(0f, 0f, nr * maxRollOffset * shake01);

        shakeTarget.localPosition += appliedPositionOffset;
        shakeTarget.localRotation = shakeTarget.localRotation * appliedRotationOffset;

        trauma = Mathf.Max(0f, trauma - traumaRecoveryPerSecond * Mathf.Max(0f, dt));
    }

    private void AddTrauma(float amount)
    {
        trauma = Mathf.Clamp01(trauma + amount);
    }

    private void ResetShakeImmediate()
    {
        trauma = 0f;

        if (shakeTarget == null)
            return;

        shakeTarget.localPosition -= appliedPositionOffset;
        shakeTarget.localRotation = shakeTarget.localRotation * Quaternion.Inverse(appliedRotationOffset);
        appliedPositionOffset = Vector3.zero;
        appliedRotationOffset = Quaternion.identity;
    }

    private void UpgradeLegacySerializedValues()
    {
        bool isLegacyConfig =
            Mathf.Approximately(blockBreakTrauma, LegacyBlockBreakTrauma) &&
            Mathf.Approximately(maxPositionOffset, LegacyMaxPositionOffset) &&
            Mathf.Approximately(maxRollOffset, LegacyMaxRollOffset) &&
            Mathf.Approximately(traumaRecoveryPerSecond, LegacyTraumaRecoveryPerSecond);

        if (!isLegacyConfig)
            return;

        blockBreakTrauma = 0.28f;
        maxPositionOffset = 0.12f;
        maxRollOffset = 1.8f;
        traumaRecoveryPerSecond = 4.5f;
    }
}
