using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FpsDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private Text legacyText;
    [SerializeField] private float refreshInterval = 0.5f;
    [SerializeField] private float minDelta = 0.5f;

    private float timeLeft;
    private int frames;
    private float accum;
    private float lastFps = -1f;

    void Awake()
    {
        if (tmpText == null) tmpText = GetComponent<TMP_Text>();
        if (legacyText == null) legacyText = GetComponent<Text>();
        timeLeft = Mathf.Max(0.1f, refreshInterval);
    }

    void Update()
    {
        timeLeft -= Time.unscaledDeltaTime;
        accum += Time.unscaledDeltaTime;
        frames++;

        if (timeLeft > 0f) return;

        float fps = accum > 0f ? frames / accum : 0f;
        if (lastFps >= 0f && Mathf.Abs(fps - lastFps) < minDelta)
        {
            ResetSample();
            return;
        }

        lastFps = fps;
        if (tmpText != null) tmpText.SetText("FPS: {0:F1}", fps);
        else if (legacyText != null) legacyText.text = $"FPS: {fps:F1}";

        ResetSample();
    }

    private void ResetSample()
    {
        timeLeft = Mathf.Max(0.1f, refreshInterval);
        accum = 0f;
        frames = 0;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
