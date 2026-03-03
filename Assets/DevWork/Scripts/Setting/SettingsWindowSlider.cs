using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsWindowSlider : MonoBehaviour
{
    private enum TabIndex
    {
        General = 0,
        Info = 1,
        More = 2
    }

    [Header("Tab Buttons")]
    [SerializeField] private Button generalButton;
    [SerializeField] private Button infoButton;
    [SerializeField] private Button moreButton;

    [Header("Tab Contents")]
    [SerializeField] private RectTransform generalContent;
    [SerializeField] private RectTransform infoContent;
    [SerializeField] private RectTransform moreContent;

    [Header("Selector")]
    [SerializeField] private RectTransform selectorBar;
    [SerializeField] private float selectorDuration = 0.2f;
    [SerializeField] private Ease selectorEase = Ease.OutQuad;

    [Header("Slide")]
    [SerializeField] private RectTransform contentViewport;
    [SerializeField] private bool autoBuildIfMissing = true;
    [SerializeField] private float tabBarHeight = 100f;
    [SerializeField] private float tabBarHorizontalPadding = 24f;
    [SerializeField] private bool autoSlideOffset = true;
    [SerializeField] private float slideOffset = 900f;
    [SerializeField] private float slideDuration = 0.25f;
    [SerializeField] private Ease slideEase = Ease.OutQuad;
    [SerializeField] private bool deactivateOffscreenPanels = true;
    [SerializeField] private bool snapOnResize = true;

    private readonly Vector2[] cachedTabPos = new Vector2[3];
    private readonly Vector2[] cachedContentPos = new Vector2[3];
    private readonly RectTransform[] contents = new RectTransform[3];
    private readonly Button[] buttons = new Button[3];
    private int currentIndex = 0;

    private Tween selectorTween;
    private Tween slideTween;
    private bool contentBaseCached;

    void Awake()
    {
        if (autoBuildIfMissing && !HasRequiredReferences())
            AutoBuildLayout();

        contents[(int)TabIndex.General] = generalContent;
        contents[(int)TabIndex.Info] = infoContent;
        contents[(int)TabIndex.More] = moreContent;

        buttons[(int)TabIndex.General] = generalButton;
        buttons[(int)TabIndex.Info] = infoButton;
        buttons[(int)TabIndex.More] = moreButton;

        if (contentViewport == null && generalContent != null)
            contentViewport = generalContent.parent as RectTransform;

        CachePositions(true);
        BindButtons();
    }

    void OnEnable()
    {
        UpdateLayout(true);
        SetButtonInteractable();
    }

    void OnDisable()
    {
        selectorTween?.Kill();
        slideTween?.Kill();
    }

    void OnDestroy()
    {
        if (generalButton != null)
            generalButton.onClick.RemoveListener(ShowGeneral);
        if (infoButton != null)
            infoButton.onClick.RemoveListener(ShowInfo);
        if (moreButton != null)
            moreButton.onClick.RemoveListener(ShowMore);
    }

    void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled)
            return;

        UpdateLayout(snapOnResize);
    }

    public void ShowGeneral() => ShowTab((int)TabIndex.General);
    public void ShowInfo() => ShowTab((int)TabIndex.Info);
    public void ShowMore() => ShowTab((int)TabIndex.More);

    public void ShowTab(int index)
    {
        if (index < 0 || index >= contents.Length)
            return;
        if (index == currentIndex)
            return;
        if (contents[index] == null)
            return;

        int previous = currentIndex;
        currentIndex = index;

        MoveSelectorTo(index);
        SlideContents(previous, index);
        SetButtonInteractable();
    }

    private void BindButtons()
    {
        if (generalButton != null)
        {
            generalButton.onClick.RemoveListener(ShowGeneral);
            generalButton.onClick.AddListener(ShowGeneral);
        }
        if (infoButton != null)
        {
            infoButton.onClick.RemoveListener(ShowInfo);
            infoButton.onClick.AddListener(ShowInfo);
        }
        if (moreButton != null)
        {
            moreButton.onClick.RemoveListener(ShowMore);
            moreButton.onClick.AddListener(ShowMore);
        }
    }

    private void CachePositions(bool recacheContentBase)
    {
        CacheTabPosition((int)TabIndex.General, generalButton);
        CacheTabPosition((int)TabIndex.Info, infoButton);
        CacheTabPosition((int)TabIndex.More, moreButton);

        if (recacheContentBase || !contentBaseCached)
        {
            CacheContentPosition((int)TabIndex.General, generalContent);
            CacheContentPosition((int)TabIndex.Info, infoContent);
            CacheContentPosition((int)TabIndex.More, moreContent);
            contentBaseCached = true;
        }
    }

    private void CacheTabPosition(int index, Button button)
    {
        if (button == null)
            return;

        var rt = button.transform as RectTransform;
        if (rt != null)
            cachedTabPos[index] = rt.anchoredPosition;
    }

    private void CacheContentPosition(int index, RectTransform content)
    {
        if (content == null)
            return;
        cachedContentPos[index] = content.anchoredPosition;
    }

    private void UpdateLayout(bool snap)
    {
        if (contentViewport == null && generalContent != null)
            contentViewport = generalContent.parent as RectTransform;

        CachePositions(false);

        if (autoSlideOffset && contentViewport != null)
            slideOffset = Mathf.Max(1f, contentViewport.rect.width);

        if (snap)
            SnapToCurrent();
    }

    private void MoveSelectorTo(int index)
    {
        if (selectorBar == null)
            return;

        selectorTween?.Kill();
        float targetX = cachedTabPos[index].x;
        selectorTween = selectorBar.DOAnchorPosX(targetX, selectorDuration).SetEase(selectorEase);
    }

    private void SlideContents(int fromIndex, int toIndex)
    {
        slideTween?.Kill();

        int dir = toIndex > fromIndex ? 1 : -1;
        Vector2 offRight = new Vector2(slideOffset, 0f);
        Vector2 offLeft = -offRight;

        RectTransform toContent = contents[toIndex];
        RectTransform fromContent = contents[fromIndex];

        if (toContent == null)
            return;

        if (fromContent != null)
            fromContent.gameObject.SetActive(true);
        toContent.gameObject.SetActive(true);

        toContent.anchoredPosition = cachedContentPos[toIndex] + (dir > 0 ? offRight : offLeft);

        var sequence = DOTween.Sequence();
        sequence.Join(toContent.DOAnchorPos(cachedContentPos[toIndex], slideDuration).SetEase(slideEase));

        if (fromContent != null)
            sequence.Join(fromContent.DOAnchorPos(cachedContentPos[fromIndex] + (dir > 0 ? offLeft : offRight), slideDuration).SetEase(slideEase));

        sequence.OnComplete(() =>
        {
            if (!deactivateOffscreenPanels)
                return;

            for (int i = 0; i < contents.Length; i++)
            {
                RectTransform panel = contents[i];
                if (panel != null)
                    panel.gameObject.SetActive(i == currentIndex);
            }
        });

        slideTween = sequence;
    }

    private void SnapToCurrent()
    {
        Vector2 offRight = new Vector2(slideOffset, 0f);
        Vector2 offLeft = -offRight;

        for (int i = 0; i < contents.Length; i++)
        {
            RectTransform panel = contents[i];
            if (panel == null)
                continue;

            if (i == currentIndex)
            {
                panel.anchoredPosition = cachedContentPos[i];
                panel.gameObject.SetActive(true);
                continue;
            }

            panel.anchoredPosition = cachedContentPos[i] + (i < currentIndex ? offLeft : offRight);
            if (deactivateOffscreenPanels)
                panel.gameObject.SetActive(false);
        }

        if (selectorBar != null)
            selectorBar.anchoredPosition = new Vector2(cachedTabPos[currentIndex].x, selectorBar.anchoredPosition.y);
    }

    private void SetButtonInteractable()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].interactable = i != currentIndex;
        }
    }

    private bool HasRequiredReferences()
    {
        return generalButton != null &&
               infoButton != null &&
               moreButton != null &&
               generalContent != null &&
               infoContent != null &&
               moreContent != null;
    }

    private void AutoBuildLayout()
    {
        RectTransform root = transform as RectTransform;
        if (root == null)
            return;

        RectTransform tabBar = FindDirectChildRect(root, "SettingsTabBar");
        if (tabBar == null)
            tabBar = CreateRectChild(root, "SettingsTabBar");
        SetTabBarLayout(tabBar);

        if (generalButton == null)
            generalButton = CreateTabButton(tabBar, "Tab_General", "General", 0);
        if (infoButton == null)
            infoButton = CreateTabButton(tabBar, "Tab_Info", "Info", 1);
        if (moreButton == null)
            moreButton = CreateTabButton(tabBar, "Tab_More", "More", 2);

        if (selectorBar == null)
        {
            RectTransform selector = FindDirectChildRect(tabBar, "TabSelector");
            if (selector == null)
                selector = CreateRectChild(tabBar, "TabSelector");

            var img = selector.GetComponent<Image>();
            if (img == null)
                img = selector.gameObject.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.9f);

            selector.anchorMin = new Vector2(0f, 0f);
            selector.anchorMax = new Vector2(1f / 3f, 0f);
            selector.pivot = new Vector2(0.5f, 0f);
            selector.anchoredPosition = Vector2.zero;
            selector.sizeDelta = new Vector2(0f, 6f);
            selectorBar = selector;
        }

        if (contentViewport == null)
        {
            contentViewport = FindDirectChildRect(root, "SettingsViewport");
            if (contentViewport == null)
                contentViewport = CreateRectChild(root, "SettingsViewport");
        }
        SetViewportLayout(contentViewport);

        if (generalContent == null)
            generalContent = EnsureContentPanel(contentViewport, "GeneralContent");
        if (infoContent == null)
            infoContent = EnsureContentPanel(contentViewport, "InfoContent");
        if (moreContent == null)
            moreContent = EnsureContentPanel(contentViewport, "MoreContent");

        MoveLegacyChildrenToPanels(root, tabBar, contentViewport, generalContent, infoContent, moreContent);
    }

    private static RectTransform FindDirectChildRect(RectTransform parent, string childName)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (c != null && c.name == childName)
                return c as RectTransform;
        }
        return null;
    }

    private static RectTransform CreateRectChild(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private void SetTabBarLayout(RectTransform tabBar)
    {
        if (tabBar == null)
            return;

        tabBar.anchorMin = new Vector2(0f, 1f);
        tabBar.anchorMax = new Vector2(1f, 1f);
        tabBar.pivot = new Vector2(0.5f, 1f);
        tabBar.anchoredPosition = Vector2.zero;
        tabBar.sizeDelta = new Vector2(0f, tabBarHeight);
    }

    private void SetViewportLayout(RectTransform viewport)
    {
        if (viewport == null)
            return;

        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
    }

    private RectTransform EnsureContentPanel(RectTransform parent, string name)
    {
        RectTransform panel = FindDirectChildRect(parent, name);
        if (panel == null)
            panel = CreateRectChild(parent, name);

        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        panel.localScale = Vector3.one;
        return panel;
    }

    private Button CreateTabButton(RectTransform tabBar, string name, string label, int index)
    {
        RectTransform rect = FindDirectChildRect(tabBar, name);
        if (rect == null)
            rect = CreateRectChild(tabBar, name);

        rect.anchorMin = new Vector2(index / 3f, 0f);
        rect.anchorMax = new Vector2((index + 1f) / 3f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(tabBarHorizontalPadding * 0.5f, 12f);
        rect.offsetMax = new Vector2(-tabBarHorizontalPadding * 0.5f, -12f);

        Image image = rect.GetComponent<Image>();
        if (image == null)
            image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.28f);

        Button button = rect.GetComponent<Button>();
        if (button == null)
            button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        Transform labelTr = rect.Find("Label");
        RectTransform labelRect = labelTr as RectTransform;
        if (labelRect == null)
            labelRect = CreateRectChild(rect, "Label");

        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);

        TextMeshProUGUI tmp = labelRect.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 30f;
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return button;
    }

    private static void MoveLegacyChildrenToPanels(
        RectTransform root,
        RectTransform tabBar,
        RectTransform viewport,
        RectTransform generalPanel,
        RectTransform infoPanel,
        RectTransform morePanel)
    {
        if (root == null || generalPanel == null || infoPanel == null || morePanel == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;
            if (child == tabBar || child == viewport)
                continue;

            RectTransform childRect = child as RectTransform;
            if (childRect == null)
                continue;

            RectTransform target = ResolveTargetPanel(child.name, generalPanel, infoPanel, morePanel);
            if (target == null)
                target = generalPanel;

            childRect.SetParent(target, false);
        }
    }

    private static RectTransform ResolveTargetPanel(
        string childName,
        RectTransform generalPanel,
        RectTransform infoPanel,
        RectTransform morePanel)
    {
        if (string.IsNullOrEmpty(childName))
            return generalPanel;

        switch (childName)
        {
            case "Text (TMP)":
            case "InputField (TMP)":
            case "Save":
                return infoPanel;
            case "Giftcode":
            case "Text (TMP) (1)":
            case "Button":
            case "Status":
                return morePanel;
            default:
                return generalPanel;
        }
    }
}
