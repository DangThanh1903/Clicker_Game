using System.Collections;
using DG.Tweening;
using Lean.Pool;
using UnityEngine;
using UnityEngine.Rendering;

public class BlockFragment : MonoBehaviour
{
    [Header("Texture")]
    [SerializeField] bool useMaterialInstanceFallback = false;

    [Header("Interaction")]
    [SerializeField, Tooltip("When enabled, validates that this prefab already uses Ignore Raycast layer. No runtime auto-fix.")]
    bool setIgnoreRaycastLayer = true;
    [SerializeField] string ignoreRaycastLayerName = "Ignore Raycast";

    [Header("Physics Launch")]
    [SerializeField] float spawnJitterRadius = 0.08f;
    [SerializeField] Vector2 impulseRange = new Vector2(2.4f, 4.6f);
    [SerializeField, Min(0f)] float launchImpulseMultiplier = 6f;
    [SerializeField, Min(0f)] float impulseRangeStackGrowth = 0.32f;
    [SerializeField] float upwardBias = 0.28f;
    [SerializeField] Vector2 torqueRange = new Vector2(1.1f, 3.2f);
    [SerializeField, Min(0f)] float torqueRangeStackGrowth = 0.55f;
    [SerializeField, Min(0f)] float torqueStrengthStackGrowth = 0.9f;
    [SerializeField, Min(1f)] float torqueStackCurvePower = 1.25f;
    [SerializeField] float lifeTime = 2.8f;
    [SerializeField] bool shrinkBeforeDespawn = true;
    [SerializeField, Min(0.01f)] float shrinkDuration = 0.18f;
    [SerializeField] Ease shrinkEase = Ease.InQuad;

    [Header("Offscreen Culling")]
    [SerializeField] bool despawnWhenOutOfCamera = true;
    [SerializeField, Min(0.02f)] float outOfCameraCheckInterval = 0.12f;
    [SerializeField, Min(0f)] float outOfCameraGraceTime = 0.15f;
    [SerializeField, Min(0f)] float outOfCameraViewportPadding = 0.05f;

    [Header("Render Optimization")]
    [SerializeField] bool optimizeRendererSettings = true;
    [SerializeField] ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;
    [SerializeField] bool receiveShadows = false;
    [SerializeField] LightProbeUsage lightProbeUsage = LightProbeUsage.Off;
    [SerializeField] ReflectionProbeUsage reflectionProbeUsage = ReflectionProbeUsage.Off;
    [SerializeField] MotionVectorGenerationMode motionVectorMode = MotionVectorGenerationMode.ForceNoMotion;
    [SerializeField] bool disableDynamicOcclusion = true;

    Renderer rend;
    Renderer[] cachedRenderers;
    MaterialPropertyBlock mpb;
    Rigidbody rb;
    Collider physicsCollider;
    Tween scaleTween;
    Coroutine despawnRoutine;
    int ignoreRaycastLayer = -1;
    bool warnedMissingIgnoreRaycastLayer;
    bool warnedInvalidIgnoreRaycastLayer;
    bool warnedMissingRigidbody;
    bool warnedMissingCollider;
    bool warnedInvalidMeshCollider;
    bool physicsConfigured;
    static Camera cachedMainCamera;
    static int cachedMainCameraFrame = -1;
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
    static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    Vector3 initialLocalScale;
    float nextOutOfCameraCheckAt;
    float outOfCameraElapsed;

    void Awake()
    {
        CacheRenderers();
        mpb = new MaterialPropertyBlock();
        rb = GetComponent<Rigidbody>();
        physicsCollider = GetComponent<Collider>();
        initialLocalScale = transform.localScale;
        physicsConfigured = ConfigurePhysicsComponents();
        ValidateIgnoreRaycastLayer();
        ApplyRendererOptimization();
    }

    void OnEnable()
    {
        transform.localScale = initialLocalScale;
        KillTweens();
        StopDespawnRoutine();
        ResetOutOfCameraCullingState();
        if (despawnWhenOutOfCamera)
            BlockFragmentCullingManager.Register(this);
        ValidateIgnoreRaycastLayer();
        ApplyRendererOptimization();
        if (!physicsConfigured)
            physicsConfigured = ConfigurePhysicsComponents();
        LaunchWithPhysicsInternal(null, 1f, 0, true);
    }

