using UnityEngine;

public sealed class BlockAuraController : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private ParticleSystem[] particleSystems;
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private Light[] targetLights;

    private MaterialPropertyBlock propertyBlock;
    private float[] baseRateOverTimeMultipliers;
    private float[] baseRateOverDistanceMultipliers;
    private float[] baseSimulationSpeeds;
    private float[] baseLightIntensities;
    private Color currentColor = Color.white;
    private float currentIntensity = 1f;
    private bool isCached;

    private void Awake()
    {
        CacheIfNeeded();
        Hide();
    }

    public void SetState(bool visible, Color color, float intensity)
    {
        if (visible)
            Show(color, intensity);
        else
            Hide();
    }

    public void Show(Color color, float intensity)
    {
        CacheIfNeeded();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        currentColor = color;
        currentIntensity = Mathf.Max(0f, intensity);

        ApplyParticleState();
        ApplyRendererTint();
        ApplyLightState();

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;

            if (!ps.gameObject.activeSelf)
                ps.gameObject.SetActive(true);

            if (!ps.isPlaying)
                ps.Play(withChildren: true);
        }
    }

    public void Hide()
    {
        CacheIfNeeded();

        currentIntensity = 0f;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;

            ps.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        ApplyParticleState();
        ApplyRendererTint();
        ApplyLightState();

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    public void SetColor(Color color)
    {
        CacheIfNeeded();
        currentColor = color;
        ApplyRendererTint();
        ApplyLightState();
    }

    public void SetIntensity(float intensity)
    {
        CacheIfNeeded();
        currentIntensity = Mathf.Max(0f, intensity);
        ApplyParticleState();
        ApplyRendererTint();
        ApplyLightState();
    }

    private void CacheIfNeeded()
    {
        if (isCached)
            return;

        if (particleSystems == null || particleSystems.Length == 0)
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        if (targetLights == null || targetLights.Length == 0)
            targetLights = GetComponentsInChildren<Light>(true);

        propertyBlock = new MaterialPropertyBlock();
        baseRateOverTimeMultipliers = new float[particleSystems.Length];
        baseRateOverDistanceMultipliers = new float[particleSystems.Length];
        baseSimulationSpeeds = new float[particleSystems.Length];
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;

            var emission = ps.emission;
            var main = ps.main;
            baseRateOverTimeMultipliers[i] = emission.rateOverTimeMultiplier;
            baseRateOverDistanceMultipliers[i] = emission.rateOverDistanceMultiplier;
            baseSimulationSpeeds[i] = main.simulationSpeed;
        }

        baseLightIntensities = new float[targetLights.Length];
        for (int i = 0; i < targetLights.Length; i++)
        {
            Light light = targetLights[i];
            if (light == null)
                continue;

            baseLightIntensities[i] = light.intensity;
        }

        isCached = true;
    }

    private void ApplyParticleState()
    {
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null)
                continue;

            var emission = ps.emission;
            emission.rateOverTimeMultiplier = baseRateOverTimeMultipliers[i] * currentIntensity;
            emission.rateOverDistanceMultiplier = baseRateOverDistanceMultipliers[i] * currentIntensity;
            var main = ps.main;
            main.simulationSpeed = currentIntensity > 0.0001f ? baseSimulationSpeeds[i] : 0f;
        }
    }

    private void ApplyRendererTint()
    {
        Color hdrColor = currentColor * currentIntensity;
        hdrColor.a = Mathf.Clamp01(currentColor.a);

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
                continue;

            targetRenderer.enabled = currentIntensity > 0.0001f;
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.Clear();

            Material sharedMaterial = targetRenderer.sharedMaterial;
            if (sharedMaterial != null)
            {
                if (sharedMaterial.HasProperty(ColorId))
                    propertyBlock.SetColor(ColorId, hdrColor);
                if (sharedMaterial.HasProperty(BaseColorId))
                    propertyBlock.SetColor(BaseColorId, hdrColor);
                if (sharedMaterial.HasProperty(TintColorId))
                    propertyBlock.SetColor(TintColorId, hdrColor);
                if (sharedMaterial.HasProperty(EmissionColorId))
                    propertyBlock.SetColor(EmissionColorId, hdrColor);
            }

            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ApplyLightState()
    {
        for (int i = 0; i < targetLights.Length; i++)
        {
            Light light = targetLights[i];
            if (light == null)
                continue;

            light.color = currentColor;
            light.intensity = baseLightIntensities[i] * currentIntensity;
            light.enabled = light.intensity > 0.0001f;
        }
    }
}
