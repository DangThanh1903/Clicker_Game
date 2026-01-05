using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class UITabSwitcherTween : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button progressTabButton;
    [SerializeField] private Button dailyTabButton;

    [Header("Tab Contents")]
    [SerializeField] private RectTransform progressTabContent;
    [SerializeField] private RectTransform dailyTabContent;

    [Header("Selector (underline bar)")]
    [SerializeField] private RectTransform selectorBar;
    [SerializeField] private float selectorMoveDuration = 0.2f;
    [SerializeField] private Ease selectorEase = Ease.OutQuad;

    [Header("Content Slide")]
    [SerializeField] private float contentSlideDuration = 0.25f;
    [SerializeField] private Ease contentEase = Ease.OutQuad;
    [SerializeField] private float slideOffset = 800f; // how far offscreen to slide

    private Vector2 _progressTabPos;
    private Vector2 _dailyTabPos;

    private Vector2 _progressContentPos;
    private Vector2 _dailyContentPos;

    private Tween _selectorTween;
    private Tween _contentTween;

    private int _currentTab = 0; // 0=progress, 1=daily

    private void Awake()
    {
        // Cache positions
        _progressTabPos = ((RectTransform)progressTabButton.transform).anchoredPosition;
        _dailyTabPos = ((RectTransform)dailyTabButton.transform).anchoredPosition;

        _progressContentPos = progressTabContent.anchoredPosition;
        _dailyContentPos = dailyTabContent.anchoredPosition;

        // Button listeners
        progressTabButton.onClick.AddListener(() => ShowTab(0));
        dailyTabButton.onClick.AddListener(() => ShowTab(1));
    }

    private void OnEnable()
    {
        // Default to progress tab
        // Put progress in center, daily to the right initially
        _currentTab = 0;
        progressTabContent.anchoredPosition = _progressContentPos;
        dailyTabContent.anchoredPosition = _dailyContentPos + new Vector2(slideOffset, 0f);

        // Move selector under progress tab
        if (selectorBar != null)
        {
            selectorBar.anchoredPosition = new Vector2(_progressTabPos.x, selectorBar.anchoredPosition.y);
        }

        UpdateButtonInteractable();
    }

    private void OnDisable()
    {
        _selectorTween?.Kill();
        _contentTween?.Kill();
    }

    private void ShowTab(int index)
    {
        if (_currentTab == index) return;
        int previous = _currentTab;
        _currentTab = index;

        // 1) Move selector bar
        if (selectorBar != null)
        {
            _selectorTween?.Kill();

            float targetX = (index == 0) ? _progressTabPos.x : _dailyTabPos.x;

            _selectorTween = selectorBar.DOAnchorPosX(targetX, selectorMoveDuration)
                .SetEase(selectorEase);
        }

        // 2) Slide content panels
        _contentTween?.Kill();

        // From left/right based on direction
        bool goingToDaily = (index == 1);

        Vector2 progressTargetPos;
        Vector2 dailyTargetPos;

        if (goingToDaily)
        {
            // progress slides left, daily slides in from right
            progressTargetPos = _progressContentPos - new Vector2(slideOffset, 0f);
            dailyTargetPos = _dailyContentPos;
        }
        else
        {
            // daily slides right, progress slides in from left
            progressTargetPos = _progressContentPos;
            dailyTargetPos = _dailyContentPos + new Vector2(slideOffset, 0f);
        }

        // Ensure both active while animating
        progressTabContent.gameObject.SetActive(true);
        dailyTabContent.gameObject.SetActive(true);

        _contentTween = DOTween.Sequence()
            .Join(progressTabContent.DOAnchorPos(progressTargetPos, contentSlideDuration).SetEase(contentEase))
            .Join(dailyTabContent.DOAnchorPos(dailyTargetPos, contentSlideDuration).SetEase(contentEase))
            .OnComplete(() =>
            {
                // After sliding, you can optionally deactivate offscreen panel
                if (_currentTab == 0)
                {
                    // progress visible
                    progressTabContent.gameObject.SetActive(true);
                    dailyTabContent.gameObject.SetActive(false);
                }
                else
                {
                    progressTabContent.gameObject.SetActive(false);
                    dailyTabContent.gameObject.SetActive(true);
                }
            });

        UpdateButtonInteractable();
    }

    private void UpdateButtonInteractable()
    {
        // Active tab = not interactable (looks "selected")
        progressTabButton.interactable = _currentTab != 0;
        dailyTabButton.interactable = _currentTab != 1;
    }
}
