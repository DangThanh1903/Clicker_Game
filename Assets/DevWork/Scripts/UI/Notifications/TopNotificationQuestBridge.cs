using System;
using UniRx;
using UnityEngine;

public sealed class TopNotificationQuestBridge : MonoBehaviour
{
    [Header("Enable Types")]
    [SerializeField] private bool notifyQuestComplete = true;
    [SerializeField] private bool notifyAchievementComplete = true;

    [Header("Message")]
    [SerializeField] private string questPrefix = "Quest completed";
    [SerializeField] private string achievementPrefix = "Achievement completed";
    [SerializeField, Min(0.2f)] private float duration = 1.5f;

    private IDisposable questCompletedSub;
    private bool bound;

    private void OnEnable()
    {
        TryBind();
    }

    private void Update()
    {
        if (!bound)
            TryBind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void TryBind()
    {
        if (bound || QuestManager.Ins == null)
            return;

        questCompletedSub = QuestManager.Ins.OnQuestCompleted.Subscribe(OnQuestCompleted);
        bound = true;
    }

    private void Unbind()
    {
        questCompletedSub?.Dispose();
        questCompletedSub = null;
        bound = false;
    }

    private void OnQuestCompleted(QuestDef def)
    {
        if (def == null)
            return;

        string title = string.IsNullOrWhiteSpace(def.title) ? def.id : def.title;
        if (def.isAchievement)
        {
            if (!notifyAchievementComplete)
                return;

            TopNotificationManager.NotifyAchievement($"{achievementPrefix}: {title}", duration);
            return;
        }

        if (!notifyQuestComplete)
            return;

        TopNotificationManager.NotifyQuest($"{questPrefix}: {title}", duration);
    }
}
