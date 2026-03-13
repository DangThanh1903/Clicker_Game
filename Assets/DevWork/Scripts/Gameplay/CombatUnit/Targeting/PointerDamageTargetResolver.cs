using UnityEngine;

public interface IPointerDamageTargetResolver
{
    bool TryResolvePointerTarget(out IDamageReceiver target, out Vector3 hitPoint);
    void ApplyPointerHitContext(IDamageReceiver target, Vector3 hitPoint);
}

public sealed class PhysicsPointerDamageTargetResolver : IPointerDamageTargetResolver
{
    public static readonly PhysicsPointerDamageTargetResolver Instance = new PhysicsPointerDamageTargetResolver();

    private static Camera cachedMainCamera;
    private static int cachedMainCameraFrame = -1;
    private int raycastLayerMask = Physics.DefaultRaycastLayers;
    private float raycastDistance = Mathf.Infinity;
    private QueryTriggerInteraction raycastTriggerInteraction = QueryTriggerInteraction.Ignore;

    private PhysicsPointerDamageTargetResolver()
    {
    }

    public void ConfigureRaycast(int layerMask, float maxDistance = Mathf.Infinity, QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore)
    {
        raycastLayerMask = layerMask == 0 ? Physics.DefaultRaycastLayers : layerMask;
        raycastDistance = maxDistance > 0f ? maxDistance : Mathf.Infinity;
        raycastTriggerInteraction = triggerInteraction;
    }

    public bool TryResolvePointerTarget(out IDamageReceiver target, out Vector3 hitPoint)
    {
        target = null;
        hitPoint = Vector3.zero;

        Camera cam = ResolveMainCamera();
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                raycastDistance,
                raycastLayerMask,
                raycastTriggerInteraction))
            return false;

        hitPoint = hit.point;

        if (!TryGetDamageTargetFromHit(hit.transform, out IDamageReceiver damageTarget))
            return false;

        target = damageTarget;
        return true;
    }

    public void ApplyPointerHitContext(IDamageReceiver target, Vector3 hitPoint)
    {
        if (target is IPointerHitContext pointerHitContext)
            pointerHitContext.SetPointerHit(hitPoint);
    }

    private static Camera ResolveMainCamera()
    {
        if (cachedMainCamera != null && cachedMainCamera.isActiveAndEnabled)
            return cachedMainCamera;

        if (cachedMainCameraFrame == Time.frameCount)
            return cachedMainCamera;

        cachedMainCameraFrame = Time.frameCount;
        cachedMainCamera = Camera.main;
        return cachedMainCamera;
    }

    private static bool TryGetDamageTargetFromHit(Transform hitTransform, out IDamageReceiver damageTarget)
    {
        damageTarget = null;
        if (hitTransform == null)
            return false;

        damageTarget = hitTransform.GetComponent(typeof(IDamageReceiver)) as IDamageReceiver;
        if (!IsNullTarget(damageTarget))
            return true;

        damageTarget = hitTransform.GetComponentInParent(typeof(IDamageReceiver)) as IDamageReceiver;
        return !IsNullTarget(damageTarget);
    }

    private static bool IsNullTarget(IDamageReceiver target)
    {
        if (ReferenceEquals(target, null))
            return true;

        if (target is Object unityObj)
            return unityObj == null;

        return false;
    }
}
