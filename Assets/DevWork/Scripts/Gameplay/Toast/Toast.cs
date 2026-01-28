using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Lean.Pool;

[RequireComponent(typeof(CanvasGroup))]
public class Toast : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Image icon;

    [Header("Anim")]
    [SerializeField] private float inDuration = 0.2f;
    [SerializeField] private float holdDuration = 1.8f;
    [SerializeField] private float outDuration = 0.2f;
    [SerializeField] private float slideDistance = 30f; // px upward on show
    [SerializeField] private Ease inEase = Ease.OutCubic;
    [SerializeField] private Ease outEase = Ease.InCubic;

    private CanvasGroup cg;
    private RectTransform rt;
    private Tween currentTween;
    private Color baseTextColor = Color.white;
    private bool useRainbowRuntime;
    private RectTransform iconRt;
    private Vector2 iconBaseSize;

    [Header("Rainbow Text")]
    [SerializeField] private bool useRainbowText = false;
    [SerializeField] private float rainbowSpeed = 0.6f;
    [SerializeField] private float rainbowUpdateInterval = 0.05f;
    [SerializeField] private float rainbowSaturation = 1f;
    [SerializeField] private float rainbowValue = 1f;
    private float nextRainbowUpdateAt;

    [Header("Icon Size")]
    [SerializeField] [Range(0.1f, 1f)] private float iconScaleWithSprite = 0.7f;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        rt = GetComponent<RectTransform>();
        if (messageText != null) baseTextColor = messageText.color;
        if (icon != null)
        {
            iconRt = icon.rectTransform;
            iconBaseSize = iconRt.sizeDelta;
        }
    }

    void OnDisable()
    {
        currentTween?.Kill();
        if (messageText != null) messageText.color = baseTextColor;
        useRainbowRuntime = false;
    }

    public void Play(string msg, Sprite sprite = null, float? durationOverride = null, bool? rainbowOverride = null)
    {
        // text + icon
        if (messageText) messageText.text = msg;
        if (icon)
        {
            icon.enabled = sprite != null;
            icon.sprite = sprite;
            if (iconRt != null)
                iconRt.sizeDelta = sprite != null ? iconBaseSize * iconScaleWithSprite : iconBaseSize;
        }

        useRainbowRuntime = rainbowOverride ?? useRainbowText;
        if (messageText != null && !useRainbowRuntime)
            messageText.color = baseTextColor;
        nextRainbowUpdateAt = 0f;

        // reset starting state
        currentTween?.Kill();
        cg.alpha = 0f;

        Vector2 startPos = rt.anchoredPosition;
        rt.anchoredPosition = startPos - new Vector2(0, slideDistance);

        float hold = durationOverride.HasValue ? Mathf.Max(0.1f, durationOverride.Value) : holdDuration;

        // sequence: fade/slide in -> hold -> fade/slide out -> despawn
        currentTween = DOTween.Sequence()
            .Append(cg.DOFade(1f, inDuration).SetEase(inEase))
            .Join(rt.DOAnchorPos(startPos, inDuration).SetEase(inEase))
            .AppendInterval(hold)
            .Append(cg.DOFade(0f, outDuration).SetEase(outEase))
            .Join(rt.DOAnchorPos(startPos + new Vector2(0, slideDistance * 0.5f), outDuration).SetEase(outEase))
            .OnComplete(() =>
            {
                // return to pool
                LeanPool.Despawn(gameObject);
            });
    }

    void Update()
    {
        if (!useRainbowRuntime || messageText == null) return;

        float now = Time.unscaledTime;
        float interval = Mathf.Max(0f, rainbowUpdateInterval);
        if (interval > 0f && now < nextRainbowUpdateAt) return;

        nextRainbowUpdateAt = now + interval;
        float h = Mathf.Repeat(now * rainbowSpeed, 1f);
        messageText.color = Color.HSVToRGB(h, rainbowSaturation, rainbowValue);
    }
}
