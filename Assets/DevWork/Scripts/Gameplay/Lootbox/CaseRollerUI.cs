using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;
using System.Collections.Generic;

public class CaseRollerUI : MonoBehaviour
{
    [Header("UI")]
    public ScrollRect scrollRect;
    public RectTransform content;
    public CaseItemCard cardPrefab;            // simple icon+name view

    [Header("Layout")]
    public float itemWidth = 160f;             // set to your card width (including spacing)
    public int visibleWindow = 7;              // how many items visible across (for centering)
    public float extraContentPadding = 800f;   // optional: extra pad to avoid snapping edges

    [Header("Anim")]
    public float spinTime = 3.5f;
    public Ease spinEase = Ease.OutCubic;
    public float bounce = 40f;                 // small overshoot

    // Events
    public Action<Item, int> OnLanded;         // fire when the reel stops

    // Internal
    private List<CaseItemCard> _spawned = new();
    private Tween _spinTween;

    public void BuildAndSpin(CaseRollPayload payload)
    {
        Clear();

        // 1) Build cards
        for (int i = 0; i < payload.reel.Count; i++)
        {
            var card = Instantiate(cardPrefab, content);

            // If this index is the winning one → show real amount
            int amt = (i == payload.targetIndex) ? payload.amount : 1;

            card.Setup(payload.reel[i], amt);
            _spawned.Add(card);
        }


        // Optional: pad content for safety
        var size = content.sizeDelta;
        content.sizeDelta = new Vector2(payload.reel.Count * itemWidth + extraContentPadding, size.y);

        // 2) Reset scroll to the start
        scrollRect.horizontalNormalizedPosition = 0f;
        content.anchoredPosition = Vector2.zero;

        // 3) Compute target X so the targetIndex is centered in the viewport.
        float centerOffset = (visibleWindow * 0.5f - 0.5f) * itemWidth;
        float targetX = payload.targetIndex * itemWidth - centerOffset;

        // 4) Add a tiny overshoot for juice, then bounce back
        float overshootX = targetX + bounce;

        // Stop any previous tween
        _spinTween?.Kill();

        // 5) Animate
        _spinTween = DOTween.Sequence()
            .Append(content.DOAnchorPosX(-overshootX, spinTime * 0.92f).SetEase(spinEase))
            .Append(content.DOAnchorPosX(-targetX, spinTime * 0.08f).SetEase(Ease.OutSine))
            .OnComplete(() =>
            {
                // Safety snap
                content.anchoredPosition = new Vector2(-targetX, content.anchoredPosition.y);
                OnLanded?.Invoke(payload.item, payload.amount);
            });
    }

    public void Skip()
    {
        _spinTween?.Complete(true);
    }

    public void Clear()
    {
        _spinTween?.Kill();
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i]) Destroy(_spawned[i].gameObject);
        _spawned.Clear();
    }
}