    void OnDisable()
    {
        KillTweens();
        StopDespawnRoutine();
        BlockFragmentCullingManager.Unregister(this);
        transform.localScale = initialLocalScale;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }
    }

    // Call this after spawn to set atlas tile (if needed)
    public void SetupTile(Texture atlas, int cols, int rows, Vector2Int tile, bool flipY)
    {
        if (atlas == null) return;
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            CacheRenderers();
        if (cachedRenderers == null || cachedRenderers.Length == 0) return;

        float sx = 1f / Mathf.Max(1, cols);
        float sy = 1f / Mathf.Max(1, rows);
        int ty = flipY ? (rows - 1 - tile.y) : tile.y;
        Vector4 st = new Vector4(sx, sy, tile.x * sx, ty * sy);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            var target = cachedRenderers[i];
            if (target == null)
                continue;

            int matCount = target.sharedMaterials != null ? target.sharedMaterials.Length : 0;
            if (matCount <= 0)
                continue;

            mpb.Clear();
            target.GetPropertyBlock(mpb);
            mpb.SetTexture(MainTexId, atlas);
            mpb.SetVector(MainTexStId, st);
            mpb.SetTexture(BaseMapId, atlas);
            mpb.SetVector(BaseMapStId, st);
            target.SetPropertyBlock(mpb);

            if (useMaterialInstanceFallback)
                ApplyMaterialFallbackToAllSlots(target, atlas, st);
        }
    }

    static void ApplyMaterialFallbackToAllSlots(Renderer target, Texture atlas, Vector4 st)
    {
        var mats = target.materials;
        Vector2 scale = new Vector2(st.x, st.y);
        Vector2 offset = new Vector2(st.z, st.w);

        if (mats == null || mats.Length == 0)
            return;

        for (int i = 0; i < mats.Length; i++)
        {
            var mat = mats[i];
            if (mat == null)
                continue;

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", atlas);
                mat.SetTextureScale("_BaseMap", scale);
                mat.SetTextureOffset("_BaseMap", offset);
            }

            if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", atlas);
                mat.SetTextureScale("_MainTex", scale);
                mat.SetTextureOffset("_MainTex", offset);
            }
        }
    }

    void CacheRenderers()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
            cachedRenderers = new[] { rend };
        else
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }

    void ApplyRendererOptimization()
    {
        if (!optimizeRendererSettings)
            return;

        if (cachedRenderers == null || cachedRenderers.Length == 0)
            CacheRenderers();

        if (cachedRenderers == null || cachedRenderers.Length == 0)
            return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            var r = cachedRenderers[i];
            if (r == null)
                continue;

            r.shadowCastingMode = shadowCastingMode;
            r.receiveShadows = receiveShadows;
            r.lightProbeUsage = lightProbeUsage;
            r.reflectionProbeUsage = reflectionProbeUsage;
            r.motionVectorGenerationMode = motionVectorMode;

            if (disableDynamicOcclusion)
                r.allowOcclusionWhenDynamic = false;

        }
    }

    void ValidateIgnoreRaycastLayer()
    {
        if (!setIgnoreRaycastLayer)
            return;

        if (ignoreRaycastLayer < 0)
            ignoreRaycastLayer = LayerMask.NameToLayer(ignoreRaycastLayerName);

        if (ignoreRaycastLayer < 0)
        {
            if (!warnedMissingIgnoreRaycastLayer)
            {
                warnedMissingIgnoreRaycastLayer = true;
                Debug.LogError($"[BlockFragment] Layer '{ignoreRaycastLayerName}' not found.", this);
            }
            return;
        }

        if (gameObject.layer != ignoreRaycastLayer)
        {
            if (!warnedInvalidIgnoreRaycastLayer)
            {
                warnedInvalidIgnoreRaycastLayer = true;
                Debug.LogError(
                    $"[BlockFragment] '{name}' must be on layer '{ignoreRaycastLayerName}'. " +
                    $"Current layer is '{LayerMask.LayerToName(gameObject.layer)}'.",
                    this);
            }
        }
    }

    bool ConfigurePhysicsComponents()
    {
        if (rb == null)
        {
            if (!warnedMissingRigidbody)
            {
                warnedMissingRigidbody = true;
                Debug.LogError($"[BlockFragment] Missing Rigidbody on '{name}'. Add it on prefab for physics debris.", this);
            }
            return false;
        }

        if (physicsCollider == null)
        {
            if (!warnedMissingCollider)
            {
                warnedMissingCollider = true;
                Debug.LogError($"[BlockFragment] Missing Collider on '{name}'. Add it on prefab for physics debris.", this);
            }
            return false;
        }

        if (physicsCollider is MeshCollider meshCol && !meshCol.convex)
        {
            if (!warnedInvalidMeshCollider)
            {
                warnedInvalidMeshCollider = true;
                Debug.LogError($"[BlockFragment] MeshCollider on '{name}' must be Convex for non-kinematic Rigidbody.", this);
            }
        }

        return true;
    }

    public void LaunchDirected(Vector3 direction, float impulseMultiplier = 1f, int stackCount = 0)
    {
        LaunchWithPhysicsInternal(direction, impulseMultiplier, stackCount, false);
    }

    void LaunchWithPhysicsInternal(Vector3? directed, float impulseMultiplier, int stackCount, bool useSpawnJitter)
    {
        if (!physicsConfigured)
            physicsConfigured = ConfigurePhysicsComponents();

        if (useSpawnJitter)
            transform.position += Random.insideUnitSphere * spawnJitterRadius;
        transform.rotation = Random.rotation;

        if (!physicsConfigured || rb == null)
        {
            DespawnAfterDelay();
            return;
        }

        rb.isKinematic = false;
        rb.WakeUp();
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 dir;
        if (directed.HasValue && directed.Value.sqrMagnitude > 0.0001f)
        {
            dir = directed.Value.normalized;
        }
        else
        {
            Vector2 flat = Random.insideUnitCircle.normalized;
            if (flat.sqrMagnitude < 0.0001f)
                flat = Vector2.right;
            dir = new Vector3(flat.x, 0f, flat.y).normalized;
        }
        dir = (dir + Vector3.up * upwardBias).normalized;

        float mul = Mathf.Max(0.1f, impulseMultiplier);
        float stack = Mathf.Max(0, stackCount);
        float impulseRangeScale = 1f + stack * Mathf.Max(0f, impulseRangeStackGrowth);
        float torqueRangeScale = 1f + stack * Mathf.Max(0f, torqueRangeStackGrowth);
        float torqueCurveScale = 1f + Mathf.Pow(stack, Mathf.Max(1f, torqueStackCurvePower)) * Mathf.Max(0f, torqueStrengthStackGrowth);

        float impulse = Random.Range(impulseRange.x, impulseRange.y) * impulseRangeScale * mul * Mathf.Max(0f, launchImpulseMultiplier);
        float torque = Random.Range(torqueRange.x, torqueRange.y) * torqueRangeScale * mul * torqueCurveScale;

        rb.AddForce(dir * impulse, ForceMode.Impulse);
        rb.AddTorque(Random.onUnitSphere * torque, ForceMode.Impulse);

        DespawnAfterDelay();
    }

    void DespawnAfterDelay()
    {
        if (lifeTime <= 0f)
        {
            LeanPool.Despawn(gameObject);
            return;
        }

        StopDespawnRoutine();
        despawnRoutine = StartCoroutine(DespawnAfter(lifeTime));
    }

    IEnumerator DespawnAfter(float seconds)
    {
        float total = Mathf.Max(0f, seconds);

        if (!shrinkBeforeDespawn || shrinkDuration <= 0f)
        {
            yield return new WaitForSeconds(total);
            LeanPool.Despawn(gameObject);
            yield break;
        }

        float shrinkTime = Mathf.Min(total, Mathf.Max(0.2f, shrinkDuration));
        float wait = Mathf.Max(0f, total - shrinkTime);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        scaleTween?.Kill();
        scaleTween = transform.DOScale(Vector3.zero, shrinkTime).SetEase(shrinkEase);
        yield return scaleTween.WaitForCompletion();

        LeanPool.Despawn(gameObject);
    }

    void KillTweens()
    {
        scaleTween?.Kill();
        scaleTween = null;
    }

    void StopDespawnRoutine()
    {
        if (despawnRoutine == null) return;
        StopCoroutine(despawnRoutine);
        despawnRoutine = null;
    }

    void ResetOutOfCameraCullingState()
    {
        nextOutOfCameraCheckAt = 0f;
        outOfCameraElapsed = 0f;
    }

    internal bool ShouldDespawnOutOfCamera(float nowUnscaled)
    {
        if (!despawnWhenOutOfCamera || !isActiveAndEnabled)
            return false;

        if (nowUnscaled < nextOutOfCameraCheckAt)
            return false;

        float interval = Mathf.Max(0.02f, outOfCameraCheckInterval);
        nextOutOfCameraCheckAt = nowUnscaled + interval;

        if (IsOutOfMainCameraView())
        {
            outOfCameraElapsed += interval;
            return outOfCameraElapsed >= Mathf.Max(0f, outOfCameraGraceTime);
        }

        outOfCameraElapsed = 0f;
        return false;
    }

    bool IsOutOfMainCameraView()
    {
        var cam = ResolveMainCamera();
        if (cam == null)
            return false;

        float pad = Mathf.Max(0f, outOfCameraViewportPadding);

        if (cachedRenderers == null || cachedRenderers.Length == 0)
            CacheRenderers();

        if (cachedRenderers != null && cachedRenderers.Length > 0)
        {
            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                var r = cachedRenderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                    continue;

                if (IsPointInMainCameraViewport(cam, r.bounds.center, pad))
                    return false;
            }

            return true;
        }

        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        if (vp.z <= 0f)
            return true;

        return vp.x < -pad || vp.x > 1f + pad || vp.y < -pad || vp.y > 1f + pad;
    }

    static bool IsPointInMainCameraViewport(Camera cam, Vector3 worldPoint, float pad)
    {
        Vector3 vp = cam.WorldToViewportPoint(worldPoint);
        return vp.z > 0f &&
               vp.x >= -pad && vp.x <= 1f + pad &&
               vp.y >= -pad && vp.y <= 1f + pad;
    }

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

}
