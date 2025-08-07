using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Ins { get; private set; }

    [SerializeField] private List<Button> buttons;
    [SerializeField] private List<RectTransform> panels;
    [SerializeField] private RectTransform uIPanel;
    [SerializeField] private Button settingButton;

    [SerializeField] private Vector3 selectedIconScale = new Vector3(1.2f, 1.2f, 1f);
    [SerializeField] private float iconRiseHeight = 20f;

    private int indexOfMenu = 2;
    private int CurrentIndex = -1;

    void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }
        Ins = this;
    }

    void Start()
    {
        UIMenuSettingUp();
    }

    void UIMenuSettingUp()
    {
        BottomButtonAnim(indexOfMenu);
        for (int i = 0; i < panels.Count; i++)
        {
            int index = i; // capture value
            buttons[index].onClick.AddListener(() =>
            {
                Vector2 targetPos = panels[index].anchoredPosition;

                // Flip the direction
                Vector2 flippedPos = new Vector2(-targetPos.x, -targetPos.y);

                uIPanel.DOAnchorPos(flippedPos, 0.3f).SetEase(Ease.OutCubic);

                BottomButtonAnim(index);
            });
        }
    }

    void BottomButtonAnim(int index)
    {
        for (int j = 0; j < buttons.Count; j++)
        {
            Transform icon = buttons[j].transform.GetChild(0);
            Transform text = buttons[j].transform.GetChild(1);

            if (j == index)
            {
                // Selected button: scale up & move up
                icon.DOScale(selectedIconScale, 0.3f).SetEase(Ease.OutBack);
                icon.DOLocalMoveY(iconRiseHeight, 0.3f).SetEase(Ease.OutQuad);
                text.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
            else
            {
                // Reset other icons
                icon.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutCubic);
                icon.DOLocalMoveY(0f, 0.3f).SetEase(Ease.OutCubic);
                text.localScale = Vector3.zero;
            }
        }
        CurrentIndex = index;
    }

    public bool IsMenuPanel() => indexOfMenu == CurrentIndex;
}
