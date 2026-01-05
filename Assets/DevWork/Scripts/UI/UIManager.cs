using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Ins { get; private set; }

    [Header("Nav")]
    [SerializeField] private List<Button> buttons;          // 5 nút dưới
    [SerializeField] private List<RectTransform> panels;    // Page_* xếp ngang (con của uIPanel)
    [SerializeField] private RectTransform uIPanel;         // Content trượt ngang
    [SerializeField] private float duration = 0.25f;
    [SerializeField] private Ease ease = Ease.OutCubic;
    [SerializeField] private float pageWidth = 0f;          // = width của viewport

    [Header("Bottom button anim")]
    [SerializeField] private Vector3 selectedIconScale = new Vector3(1.2f, 1.2f, 1f);
    [SerializeField] private float iconRiseHeight = 20f;

    private readonly int startIndex = 2;     // vào game ở giữa
    private int currentIndex = -1;
    private Tween moveTween;
    private bool isTweening;

    [Header("Setting button")]
    private bool isOpenSetting;
    [SerializeField] private Button settingButton;

    [Header("Location")]
    [SerializeField] private Image[] locationBackground;
    [SerializeField] private Sprite[] locationTexture2D;

    void Awake()
    {
        if (Ins != null && Ins != this) { Destroy(gameObject); return; }
        Ins = this;

        // Culling theo alpha để UI alpha=0 không vẽ (giảm overdraw khi có fade)
        foreach (var g in GetComponentsInChildren<Graphic>(true))
            g.canvasRenderer.cullTransparentMesh = true;
    }

    void Start()
    {
        if (pageWidth <= 0f)
        {
            // đo theo parent/viewport nếu chưa set tay
            var vp = uIPanel.parent as RectTransform;
            pageWidth = vp ? vp.rect.width : 720f;
        }

        SetupButtons();
        // Chỉ bật trang start, tắt còn lại → batching tốt hơn
        ActivateOnly(startIndex, snap: true);
        BottomButtonAnim(startIndex);
    }

    void SetupButtons()
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            int index = i;

            // tránh duplicate listeners nếu hàm này gọi lại
            buttons[index].onClick.RemoveAllListeners();

            // Chỉ giữ raycast ở targetGraphic của button; icon/text con tắt raycast cho nhẹ input
            var keep = buttons[index].targetGraphic;
            foreach (var g in buttons[index].GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = (g == keep);

            buttons[index].onClick.AddListener(() =>
            {
                if (isTweening || currentIndex == index) return;
                SlideTo(index);
                BottomButtonAnim(index);
            });
        }
    }

    void SlideTo(int target)
    {
        target = Mathf.Clamp(target, 0, panels.Count - 1);

        // Bật chỉ các page đi ngang qua (hoặc thêm đệm ±1 nếu muốn)
        int a = Mathf.Min(currentIndex < 0 ? target : currentIndex, target);
        int b = Mathf.Max(currentIndex < 0 ? target : currentIndex, target);
        for (int i = 0; i < panels.Count; i++)
            panels[i].gameObject.SetActive(i >= a && i <= b);

        // Chặn spam click trong lúc tween
        SetButtonsInteractable(false);
        isTweening = true;

        // Tween chỉ theo trục X (tránh kéo Y → rebuild layout/giật)
        float targetX = -target * pageWidth;

        moveTween?.Kill(true);
        moveTween = uIPanel.DOAnchorPosX(targetX, duration)
                           .SetEase(ease)
                           .SetUpdate(true) // không phụ thuộc timescale nếu cần pause
                           .OnComplete(() =>
                           {
                               ActivateOnly(target, snap: false);
                               SetButtonsInteractable(true);
                               isTweening = false;
                           });
    }

    public void MoveToMain()
    {
        SlideTo(startIndex);
        BottomButtonAnim(startIndex);
    }
    void ActivateOnly(int idx, bool snap)
    {
        for (int i = 0; i < panels.Count; i++)
            panels[i].gameObject.SetActive(i == idx);

        if (snap)
            uIPanel.anchoredPosition = new Vector2(-idx * pageWidth, uIPanel.anchoredPosition.y);

        currentIndex = idx;
    }

    public void SetButtonsInteractable(bool on)
    {
        foreach (var b in buttons) b.interactable = on;
    }

    void BottomButtonAnim(int index)
    {
        for (int j = 0; j < buttons.Count; j++)
        {
            Transform icon = buttons[j].transform.GetChild(0);
            Transform text = buttons[j].transform.GetChild(1);

            // Kill tween cũ để không chồng
            icon.DOKill(true); text.DOKill(true);

            if (j == index)
            {
                icon.DOScale(selectedIconScale, 0.25f).SetEase(Ease.OutBack);
                icon.DOLocalMoveY(iconRiseHeight, 0.25f).SetEase(Ease.OutQuad);
                text.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
            }
            else
            {
                icon.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutCubic);
                icon.DOLocalMoveY(0f, 0.2f).SetEase(Ease.OutCubic);
                text.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InCubic);
            }
        }
    }

    public bool IsBlockCanClick() => startIndex == currentIndex && !isOpenSetting;

    public void SetLocationBackground(int index)
    {
        foreach (var image in locationBackground)
        {
            image.sprite = locationTexture2D[index];
        }
    }
}
