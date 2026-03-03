using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialOverlayView : MonoBehaviour
{
    private const float DefaultHandZOffset = -90f;

    [Header("Refs")]
    [SerializeField] private RectTransform characterRoot;
    [SerializeField] private RectTransform messageBubbleRoot;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private RectTransform handStick;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Button nextButton;

    [Header("Character DOTween")]
    [SerializeField] private bool characterBreathEnabled = true;
    [SerializeField, Min(0.01f)] private float characterBreathDuration = 1.1f;
    [SerializeField, Min(0f)] private float characterBreathScale = 0.03f;

    [Header("Hand Pointer")]
    [SerializeField] private bool rotateHandToTarget = true;

    [Header("Layout")]
    [SerializeField] private bool avoidCoverTarget = true;
    [SerializeField, Range(0f, 0.45f)] private float contentPaddingNormalized = 0.12f;
    [SerializeField, Range(0.02f, 0.4f)] private float contentTargetGapNormalized = 0.16f;

    private Tween characterBreathTween;
    private RectTransform currentTarget;
    private Vector2 fallbackNormalized = new Vector2(0.5f, 0.5f);
    private float handBaseAngle;
    private float baseAnchorMinX;
    private float baseAnchorMaxX;
    private float baseAnchoredPosX;
    private float bubbleBaseY;
    private float bubbleBaseZ;
    private float textBaseY;
    private float textBaseZ;
    private float nextBaseY;
    private float nextBaseZ;
    private bool nextPressed;
    private bool showHandPointer = true;

    private void Awake()
    {
        if (handStick != null)
            handBaseAngle = handStick.localEulerAngles.z;

        if (messageBubbleRoot == null)
            messageBubbleRoot = contentRoot;

        if (contentRoot != null)
        {
            baseAnchorMinX = contentRoot.anchorMin.x;
            baseAnchorMaxX = contentRoot.anchorMax.x;
            baseAnchoredPosX = contentRoot.anchoredPosition.x;
        }

        if (messageBubbleRoot != null)
        {
            Vector3 euler = messageBubbleRoot.localEulerAngles;
            bubbleBaseY = euler.y;
            bubbleBaseZ = euler.z;
        }

        if (messageText != null)
        {
            Vector3 euler = messageText.rectTransform.localEulerAngles;
            textBaseY = euler.y;
            textBaseZ = euler.z;
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(() => nextPressed = true);
            nextButton.gameObject.SetActive(false);

            Vector3 euler = nextButton.transform.localEulerAngles;
            nextBaseY = euler.y;
            nextBaseZ = euler.z;
        }

        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!gameObject.activeSelf)
            return;

        UpdateBubbleOrientation();

        if (avoidCoverTarget)
            UpdateContentRootPlacement();

        if (rotateHandToTarget && showHandPointer)
            UpdateHandRotation();
    }

    public void Show(string message, RectTransform target, Vector2 fallbackTargetNormalized, bool showNextButton = false, bool showHand = true)
    {
        if (messageText != null)
            messageText.text = message ?? string.Empty;

        currentTarget = target;
        fallbackNormalized = fallbackTargetNormalized;
        nextPressed = false;
        showHandPointer = showHand;

        if (nextButton != null)
            nextButton.gameObject.SetActive(showNextButton);
        if (handStick != null)
            handStick.gameObject.SetActive(showHandPointer);

        gameObject.SetActive(true);
        StartCharacterBreath();

        UpdateBubbleOrientation();

        if (avoidCoverTarget)
            UpdateContentRootPlacement();

        if (rotateHandToTarget && showHandPointer)
            UpdateHandRotation();
    }

    public void Hide()
    {
        currentTarget = null;
        nextPressed = false;
        showHandPointer = true;
        StopTweens();

        if (nextButton != null)
            nextButton.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    public bool ConsumeNextPressed()
    {
        if (!nextPressed)
            return false;

        nextPressed = false;
        return true;
    }

    private void StartCharacterBreath()
    {
        if (!characterBreathEnabled || characterRoot == null)
            return;
        if (characterBreathTween != null && characterBreathTween.IsActive())
            return;

        float amount = Mathf.Max(0f, characterBreathScale);
        if (amount <= 0f)
            return;

        characterRoot.localScale = Vector3.one;
        characterBreathTween = characterRoot
            .DOScale(1f + amount, Mathf.Max(0.01f, characterBreathDuration))
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void StopTweens()
    {
        if (characterBreathTween != null)
        {
            characterBreathTween.Kill(false);
            characterBreathTween = null;
        }

        if (characterRoot != null)
            characterRoot.localScale = Vector3.one;
    }

    private void UpdateHandRotation()
    {
        if (handStick == null)
            return;

        Vector2 targetScreenPos = GetTargetScreenPosition();
        Camera handCamera = GetCanvasCamera(handStick);
        Vector2 handScreenPos = RectTransformUtility.WorldToScreenPoint(handCamera, handStick.position);
        Vector2 direction = targetScreenPos - handScreenPos;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + handBaseAngle + DefaultHandZOffset;
        handStick.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void UpdateContentRootPlacement()
    {
        if (contentRoot == null)
            return;

        Vector2 targetNormalized = GetTargetNormalized();
        float p = Mathf.Clamp01(contentPaddingNormalized);
        float gap = Mathf.Clamp(contentTargetGapNormalized, 0.02f, 0.4f);
        bool targetInBottomHalf = targetNormalized.y <= 0.5f;
        float y = targetInBottomHalf
            ? targetNormalized.y + gap
            : targetNormalized.y - gap;
        y = Mathf.Clamp(y, p, 1f - p);

        Vector2 min = contentRoot.anchorMin;
        Vector2 max = contentRoot.anchorMax;
        min.x = baseAnchorMinX;
        max.x = baseAnchorMaxX;
        min.y = y;
        max.y = y;
        contentRoot.anchorMin = min;
        contentRoot.anchorMax = max;
        contentRoot.anchoredPosition = new Vector2(baseAnchoredPosX, 0f);

    }

    private Vector2 GetTargetScreenPosition()
    {
        if (currentTarget != null)
        {
            Camera targetCamera = GetCanvasCamera(currentTarget);
            return RectTransformUtility.WorldToScreenPoint(targetCamera, currentTarget.position);
        }

        return new Vector2(
            fallbackNormalized.x * Screen.width,
            fallbackNormalized.y * Screen.height);
    }

    private Vector2 GetTargetNormalized()
    {
        if (Screen.width <= 0 || Screen.height <= 0)
            return fallbackNormalized;

        Vector2 screenPos = GetTargetScreenPosition();
        return new Vector2(
            Mathf.Clamp01(screenPos.x / Screen.width),
            Mathf.Clamp01(screenPos.y / Screen.height));
    }

    private void UpdateBubbleOrientation()
    {
        bool targetInBottomHalf = GetTargetNormalized().y <= 0.5f;
        float desiredX = targetInBottomHalf ? 180f : 0f;
        ApplyBubbleOrientation(desiredX);
    }

    private void ApplyBubbleOrientation(float xRotation)
    {
        if (messageBubbleRoot != null)
            messageBubbleRoot.localRotation = Quaternion.Euler(xRotation, bubbleBaseY, bubbleBaseZ);

        if (messageText != null)
            messageText.rectTransform.localRotation = Quaternion.Euler(xRotation, textBaseY, textBaseZ);

        if (nextButton != null)
            nextButton.transform.localRotation = Quaternion.Euler(xRotation, nextBaseY, nextBaseZ);
    }

    private static Camera GetCanvasCamera(RectTransform rect)
    {
        if (rect == null)
            return null;

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null)
            return null;

        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }
}
