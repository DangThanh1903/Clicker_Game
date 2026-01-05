using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

public class SplashManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string nextSceneName = "MainMenu";
    [SerializeField] private bool autoActivateSceneWhenLoaded = true;

    [Header("Title Animation")]
    [SerializeField] private RectTransform titleRect;
    [SerializeField] private float popDuration = 0.6f;
    [SerializeField] private float idleScale = 1.05f;
    [SerializeField] private float idleDuration = 1.5f;
    [SerializeField] private Ease popEase = Ease.OutBack;

    [Header("Fade")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeInDuration = 0.4f;

    [Header("Loading UI")]
    [SerializeField] private Image progressFillImage;  // MUST be set to Filled
    [SerializeField] private Text progressText;

    private AsyncOperation _loadOp;

    private void Awake()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (titleRect != null)
            titleRect.localScale = Vector3.zero;

        if (progressFillImage != null)
            progressFillImage.fillAmount = 0f;
    }

    private void Start()
    {
        _loadOp = SceneManager.LoadSceneAsync(nextSceneName);
        _loadOp.allowSceneActivation = false;

        PlayIntro();
    }

    private void PlayIntro()
    {
        Sequence seq = DOTween.Sequence();

        // Fade in
        seq.Append(canvasGroup.DOFade(1f, fadeInDuration));

        // Title pop
        seq.Join(titleRect.DOScale(1f, popDuration).SetEase(popEase));

        // Idle breathing loop
        seq.OnComplete(() =>
        {
            titleRect.DOScale(idleScale, idleDuration)
                     .SetEase(Ease.InOutSine)
                     .SetLoops(-1, LoopType.Yoyo);
        });
    }

    private void Update()
    {
        if (_loadOp == null) return;

        float rawProgress = _loadOp.progress;
        float normalized = Mathf.Clamp01(rawProgress / 0.9f);

        // Update filled bar
        if (progressFillImage != null)
            progressFillImage.fillAmount = normalized;

        // Update %
        if (progressText != null)
            progressText.text = Mathf.RoundToInt(normalized * 100f) + "%";

        if (normalized >= 1f && autoActivateSceneWhenLoaded)
        {
            _loadOp.allowSceneActivation = true;
        }
    }
}
