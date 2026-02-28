using UnityEngine;
using UnityEngine.VFX;

[DisallowMultipleComponent]
public class HoldBeamVFX : MonoBehaviour
{
    [Header("VFX Graph (Start -> End)")]
    [SerializeField] private VisualEffect visualEffect;
    [SerializeField] private string startPointProperty = "StartPosition";
    [SerializeField] private string endPointProperty = "EndPosition";
    [SerializeField] private bool graphUsesLocalSpace = true;
    [SerializeField] private string onBeamStartEvent = "OnBeamStart";
    [SerializeField] private string onBeamEndEvent = "OnBeamEnd";
    [SerializeField] private float endDespawnDelay = 0.12f;

    [Header("Impact (optional)")]
    [SerializeField] private Transform impactTransform;
    [SerializeField] private bool alignImpactToBeam = true;

    private bool hasStart;
    private Vector3 startPoint;
    private bool warnedMissingVisualEffect;
    private bool warnedMissingBegin;
    public float EndDespawnDelay => Mathf.Max(0f, endDespawnDelay);

    void Awake()
    {
        if (visualEffect == null)
            visualEffect = GetComponentInChildren<VisualEffect>(true);
    }

    void OnEnable()
    {
        hasStart = false;
        warnedMissingVisualEffect = false;
        warnedMissingBegin = false;
    }

    public void Begin(Vector3 start)
    {
        startPoint = start;
        hasStart = true;
        ApplyVfxGraph(startPoint);
        SendStartEvent();
    }

    public void SetEndPoint(Vector3 end)
    {
        if (!hasStart)
        {
            if (!warnedMissingBegin)
            {
                warnedMissingBegin = true;
                Debug.LogWarning("[HoldBeamVFX] SetEndPoint called before Begin. StartPosition is not initialized.");
            }
            return;
        }

        UpdateImpact(end);
        ApplyVfxGraph(end);
    }

    public void EndBeam()
    {
        SendEndEvent();
    }

    private void SendStartEvent()
    {
        if (visualEffect == null || string.IsNullOrWhiteSpace(onBeamStartEvent))
            return;

        visualEffect.SendEvent(onBeamStartEvent);
    }

    private void SendEndEvent()
    {
        if (visualEffect == null || string.IsNullOrWhiteSpace(onBeamEndEvent))
            return;

        visualEffect.SendEvent(onBeamEndEvent);
    }

    private void ApplyVfxGraph(Vector3 endPoint)
    {
        if (visualEffect == null)
        {
            if (!warnedMissingVisualEffect)
            {
                warnedMissingVisualEffect = true;
                Debug.LogWarning("[HoldBeamVFX] Missing VisualEffect reference. Assign a VisualEffect component in prefab/scene.");
            }
            return;
        }

        Vector3 graphStart = ToGraphSpace(startPoint);
        Vector3 graphEnd = ToGraphSpace(endPoint);

        if (!string.IsNullOrWhiteSpace(startPointProperty))
            TrySetVector3(startPointProperty, graphStart);

        if (!string.IsNullOrWhiteSpace(endPointProperty))
            TrySetVector3(endPointProperty, graphEnd);
    }

    private void UpdateImpact(Vector3 end)
    {
        if (impactTransform == null)
            return;

        impactTransform.position = end;
        if (!alignImpactToBeam)
            return;

        Vector3 dir = startPoint - end;
        if (dir.sqrMagnitude > 0.0001f)
            impactTransform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void TrySetVector3(string propertyName, Vector3 value)
    {
        if (visualEffect == null || string.IsNullOrWhiteSpace(propertyName))
            return;

        if (visualEffect.HasVector3(propertyName))
            visualEffect.SetVector3(propertyName, value);
    }

    private Vector3 ToGraphSpace(Vector3 worldPosition)
    {
        if (!graphUsesLocalSpace || visualEffect == null)
            return worldPosition;

        return visualEffect.transform.InverseTransformPoint(worldPosition);
    }
}
