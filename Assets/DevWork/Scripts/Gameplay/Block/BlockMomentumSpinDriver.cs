using UnityEngine;

public sealed class BlockMomentumSpinDriver : MonoBehaviour
{
    private const float MinThreshold = 0.001f;
    [SerializeField, Min(0.1f)] private float damping = 7.5f;
    [SerializeField, Min(0f)] private float maxAngularSpeed = 900f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField, Min(MinThreshold)] private float stopSpeedThreshold = 8f;

    private Vector3 angularVelocityDegPerSec;
    public bool HasMomentum
    {
        get
        {
            float threshold = Mathf.Max(MinThreshold, stopSpeedThreshold);
            return angularVelocityDegPerSec.sqrMagnitude > threshold * threshold;
        }
    }

    public void Configure(float dampingValue, float maxAngularSpeedValue, bool useUnscaledTimeValue, float stopSpeedThresholdValue = 8f)
    {
        damping = Mathf.Max(0.1f, dampingValue);
        maxAngularSpeed = Mathf.Max(0f, maxAngularSpeedValue);
        useUnscaledTime = useUnscaledTimeValue;
        stopSpeedThreshold = Mathf.Max(MinThreshold, stopSpeedThresholdValue);
    }

    public void AddAngularVelocity(Vector3 worldAxis, float deltaSpeedDegPerSec)
    {
        if (worldAxis.sqrMagnitude <= 0.000001f || deltaSpeedDegPerSec <= 0.00001f)
            return;

        angularVelocityDegPerSec += worldAxis.normalized * deltaSpeedDegPerSec;
        if (maxAngularSpeed > 0f)
        {
            float speed = angularVelocityDegPerSec.magnitude;
            if (speed > maxAngularSpeed)
                angularVelocityDegPerSec = angularVelocityDegPerSec.normalized * maxAngularSpeed;
        }
    }

    public void ResetMomentum()
    {
        angularVelocityDegPerSec = Vector3.zero;
    }

    private void OnDisable()
    {
        angularVelocityDegPerSec = Vector3.zero;
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f)
            return;

        float speed = angularVelocityDegPerSec.magnitude;
        if (speed <= stopSpeedThreshold)
        {
            angularVelocityDegPerSec = Vector3.zero;
            return;
        }

        transform.Rotate(angularVelocityDegPerSec.normalized, speed * dt, Space.World);

        float decay = Mathf.Exp(-Mathf.Max(0.1f, damping) * dt);
        angularVelocityDegPerSec *= decay;
    }
}
