using UnityEngine;
using UnityEngine.Rendering; // AmbientMode
using UniRx;
using System;
using Sirenix.OdinInspector;

public enum TimeState { Day, Night, Any }

public class TimeSystem : MonoBehaviour
{
    public static TimeSystem Instance { get; private set; }

    [Header("Timing")]
    [Min(0.01f)] public float updateTick = 0.1f;
    [Min(1f)]    public float dayDuration = 180f;
    [Min(1f)]    public float nightDuration = 180f;
    [Min(0.01f)] public float timeScale = 1f;
    public bool smoothTime = true;
    public bool useUnscaledTime = true;
    [Min(0f)] public float lightingUpdateInterval = 0.1f;

    [Header("Light (Single Directional)")]
    [SerializeField] private Light mainLight;                    // Assign your directional light
    [SerializeField] private Vector3 lightAxis = new Vector3(1f, 0f, 0f);
    [SerializeField, Range(-90f, 90f)] private float tilt = -30f;
    [SerializeField] private bool autoAssignSunLight = true;

    [Header("Directional Color & Intensity (auto-aligned)")]
    [Tooltip("Evaluated over normalized [0..1] where 0 = sunrise, dayPortion = sunset, 1 = next sunrise")]
    public Gradient lightColorOverDay;
    [Tooltip("Directional light intensity over [0..1]")]
    public AnimationCurve directIntensityOverDay = AnimationCurve.Linear(0, 0, 1, 1);

    [Header("Ambient (Flat/Trilight)")]
    [Tooltip("Ambient intensity over [0..1] (used by Flat & Trilight)")]
    public AnimationCurve ambientIntensityOverDay = AnimationCurve.Linear(0, 0.3f, 1, 0.3f);
    [Tooltip("Flat ambient color over [0..1] (used if AmbientMode = Color)")]
    public Gradient ambientColorOverDay;

    [Header("Trilight Ambient (used if AmbientMode = Trilight)")]
    public Gradient ambientSkyOverDay;
    public Gradient ambientEquatorOverDay;
    public Gradient ambientGroundOverDay;

    [Header("Night Options")]
    [Range(0f, 1f)] public float nightShadowStrength = 0.2f;
    [Range(0f, 1f)] public float dayShadowStrength = 1f;

    [Header("Preset Start Time")]
    [Tooltip("Seconds into the cycle to start at. 0 = sunrise, dayDuration = start of night.")]
    public float startTime = 0f;

    [Header("Rotation Phase")]
    [SerializeField, Range(0f, 360f)]
    private float sunriseAngle = 15f;            // elevation at sunrise (>0 so it's not dark at t=0)
    [SerializeField, Range(0f, 720f)]
    private float fullCycleDegrees = 360f;       // full sweep per cycle

    [Header("Skybox (URP Shader Graph)")]
    [Tooltip("Material using your URP Unlit Shader Graph with Day/Night blend.")]
    [SerializeField] private Material skyboxMaterial;
    [Range(0f, 1f)] public float nightMinBlend = 0.0f; // min blend at full night
    [Range(0f, 1f)] public float dayMaxBlend  = 1.0f;  // max blend at full day
    public bool assignAsRenderSettingsSkybox = true;

    [ReadOnly] public ReactiveProperty<TimeState> CurrentTimeState = new ReactiveProperty<TimeState>();
    public ReactiveProperty<float> CurrentTime { get; private set; } = new ReactiveProperty<float>(); // seconds

    private float CycleLength => dayDuration + nightDuration;
    private float lastLightingUpdateAt = -1f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(this);

