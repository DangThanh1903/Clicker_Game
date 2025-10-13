using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;
using System.Collections.Generic;

public class CaseRollerUI : MonoBehaviour
{
    [Header("UI")]
    public RectTransform content;
    public CaseItemCard cardPrefab;
    [Header("Lootbox Icon")]
    public Image lootboxIcon;
    public float openScale = 1.2f;
    public float openDuration = 0.3f;

    [Header("Layout")]
    [Min(1)] public int visibleWindow = 7;
    public float itemWidth = 160f;

    [Header("Anim")]
    public float spinTime = 3.5f;
    public Ease spinEase = Ease.OutCubic;
    public float overshootItems = 0.25f;

    public Action<Item, int> OnLanded;

    // Internal
    private readonly List<CaseItemCard> _slots = new();
    private float _center;
    private Tween _spinTween;
    private CaseRollPayload _payload;

    void Awake()
    {

        if (content)
        {
            var size = content.sizeDelta;
            content.sizeDelta = new Vector2(visibleWindow * itemWidth, size.y);
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot    = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;
        }

        BuildSlots();
    }

    private void BuildSlots()
    {
        // Clear old
        for (int i = _slots.Count - 1; i >= 0; --i)
            if (_slots[i]) Destroy(_slots[i].gameObject);
        _slots.Clear();

        // Create exactly visibleWindow cards
        for (int i = 0; i < visibleWindow; i++)
        {
            var slot = Instantiate(cardPrefab, content);
            var rt = slot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            _slots.Add(slot);
        }
    }

    public void BuildAndSpin(CaseRollPayload payload)
    {
        if (payload.reel == null || payload.reel.Count == 0) { return; }
        _payload = payload;

        _center = 0f;
        RenderAt(_center, true);

        var seq = DOTween.Sequence();

        // Open anim (icon)
        if (lootboxIcon && payload.sourceBox)
        {
            lootboxIcon.gameObject.SetActive(true);
            lootboxIcon.sprite = payload.sourceBox.icon;
            lootboxIcon.transform.localScale = Vector3.one;
            lootboxIcon.color = Color.white;

            seq.Append(lootboxIcon.transform.DOScale(openScale, openDuration).SetEase(Ease.OutBack));
            seq.Append(lootboxIcon.transform.DOScale(1f, 0.2f).SetEase(Ease.InOutSine));
            seq.Append(lootboxIcon.DOFade(0f, 0.15f));
            seq.AppendCallback(() => {
                lootboxIcon.gameObject.SetActive(false);
                var c = lootboxIcon.color; c.a = 1f; lootboxIcon.color = c;
            });
        }

        // Spin tweens (happen after icon anim because we Append them)
        float target = payload.targetIndex;
        seq.Append(DOTween.To(() => _center, v => { _center = v; RenderAt(_center); },
                            target + overshootItems, spinTime * 0.92f).SetEase(spinEase));
        seq.Append(DOTween.To(() => _center, v => { _center = v; RenderAt(_center); },
                            target,              spinTime * 0.08f).SetEase(Ease.OutSine));

        seq.OnComplete(() => {
            RenderAt(target, true);
            OnLanded?.Invoke(_payload.item, _payload.amount);
        });

        _spinTween?.Kill();
        _spinTween = seq;
    }


    public void Skip()
    {
        _spinTween?.Complete(true);
    }

    // Draw the 7 visible slots for a given center float position
    private void RenderAt(float center, bool immediateAssignAll = false)
    {
        if (_payload.reel == null || _payload.reel.Count == 0) return;

        // Compute leftmost visible item index and how far the first slot is shifted
        float halfWin = (visibleWindow * 0.5f) - 0.5f;
        float startF  = center - halfWin;                       // first visible item “float index”
        int firstIdx  = Mathf.FloorToInt(startF);               // integer index of the first slot
        float frac    = startF - firstIdx;                      // fractional offset [0..1)
        int reelCount = _payload.reel.Count;

        // Layout: slots k = 0..visibleWindow-1
        // x = (k - frac) * itemWidth  (so items slide smoothly by frac)
        for (int k = 0; k < _slots.Count; k++)
        {
            int itemIndex = firstIdx + k;
            int reelIndex = PositiveMod(itemIndex, reelCount);

            var slot = _slots[k];
            var rt = slot.GetComponent<RectTransform>();
            float x = (k - frac - (visibleWindow * 0.5f - 0.5f)) * itemWidth; // centered on content
            rt.anchoredPosition = new Vector2(x, 0f);

            // Only reassign visuals if necessary; but with 7 slots, cheap to assign each frame.
            int amount = (reelIndex == _payload.targetIndex) ? _payload.amount : 1;
            slot.Setup(_payload.reel[reelIndex], amount);
        }
    }

    private static int PositiveMod(int a, int m)
    {
        int r = a % m;
        if (r < 0) r += m;
        return r;
    }
}
