using System;
using UniRx;
using UnityEngine;

public sealed class AchievementPopupPresenter : MonoBehaviour
{
    [Header("Popup")]
    [SerializeField] private bool includePrefix = true;
    [SerializeField] private string prefixText = "Achievement Unlocked";
    [SerializeField] private bool useTopNotificationIfAvailable = true;
    [SerializeField] private bool showIcon = true;
    [SerializeField, Min(0.2f)] private float duration = 1.2f;
    [SerializeField, Min(0f)] private float topPadding = 120f;
    [SerializeField] private Vector2 fallbackAnchoredPosition = new Vector2(0f, 420f);

    private IDisposable achievementCompletedSub;
    private bool bound;

    void OnEnable()
    {
        TryBind();
    }

    void Update()
    {
        if (!bound)
            TryBind();
    }

    void OnDisable()
    {
        Unbind();
    }

    void TryBind()
    {
        if (bound)
            return;

        if (QuestManager.Ins == null)
            return;

        achievementCompletedSub = QuestManager.Ins.OnAchievementCompleted.Subscribe(ShowPopup);
        bound = true;
    }

    void Unbind()
    {
        achievementCompletedSub?.Dispose();
        achievementCompletedSub = null;
        bound = false;
    }

    void ShowPopup(QuestDef def)
    {
        if (def == null)
            return;

        string title = string.IsNullOrWhiteSpace(def.title) ? def.id : def.title;
        string message = includePrefix ? $"{prefixText}: {title}" : title;

        if (useTopNotificationIfAvailable && TopNotificationManager.Ins != null)
        {
            TopNotificationManager.NotifyAchievement(message, duration);
            return;
        }

        Sprite icon = showIcon ? def.icon : null;
        Toaster.Show(message, icon, duration, ResolveAnchoredPosition());
    }

    Vector2 ResolveAnchoredPosition()
    {
        var toaster = Toaster.Ins;
        if (toaster == null || toaster.Canvas == null)
            return fallbackAnchoredPosition;

        var root = toaster.Canvas.transform as RectTransform;
        if (root == null)
            return fallbackAnchoredPosition;

        float y = root.rect.yMax - Mathf.Max(0f, topPadding);
        return new Vector2(0f, y);
    }
}
