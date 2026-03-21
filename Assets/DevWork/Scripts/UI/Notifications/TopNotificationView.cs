using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TopNotificationView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text messageText;

    [Header("Motion")]
    [SerializeField] private float hiddenY = 180f;
    [SerializeField] private float shownY = -36f;
    [SerializeField, Min(0.05f)] private float slideDuration = 0.22f;
    [SerializeField] private Ease showEase = Ease.OutCubic;
    [SerializeField] private Ease hideEase = Ease.InCubic;
    [SerializeField] private bool useUnscaledTime = true;

    private Tween moveTween;
    private Tween fadeTween;

    private void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogMissingReferencesIfAny();
#endif
        SetHiddenImmediate();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    public IEnumerator Play(string message, float holdDuration, TopNotificationVisualProfile visual)
    {
        if (!HasRequiredReferences())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogMissingReferencesIfAny();
#endif
            yield break;
        }

        ApplyVisual(message, visual);

        panelRoot.gameObject.SetActive(true);
        canvasGroup.alpha = 0f;
        SetY(hiddenY);

        KillTweens();
        moveTween = panelRoot
            .DOAnchorPosY(shownY, slideDuration)
            .SetEase(showEase)
            .SetUpdate(useUnscaledTime);
        fadeTween = canvasGroup
            .DOFade(1f, slideDuration)
            .SetEase(showEase)
            .SetUpdate(useUnscaledTime);

        yield return WaitForTween(moveTween);

        float elapsed = 0f;
        while (elapsed < holdDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        KillTweens();
        moveTween = panelRoot
            .DOAnchorPosY(hiddenY, slideDuration)
            .SetEase(hideEase)
            .SetUpdate(useUnscaledTime);
        fadeTween = canvasGroup
            .DOFade(0f, slideDuration)
            .SetEase(hideEase)
            .SetUpdate(useUnscaledTime);

        yield return WaitForTween(moveTween);
        SetHiddenImmediate();
    }

    public void SetHiddenImmediate()
    {
        if (panelRoot != null)
            SetY(hiddenY);

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (panelRoot != null)
            panelRoot.gameObject.SetActive(false);
    }

    private void ApplyVisual(string message, TopNotificationVisualProfile visual)
    {
        if (messageText != null)
            messageText.text = message;

        if (backgroundImage != null)
            backgroundImage.color = visual.backgroundColor;

        if (messageText != null)
            messageText.color = visual.textColor;

        if (iconImage != null)
        {
            bool hasIcon = visual.icon != null;
            iconImage.gameObject.SetActive(hasIcon);
            if (hasIcon)
                iconImage.sprite = visual.icon;
        }
    }

    private bool HasRequiredReferences()
    {
        return panelRoot != null && canvasGroup != null && messageText != null;
    }

    private void KillTweens()
    {
        moveTween?.Kill(false);
        fadeTween?.Kill(false);
        moveTween = null;
        fadeTween = null;
    }

    private IEnumerator WaitForTween(Tween tween)
    {
        if (tween == null)
            yield break;

        while (tween.IsActive() && tween.IsPlaying())
            yield return null;
    }

    private void SetY(float y)
    {
        var pos = panelRoot.anchoredPosition;
        pos.y = y;
        panelRoot.anchoredPosition = pos;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void LogMissingReferencesIfAny()
    {
        if (HasRequiredReferences())
            return;

        Debug.LogWarning(
            "[TopNotificationView] Missing references. Bind panelRoot/canvasGroup/messageText (background/icon optional).",
            this);
    }
#endif
}