        // If you persist startTime externally, assign it here (optional):
        startTime = DataSaver.Ins.CurrentTime;
    }

    private void Start()
    {
        EnsureMainLight();
        if (timeScale < 0.05f)
            timeScale = 0.05f;

        // Clamp & apply initial time/state
        startTime = Mathf.Repeat(startTime, CycleLength);
        CurrentTime.Value = startTime;
        CurrentTimeState.Value = (startTime < dayDuration) ? TimeState.Day : TimeState.Night;

        // Build curves/gradients so keys align to sunrise/sunset/next sunrise
        EnsureCurvesMatchDurations();

        // Optionally set skybox material in RenderSettings
        if (assignAsRenderSettingsSkybox && skyboxMaterial != null)
        {
            RenderSettings.skybox = skyboxMaterial;
        }

        // Apply once before ticking
        ApplyLighting(CurrentTime.Value / CycleLength);

        // Tick
        if (smoothTime)
        {
            Observable.EveryUpdate()
                .Subscribe(_ =>
                {
                    float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                    AdvanceTime(dt * timeScale);
                })
                .AddTo(this);
        }
        else
        {
            var scheduler = useUnscaledTime ? Scheduler.MainThreadIgnoreTimeScale : Scheduler.MainThread;
            Observable.Interval(TimeSpan.FromSeconds(updateTick), scheduler)
                .Subscribe(_ => AdvanceTime(updateTick * timeScale))
                .AddTo(this);
        }

        // React to time
        CurrentTime
            .Subscribe(time =>
            {
                float t = time / CycleLength;
                float now = useUnscaledTime ? Time.unscaledTime : Time.time;
                if (lightingUpdateInterval <= 0f || now - lastLightingUpdateAt >= lightingUpdateInterval)
                {
                    ApplyLighting(t);
                    lastLightingUpdateAt = now;
                }

                if (CurrentTimeState.Value == TimeState.Day && time >= dayDuration)
                    SwitchToNight();
                else if (CurrentTimeState.Value == TimeState.Night && time < dayDuration)
                    SwitchToDay();
            })
            .AddTo(this);
    }

    /// <summary>
    /// Rebuild curves/gradients so 0 = sunrise, dayPortion = sunset, 1 = next sunrise.
    /// Call whenever day/night durations change.
    /// </summary>
    private void EnsureCurvesMatchDurations()
    {
        float dayPortion = Mathf.Clamp01(dayDuration / Mathf.Max(0.0001f, CycleLength));
        var aKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };

        // ---- Directional intensity ----
        directIntensityOverDay = new AnimationCurve(
            new Keyframe(0.00f, 0f, 0f, 6f),                          // sunrise
            new Keyframe(dayPortion * 0.30f, 1f),                     // morning ramp
            new Keyframe(dayPortion * 0.50f, 1f),                     // midday
            new Keyframe(dayPortion * 1.00f, 0f, -6f, 0f),            // sunset
            new Keyframe(Mathf.Lerp(dayPortion, 1f, 0.50f), 0f),      // mid-night
            new Keyframe(1.00f, 0f)                                   // next sunrise
        );

        // ---- Ambient intensity (used for Flat & Trilight) ----
        ambientIntensityOverDay = new AnimationCurve(
            new Keyframe(0.00f, 0.25f),                               // dawn
            new Keyframe(dayPortion * 0.30f, 0.60f),
            new Keyframe(dayPortion * 0.50f, 0.90f),
            new Keyframe(dayPortion * 1.00f, 0.30f),                  // sunset
            new Keyframe(1.00f, 0.25f)                                // pre-dawn
        );

        // ---- Directional light color ----
        var sunRise  = new GradientColorKey(new Color(1f, 0.8f, 0.55f), 0f);
        var sunDay   = new GradientColorKey(Color.white,                Mathf.Lerp(0f, dayPortion, 0.5f));
        var sunSet   = new GradientColorKey(new Color(0.98f, 0.62f, 0.45f), dayPortion);
        var sunNight = new GradientColorKey(new Color(0.25f, 0.32f, 0.48f), Mathf.Lerp(dayPortion, 1f, 0.5f));
        var sunNext  = new GradientColorKey(new Color(1f, 0.8f, 0.55f), 1f);

        lightColorOverDay = new Gradient { colorKeys = new[] { sunRise, sunDay, sunSet, sunNight, sunNext }, alphaKeys = aKeys };

        // ---- Flat ambient color (used if AmbientMode = Color) ----
        ambientColorOverDay = new Gradient { colorKeys = new[] { sunNight, sunDay, sunSet, sunNight, sunNext }, alphaKeys = aKeys };

        // ---- Trilight ambient (Sky/Equator/Ground) ----
        Color skySunrise    = new Color(0.75f, 0.55f, 0.45f);
        Color skyDay        = new Color(0.80f, 0.90f, 1.00f);
        Color skySunset     = new Color(0.80f, 0.50f, 0.40f);
        Color skyNight      = new Color(0.10f, 0.15f, 0.25f);

        Color eqSunrise     = new Color(0.80f, 0.65f, 0.55f);
        Color eqDay         = new Color(0.90f, 0.90f, 0.90f);
        Color eqSunset      = new Color(0.85f, 0.55f, 0.45f);
        Color eqNight       = new Color(0.15f, 0.18f, 0.25f);

        Color groundSunrise = new Color(0.35f, 0.25f, 0.20f);
        Color groundDay     = new Color(0.35f, 0.35f, 0.35f);
        Color groundSunset  = new Color(0.30f, 0.22f, 0.18f);
        Color groundNight   = new Color(0.06f, 0.06f, 0.07f);

        ambientSkyOverDay = new Gradient {
            colorKeys = new[] {
                new GradientColorKey(skySunrise, 0f),
                new GradientColorKey(skyDay,     Mathf.Lerp(0f, dayPortion, 0.5f)),
                new GradientColorKey(skySunset,  dayPortion),
                new GradientColorKey(skyNight,   Mathf.Lerp(dayPortion, 1f, 0.5f)),
                new GradientColorKey(skySunrise, 1f),
            },
            alphaKeys = aKeys
        };

        ambientEquatorOverDay = new Gradient {
            colorKeys = new[] {
                new GradientColorKey(eqSunrise, 0f),
                new GradientColorKey(eqDay,     Mathf.Lerp(0f, dayPortion, 0.5f)),
                new GradientColorKey(eqSunset,  dayPortion),
                new GradientColorKey(eqNight,   Mathf.Lerp(dayPortion, 1f, 0.5f)),
                new GradientColorKey(eqSunrise, 1f),
            },
            alphaKeys = aKeys
        };

        ambientGroundOverDay = new Gradient {
            colorKeys = new[] {
                new GradientColorKey(groundSunrise, 0f),
                new GradientColorKey(groundDay,     Mathf.Lerp(0f, dayPortion, 0.5f)),
                new GradientColorKey(groundSunset,  dayPortion),
                new GradientColorKey(groundNight,   Mathf.Lerp(dayPortion, 1f, 0.5f)),
                new GradientColorKey(groundSunrise, 1f),
            },
            alphaKeys = aKeys
        };
    }

    /// <summary>
    /// Apply lighting for normalized t in [0..1] over full cycle.
    /// </summary>
    private void ApplyLighting(float t)
    {
        if (!mainLight) return;

        // --- Rotation (start above horizon so day start isn't black) ---
        float angle = sunriseAngle + (t * fullCycleDegrees);
        var rot = Quaternion.Euler(angle, 0f, 0f);
        mainLight.transform.rotation = Quaternion.AngleAxis(tilt, lightAxis.normalized) * rot;

        // --- Curve time remap aligned with day/night split ---
        float dayPortion = dayDuration / Mathf.Max(0.0001f, CycleLength);
        bool isDay = (CurrentTime.Value < dayDuration);

        // 0..1 inside the active half
        float local = isDay
            ? (CurrentTime.Value / Mathf.Max(0.0001f, dayDuration))
            : ((CurrentTime.Value - dayDuration) / Mathf.Max(0.0001f, nightDuration));

        // curveT spans full [0..1], with sunset exactly at 'dayPortion'
        float curveT = isDay ? Mathf.Lerp(0f, dayPortion, local)
                             : Mathf.Lerp(dayPortion, 1f, local);

        // --- Directional ---
        mainLight.color = lightColorOverDay.Evaluate(curveT);

        float dirI = Mathf.Max(0f, directIntensityOverDay.Evaluate(curveT));
        if (!isDay) dirI = Mathf.Min(dirI, 0.05f); // tiny moon-rim; set to 0f for full darkness
        mainLight.intensity = dirI;

        // --- Ambient (Flat/Trilight) ---
        if (RenderSettings.ambientMode == AmbientMode.Trilight)
        {
            RenderSettings.ambientSkyColor     = ambientSkyOverDay.Evaluate(curveT);
            RenderSettings.ambientEquatorColor = ambientEquatorOverDay.Evaluate(curveT);
            RenderSettings.ambientGroundColor  = ambientGroundOverDay.Evaluate(curveT);
        }
        else // AmbientMode.Color (Flat) or anything else
        {
            if (ambientColorOverDay != null)
                RenderSettings.ambientLight = ambientColorOverDay.Evaluate(curveT);
        }

        RenderSettings.ambientIntensity = Mathf.Max(0f, ambientIntensityOverDay.Evaluate(curveT));

        // --- Shadows ---
        mainLight.shadowStrength = isDay ? dayShadowStrength : nightShadowStrength;

        // --- Skybox blend ---
        UpdateSkybox(curveT);
    }

    private void UpdateSkybox(float curveT)
    {
        if (!skyboxMaterial) return;

        // Use directional intensity as base "day factor"
        float rawDayFactor = Mathf.Clamp01(directIntensityOverDay.Evaluate(curveT));
        float dayFactor = Mathf.SmoothStep(0f, 1f, rawDayFactor);

        // 1 = full day sky, 0 = full night sky
        float blend = Mathf.Lerp(nightMinBlend, dayMaxBlend, dayFactor);

        // Shader Graph float property named "Blend" will be "_Blend" here
        skyboxMaterial.SetFloat("_Blend", blend);

        if (mainLight == null) return;

        Vector3 sunDir = -mainLight.transform.forward;

        if (skyboxMaterial.HasProperty("_SunDirection"))
            skyboxMaterial.SetVector("_SunDirection", sunDir);
        if (skyboxMaterial.HasProperty("_LightDirection"))
            skyboxMaterial.SetVector("_LightDirection", sunDir);
        if (skyboxMaterial.HasProperty("_MainLightDirection"))
            skyboxMaterial.SetVector("_MainLightDirection", sunDir);
        if (skyboxMaterial.HasProperty("_SunDir"))
            skyboxMaterial.SetVector("_SunDir", sunDir);

        // Disable skybox rotation updates to avoid fast/unsynced spinning
    }

    private void EnsureMainLight()
    {
        if (mainLight == null && RenderSettings.sun != null)
            mainLight = RenderSettings.sun;

        if (mainLight == null)
        {
            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                if (l != null && l.type == LightType.Directional)
                {
                    mainLight = l;
                    break;
                }
            }
        }

        if (autoAssignSunLight && mainLight != null)
            RenderSettings.sun = mainLight;
    }

    private void SwitchToDay()
    {
        CurrentTimeState.Value = TimeState.Day;
        DevLog.Log("Switched to Day");
        GameDebugHandler.LogStaticKey("UI_Debug", "time_day");
        WeatherManager.Instance?.ClearSpecialWeatherAndTriggerNext();
    }

    private void SwitchToNight()
    {
        CurrentTimeState.Value = TimeState.Night;
        DevLog.Log("Switched to Night");
        GameDebugHandler.LogStaticKey("UI_Debug", "time_night");
        WeatherManager.Instance?.ClearSpecialWeatherAndTriggerNext();
    }

    private void AdvanceTime(float deltaSeconds)
    {
        if (deltaSeconds <= 0f) return;
        CurrentTime.Value = (CurrentTime.Value + deltaSeconds) % CycleLength;
    }
}

