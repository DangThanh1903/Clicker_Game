using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TopNotificationType
{
    Generic = 0,
    Friend = 1,
    Quest = 2,
    Achievement = 3
}

[System.Serializable]
public struct TopNotificationRequest
{
    public TopNotificationType type;
    public string message;
    public float duration;

    public TopNotificationRequest(TopNotificationType type, string message, float duration = 0f)
    {
        this.type = type;
        this.message = message;
        this.duration = duration;
    }
}

[System.Serializable]
public struct TopNotificationVisualProfile
{
    public TopNotificationType type;
    public Color backgroundColor;
    public Color textColor;
    public Sprite icon;
}

public sealed class TopNotificationManager : MonoBehaviour
{
    // Allowed global owner: top notification presentation queue.
    public static TopNotificationManager Ins { get; private set; }

    [Header("References")]
    [SerializeField] private TopNotificationView viewPrefab;
    [SerializeField] private RectTransform viewContainer;

    [Header("Timing")]
    [SerializeField, Min(0.2f)] private float defaultDuration = 1.35f;
    [SerializeField, Min(0f)] private float duplicateSuppressWindow = 0.25f;

    [Header("Theme")]
    [SerializeField] private TopNotificationVisualProfile defaultVisual = new TopNotificationVisualProfile
    {
        type = TopNotificationType.Generic,
        backgroundColor = new Color(0f, 0f, 0f, 0.82f),
        textColor = Color.white,
        icon = null
    };
    [SerializeField] private List<TopNotificationVisualProfile> visuals = new List<TopNotificationVisualProfile>();

    private readonly Queue<TopNotificationRequest> queue = new Queue<TopNotificationRequest>();
    private readonly Dictionary<TopNotificationType, TopNotificationVisualProfile> visualMap = new Dictionary<TopNotificationType, TopNotificationVisualProfile>();
    private TopNotificationView activeView;
    private Coroutine processCo;
    private string lastSignature;
    private float lastEnqueueRealtime;
    private bool loggedMissingReferences;
    private bool isReady;
    private bool isInitializing;

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }

        Ins = this;
        EnsureReady();
    }

    private void OnValidate()
    {
        if (viewContainer == null)
            viewContainer = transform as RectTransform;

        BuildVisualMap();
    }

    private void OnDestroy()
    {
        if (Ins == this)
            Ins = null;
    }

    public static void Notify(TopNotificationType type, string message, float duration = 0f)
    {
        if (Ins == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[TopNotificationManager] Missing manager in scene.");
#endif
            return;
        }

        Ins.Enqueue(new TopNotificationRequest(type, message, duration));
    }

    public static void NotifyFriend(string message, float duration = 0f)
    {
        Notify(TopNotificationType.Friend, message, duration);
    }

    public static void NotifyQuest(string message, float duration = 0f)
    {
        Notify(TopNotificationType.Quest, message, duration);
    }

    public static void NotifyAchievement(string message, float duration = 0f)
    {
        Notify(TopNotificationType.Achievement, message, duration);
    }

    public void Enqueue(TopNotificationRequest request)
    {
        EnsureReady();

        if (string.IsNullOrWhiteSpace(request.message))
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[QuestDebug] TopNotification enqueue type={request.type} message={request.message}");
#endif
        string signature = $"{(int)request.type}:{request.message}";
        float now = Time.unscaledTime;
        if (signature == lastSignature && now - lastEnqueueRealtime <= duplicateSuppressWindow)
            return;

        lastSignature = signature;
        lastEnqueueRealtime = now;

        queue.Enqueue(request);
        if (processCo == null)
            processCo = StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        while (queue.Count > 0)
        {
            if (activeView == null && !CreateView())
            {
                LogMissingReferences();
                queue.Clear();
                break;
            }

            var request = queue.Dequeue();
            var visual = ResolveVisual(request.type);
            float duration = request.duration > 0f ? request.duration : defaultDuration;

            yield return activeView.Play(request.message, duration, visual);
        }

        processCo = null;
    }

    private TopNotificationVisualProfile ResolveVisual(TopNotificationType type)
    {
        if (visualMap.TryGetValue(type, out var profile))
            return profile;

        return defaultVisual;
    }

    private bool CreateView()
    {
        if (activeView != null)
            return true;

        if (viewPrefab == null || viewContainer == null)
            return false;

        activeView = Instantiate(viewPrefab, viewContainer, false);
        activeView.name = viewPrefab.name;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[QuestDebug] TopNotification view instantiated: {activeView.name}", this);
#endif
        return true;
    }

    private void BuildVisualMap()
    {
        visualMap.Clear();
        for (int i = 0; i < visuals.Count; i++)
        {
            var profile = visuals[i];
            visualMap[profile.type] = profile;
        }
    }

    private void EnsureReady()
    {
        if (isReady || isInitializing)
            return;

        isInitializing = true;
        try
        {
            if (viewContainer == null)
                viewContainer = transform as RectTransform;

            BuildVisualMap();
            CreateView();
            isReady = true;
        }
        finally
        {
            isInitializing = false;
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private void LogMissingReferences()
    {
        if (loggedMissingReferences)
            return;

        loggedMissingReferences = true;
        Debug.LogWarning("[TopNotificationManager] Missing viewPrefab or viewContainer reference.", this);
    }
}
