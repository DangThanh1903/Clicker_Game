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

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        rt = GetComponent<RectTransform>();
    }

    void OnDisable()
    {
        currentTween?.Kill();
    }

    public void Play(string msg, Sprite sprite = null, float? durationOverride = null)
    {
        // text + icon
        if (messageText) messageText.text = msg;
        if (icon)
        {
            icon.enabled = sprite != null;
            icon.sprite = sprite;
        }

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
}
