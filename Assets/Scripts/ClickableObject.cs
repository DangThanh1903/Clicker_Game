using Sirenix.OdinInspector;
using UnityEngine;

public class ClickableObject : MonoBehaviour
{
    [Header("Infomation")]
    private Vector3 spinAxis;
    [ReadOnly, SerializeField]
    private float spinSpeed;

    [Header("Settings")]
    [SerializeField] private float spinBoostPerClick = 100f;
    [SerializeField, Range(0f, 1f)] private float decayPercentPerSecond = 0.95f;
    [SerializeField] private float stopThreshold = 0.1f;

    private bool isSpinning = false;

    void Update()
    {
        if (isSpinning)
        {
            transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.World);

            spinSpeed *= Mathf.Pow(decayPercentPerSecond, Time.deltaTime);

            if (spinSpeed < stopThreshold)
            {
                spinSpeed = 0f;
                isSpinning = false; // ✅ Stop updating rotation
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                OnClicked();
            }
        }
    }

    void OnClicked()
    {
        spinAxis = Random.onUnitSphere;
        spinSpeed += spinBoostPerClick;
        isSpinning = true; // ✅ Only spin when needed

        // Increase number of click
        StatsManager.Ins.Add(StatType.Clicks, 1);
    }
}
