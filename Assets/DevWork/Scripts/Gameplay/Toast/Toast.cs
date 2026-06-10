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
    private Vector2 iconBaseAnchorMin;
    private Vector2 iconBaseAnchorMax;
    private Vector2 iconBasePivot;
    private Vector2 iconBaseAnchoredPosition;
    private bool messageTextBaseEnabled = true;

    [Header("Rainbow Text")]
    [SerializeField] private bool useRainbowText = false;
    [SerializeField] private float rainbowSpeed = 0.6f;
    [SerializeField] private float rainbowUpdateInterval = 0.05f;
    [SerializeField] private float rainbowSaturation = 1f;
    [SerializeField] private float rainbowValue = 1f;
    private float nextRainbowUpdateAt;

    [Header("Icon Size")]
    [SerializeField] [Range(0.1f, 1f)] private float iconScaleWithSprite = 0.7f;
    [SerializeField] [Range(0.3f, 4f)] private float pickupIconScale = 2.6f;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        rt = GetComponent<RectTransform>();
        if (messageText != null)
        {
            baseTextColor = messageText.color;
            messageTextBaseEnabled = messageText.enabled;
        }
        if (icon != null)
        {
            iconRt = icon.rectTransform;
            iconBaseSize = iconRt.sizeDelta;
            iconBaseAnchorMin = iconRt.anchorMin;
            iconBaseAnchorMax = iconRt.anchorMax;
            iconBasePivot = iconRt.pivot;
            iconBaseAnchoredPosition = iconRt.anchoredPosition;
        }
    }

    void OnDisable()
    {
        currentTween?.Kill();
        RestoreNormalLayout();
        useRainbowRuntime = false;
    }

    public void Play(string msg, Sprite sprite = null, float? durationOverride = null, bool? rainbowOverride = null)
    {
        ApplyContent(msg, sprite, rainbowOverride);

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

    public void PlayPickupIcon(Sprite sprite, float duration = 1f, float riseDistance = 90f)
    {
        ApplyIconOnlyContent(sprite);
        PlayPickupTween(duration, riseDistance);
    }

    private void PlayPickupTween(float duration, float riseDistance)
    {
        if (rt == null || cg == null)
            return;

        currentTween?.Kill();
        cg.alpha = 0f;

        Vector2 startPos = rt.anchoredPosition;
        float safeDuration = Mathf.Max(0.15f, duration);
        float safeRiseDistance = Mathf.Max(0f, riseDistance);
        float fadeInDuration = Mathf.Min(0.06f, safeDuration * 0.15f);
        float fadeOutDuration = Mathf.Max(0.05f, safeDuration - fadeInDuration);

        currentTween = DOTween.Sequence()
            .Append(cg.DOFade(1f, fadeInDuration).SetEase(inEase))
            .Append(cg.DOFade(0f, fadeOutDuration).SetEase(Ease.InQuad))
            .Join(rt.DOAnchorPos(startPos + new Vector2(0f, safeRiseDistance), safeDuration).SetEase(Ease.OutCubic))
            .OnComplete(() =>
            {
                LeanPool.Despawn(gameObject);
            });
    }

    private void ApplyContent(string msg, Sprite sprite, bool? rainbowOverride)
    {
        RestoreNormalLayout();

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
    }

    private void ApplyIconOnlyContent(Sprite sprite)
    {
        RestoreNormalLayout();

        if (messageText != null)
            messageText.enabled = false;

        if (icon != null)
        {
            icon.enabled = sprite != null;
            icon.sprite = sprite;

            if (iconRt != null)
            {
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                iconRt.sizeDelta = iconBaseSize * pickupIconScale;
            }
        }

        useRainbowRuntime = false;
        nextRainbowUpdateAt = 0f;
    }

    private void RestoreNormalLayout()
    {
        if (messageText != null)
        {
            messageText.enabled = messageTextBaseEnabled;
            messageText.color = baseTextColor;
        }

        if (iconRt != null)
        {
            iconRt.anchorMin = iconBaseAnchorMin;
            iconRt.anchorMax = iconBaseAnchorMax;
            iconRt.pivot = iconBasePivot;
            iconRt.anchoredPosition = iconBaseAnchoredPosition;
            iconRt.sizeDelta = iconBaseSize;
        }
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
