using System;
using System.Collections;
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

    private QuestManager boundQuestManager;
    private Coroutine bindCo;
    private bool loggedMissingQuestManager;

    private void OnEnable()
    {
        TryBind();
        if (boundQuestManager == null && bindCo == null)
            bindCo = StartCoroutine(BindNextFrame());
    }

    private void OnDisable()
    {
        if (bindCo != null)
        {
            StopCoroutine(bindCo);
            bindCo = null;
        }

        Unbind();
    }

    private IEnumerator BindNextFrame()
    {
        yield return null;
        bindCo = null;
        TryBind();
    }

    private void TryBind()
    {
        if (boundQuestManager == QuestManager.Ins && boundQuestManager != null)
            return;

        Unbind();

        QuestManager manager = QuestManager.Ins;
        if (manager == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!loggedMissingQuestManager)
            {
                loggedMissingQuestManager = true;
                Debug.LogWarning("[QuestDebug] TopNotificationQuestBridge waiting for QuestManager.", this);
            }
#endif
            return;
        }

        boundQuestManager = manager;
        boundQuestManager.QuestCompleted += OnQuestCompleted;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[QuestDebug] TopNotificationQuestBridge bound to QuestManager.", this);
#endif
    }

    private void Unbind()
    {
        if (boundQuestManager != null)
            boundQuestManager.QuestCompleted -= OnQuestCompleted;

        boundQuestManager = null;
    }

    private void OnQuestCompleted(QuestRuntimeEntry entry)
    {
        QuestDef def = entry.Def;
        if (def == null)
            return;

        string title = string.IsNullOrWhiteSpace(def.title) ? def.id : def.title;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[QuestDebug] TopNotificationQuestBridge received quest completed id={def.id} achievement={def.IsAchievement} title={title}", this);
#endif
        if (def.IsAchievement)
        {
            if (!notifyAchievementComplete)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[QuestDebug] Achievement notification skipped: notifyAchievementComplete is false.", this);
#endif
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[QuestDebug] Sending achievement top notification: {achievementPrefix}: {title}", this);
#endif
            TopNotificationManager.NotifyAchievement($"{achievementPrefix}: {title}", duration);
            return;
        }

        if (!notifyQuestComplete)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[QuestDebug] Quest notification skipped: notifyQuestComplete is false.", this);
#endif
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[QuestDebug] Sending quest top notification: {questPrefix}: {title}", this);
#endif
        TopNotificationManager.NotifyQuest($"{questPrefix}: {title}", duration);
    }
}
