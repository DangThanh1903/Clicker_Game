using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UniRx;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    // Legacy global path: keep existing callers for now, but do not add new singleton dependencies here.
    public static QuestManager Ins { get; private set; }
    public bool IsReady { get; private set; }

    [Header("Database")]
    public QuestSO questDB;

    [Header("Local Storage")]
    private IQuestStorage storage;

    [Header("Save Throttle")]
    [SerializeField] private float saveThrottleSeconds = 0.5f;
    private readonly Subject<QuestType> _saveRequests = new();

    private readonly Dictionary<QuestType, QuestBucket> bucketsByType = new();
    private readonly List<QuestBucket> bucketOrder = new();

    [Header("Daily Reset")]
    [SerializeField] private float dailyCheckIntervalSeconds = 60f;
    private Coroutine dailyWatchRoutine;

    private readonly HashSet<string> dailySelectedIds = new();
    private bool loggedMissingQuestDb;

    private string todayKey;
    private CompositeDisposable _cd = new();

    public event Action<QuestType> QuestListChanged;
    public event Action<QuestRuntimeEntry> QuestChanged;
    public event Action<QuestRuntimeEntry> QuestCompleted;

    // UI events
    public IObservable<Unit> OnAnyQuestListChanged => _onListChanged; 
    private readonly Subject<Unit> _onListChanged = new();
    public IObservable<string> OnQuestUpdated => _onQuestUpdated;
    private readonly Subject<string> _onQuestUpdated = new();
    public IObservable<QuestDef> OnQuestCompleted => _onQuestCompleted;
    private readonly Subject<QuestDef> _onQuestCompleted = new();

    private sealed class QuestBucket
    {
        private readonly Func<List<QuestDef>> defsGetter;
        private readonly Func<List<QuestState>> statesLoader;
        private readonly Action<List<QuestState>> statesSaver;

        public QuestBucket(
            QuestType type,
            Func<List<QuestDef>> defsGetter,
            Func<List<QuestState>> statesLoader,
            Action<List<QuestState>> statesSaver)
        {
            Type = type;
            this.defsGetter = defsGetter;
            this.statesLoader = statesLoader;
            this.statesSaver = statesSaver;
        }

        public QuestType Type { get; }
        public readonly Dictionary<string, QuestTracker> Trackers = new();
        public CompositeDisposable TrackerSubscriptions { get; private set; } = new();

        public List<QuestDef> GetDefs()
        {
            return defsGetter != null ? defsGetter.Invoke() : new List<QuestDef>();
        }

        public List<QuestState> LoadStates()
        {
            return statesLoader != null ? statesLoader.Invoke() : new List<QuestState>();
        }

        public void SaveStates(List<QuestState> states)
        {
            statesSaver?.Invoke(states ?? new List<QuestState>());
        }

        public void ResetTrackers()
        {
            TrackerSubscriptions.Dispose();
            TrackerSubscriptions = new CompositeDisposable();

            foreach (var tracker in Trackers.Values)
                tracker.Dispose();

            Trackers.Clear();
        }
    }

    void Awake()
    {
        if (Ins != null && Ins != this) { Destroy(gameObject); return; }
        Ins = this;
        DontDestroyOnLoad(gameObject);
        storage = new JsonQuestStorage();
        EnsureBuckets();
    }

    void Start()
    {
        StartCoroutine(Initialize());
    }

    public IEnumerator Initialize()
    {
        IsReady = false;
        _cd?.Dispose();
        _cd = new CompositeDisposable();

        if (questDB == null)
        {
            if (!loggedMissingQuestDb)
            {
                loggedMissingQuestDb = true;
                Debug.LogError("[QuestDebug] QuestManager has no questDB assigned.", this);
            }

            yield break;
        }

        if (storage == null)
            storage = new JsonQuestStorage();

        EnsureBuckets();

        QuestBucket progressBucket = GetBucket(QuestType.Progress);
        QuestBucket achievementBucket = GetBucket(QuestType.Achievement);
        QuestBucket dailyBucket = GetBucket(QuestType.Daily);

        // 1) LOAD STATE
        var progressStates = progressBucket.LoadStates();
        var achievementStates = achievementBucket.LoadStates();
        var dailyStates = dailyBucket.LoadStates();
        var savedDailyKey = storage.LoadDailyKey();
        var savedDailyIds = storage.LoadDailySelectedIds();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[QuestDebug] Quest load source=local progressStates={progressStates.Count} achievementStates={achievementStates.Count} dailyStates={dailyStates.Count}");
#endif

        MoveLegacyAchievementStates(ref progressStates, ref achievementStates);
        progressBucket.SaveStates(progressStates);
        achievementBucket.SaveStates(achievementStates);

        todayKey = GetBangkokDateKey(DateTime.UtcNow);

        // Daily rotate
        var savedKey = savedDailyKey ?? "";
        dailySelectedIds.Clear();
        if (savedKey != todayKey)
        {
            var pick = PickDailyIds(questDB.dailyQuests, questDB.dailyPickCount);
            foreach (var id in pick) dailySelectedIds.Add(id);
            storage.SaveDailySelectedIds(pick);
            storage.SaveDailyKey(todayKey);

            dailyStates = questDB.dailyQuests
                .Where(q => dailySelectedIds.Contains(q.id))
                .Select(q => NewQuestStateFromDef(q))
                .ToList();
            storage.SaveDailyStates(dailyStates);
        }
        else
        {
            foreach (var id in savedDailyIds ?? new List<string>()) dailySelectedIds.Add(id);
            if (dailySelectedIds.Count == 0)
            {
                var pick = PickDailyIds(questDB.dailyQuests, questDB.dailyPickCount);
                foreach (var id in pick) dailySelectedIds.Add(id);
                storage.SaveDailySelectedIds(pick);
                storage.SaveDailyKey(todayKey);
            }
            // ensure states exist even if DB changed
            foreach (var q in questDB.dailyQuests.Where(d => dailySelectedIds.Contains(d.id)))
                if (!dailyStates.Any(s => s.questId == q.id))
                    dailyStates.Add(NewQuestStateFromDef(q));
            storage.SaveDailyStates(dailyStates);
        }

        // 2) BUILD TRACKERS (UniRx)
        BuildTrackers(progressBucket, progressStates);
        BuildTrackers(achievementBucket, achievementStates);
        BuildTrackers(dailyBucket, dailyStates);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[QuestDebug] Quest trackers ready progress={progressBucket.Trackers.Count} achievements={achievementBucket.Trackers.Count} daily={dailyBucket.Trackers.Count}");
#endif

        BindSaveThrottle();
        RefreshUnlockStates();
        SaveRuntimeQuestStatesToLocal();
        _onQuestUpdated.Subscribe(_ => RefreshUnlockStates()).AddTo(_cd);

        NotifyQuestListChanged(QuestType.Progress);
        NotifyQuestListChanged(QuestType.Achievement);
        NotifyQuestListChanged(QuestType.Daily);

        StartDailyWatch();
        IsReady = true;
    }

    private void BuildTrackers(QuestBucket bucket, List<QuestState> states)
    {
        bucket.ResetTrackers();
        List<QuestDef> defs = bucket.GetDefs();
        Dictionary<string, QuestState> stateMap = BuildStateMap(states);
        foreach (var def in defs)
        {
            if (def == null || string.IsNullOrEmpty(def.id))
                continue;

            stateMap.TryGetValue(def.id, out var saved);
            saved ??= NewQuestStateFromDef(def);

            var tracker = new QuestTracker(def, saved);
            bucket.Trackers[def.id] = tracker;
            var entry = new QuestRuntimeEntry(bucket.Type, def, tracker);
            bool completionPopupDispatched = tracker.Completed.Value;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string stepText = string.Join(", ", tracker.Steps.Select(s => $"{s.StepId}:{s.Current.Value}/{s.Required.Value}"));
            Debug.Log($"[QuestDebug] Tracker built type={bucket.Type} id={def.id} achievement={def.IsAchievement} completed={tracker.Completed.Value} claimed={tracker.RewardClaimed.Value} steps=[{stepText}]");
#endif

            // Auto-save when progress changes (light throttle to reduce I/O)
            tracker.OnStepProgressChanged += t =>
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[QuestDebug] Quest step changed type={bucket.Type} id={t.QuestId}");
#endif
                RequestSave(bucket.Type);
                NotifyQuestChanged(entry);
            };

            // Auto-complete -> save
            tracker.Completed
                .DistinctUntilChanged()
                .Where(done => done)
                .Subscribe(_ =>
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[QuestDebug] Quest completed signal type={bucket.Type} id={def.id} achievement={def.IsAchievement} alreadyDispatched={completionPopupDispatched}");
#endif
                    RequestSave(bucket.Type);
                    NotifyQuestChanged(entry);

                    if (!completionPopupDispatched)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[QuestDebug] Emitting OnQuestCompleted id={def.id}");
#endif
                        NotifyQuestCompleted(entry);
                    }

                    completionPopupDispatched = true;
                })
                .AddTo(bucket.TrackerSubscriptions);
        }
    }

    // ---- Public getters cho UI ----
    public IEnumerable<(QuestDef def, QuestTracker tr)> GetAllProgress()
        => GetQuests(QuestType.Progress);

    public IEnumerable<(QuestDef def, QuestTracker tr)> GetAchievements()
        => GetQuests(QuestType.Achievement);

    public IEnumerable<(QuestDef def, QuestTracker tr)> GetActiveDaily()
        => GetQuests(QuestType.Daily);

    public IEnumerable<(QuestDef def, QuestTracker tr)> GetQuests(QuestType type)
    {
        foreach (var entry in GetEntries(type))
        {
            if (entry.Def == null || entry.Tracker == null)
                continue;

            yield return (entry.Def, entry.Tracker);
        }
    }

    public IEnumerable<QuestRuntimeEntry> GetProgressEntries()
        => GetEntries(QuestType.Progress);

    public IEnumerable<QuestRuntimeEntry> GetAchievementEntries()
        => GetEntries(QuestType.Achievement);

    public IEnumerable<QuestRuntimeEntry> GetDailyEntries()
        => GetEntries(QuestType.Daily);

    public IEnumerable<QuestRuntimeEntry> GetEntries(QuestType type)
    {
        var dict = GetTrackerDict(type);
        foreach (var def in GetDefs(type))
        {
            if (def == null || string.IsNullOrEmpty(def.id))
                continue;
            if (!dict.TryGetValue(def.id, out var tracker))
                continue;

            yield return new QuestRuntimeEntry(type, def, tracker);
        }
    }

    public IEnumerable<QuestRuntimeView> GetViews(QuestType type)
    {
        foreach (var entry in GetEntries(type))
            yield return entry.ToView();
    }

    public void ClaimReward(string questId)
    {
        foreach (var bucket in bucketOrder)
        {
            if (TryClaimReward(questId, bucket))
                return;
        }

        Debug.LogWarning($"QuestDef not found: {questId}");
    }

    private bool TryClaimReward(string questId, QuestBucket bucket)
    {
        var dict = bucket.Trackers;
        if (!dict.TryGetValue(questId, out var tracker))
            return false;

        var def = bucket.GetDefs().FirstOrDefault(q => q.id == questId);
        if (def == null)
        {
            Debug.LogWarning($"{bucket.Type} QuestDef not found: {questId}");
            return true;
        }

        if (!IsQuestUnlocked(def))
        {
            Debug.LogWarning($"{bucket.Type} quest locked: {questId}");
            return true;
        }

        if (!tracker.Completed.Value || tracker.RewardClaimed.Value)
            return true;

        if (!TryGrantRewards(def))
        {
            Debug.LogWarning($"[QuestManager] Cannot claim {bucket.Type} reward for '{questId}' because inventory does not have enough space.");
            return true;
        }

        tracker.RewardClaimed.Value = true;
        SaveBucket(bucket);
        NotifyQuestChanged(new QuestRuntimeEntry(bucket.Type, def, tracker));
        return true;
    }

    // ---- Save/Load helpers ----
    private void SaveBucket(QuestBucket bucket)
    {
        if (bucket == null)
            return;

        bucket.SaveStates(BuildStates(bucket.Trackers, bucket.GetDefs()));
    }

    private void BindSaveThrottle()
    {
        _saveRequests
            .GroupBy(t => t)
            .SelectMany(g => g.Throttle(TimeSpan.FromSeconds(Mathf.Max(0.05f, saveThrottleSeconds))))
            .Subscribe(SaveByType)
            .AddTo(_cd);
    }

    private void RequestSave(QuestType type)
    {
        _saveRequests.OnNext(type);
    }

    private void SaveByType(QuestType type)
    {
        SaveBucket(GetBucket(type));
    }

    private void SaveRuntimeQuestStatesToLocal()
    {
        foreach (var bucket in bucketOrder)
            SaveBucket(bucket);
    }

    private Dictionary<string, QuestTracker> GetTrackerDict(QuestType type)
    {
        return GetBucket(type).Trackers;
    }

    private List<QuestDef> GetDefs(QuestType type)
    {
        return GetBucket(type).GetDefs();
    }

    private List<QuestDef> GetProgressDefs()
    {
        if (questDB == null || questDB.progressQuests == null)
            return new List<QuestDef>();

        return questDB.progressQuests
            .Where(def => def != null && !IsAchievementDef(def))
            .ToList();
    }

    private List<QuestDef> GetAchievementDefs()
    {
        var defs = new List<QuestDef>();

        if (questDB?.achievementQuests != null)
            AddUniqueQuestDefs(defs, questDB.achievementQuests);

        AddLegacyAchievementDefs(defs);

        return defs;
    }

    private void AddLegacyAchievementDefs(List<QuestDef> target)
    {
        if (questDB?.progressQuests == null)
            return;

        AddUniqueQuestDefs(target, questDB.progressQuests.Where(IsAchievementDef));
    }

    private List<QuestDef> GetActiveDailyDefs()
    {
        if (questDB == null || questDB.dailyQuests == null)
            return new List<QuestDef>();

        return questDB.dailyQuests
            .Where(q => q != null && dailySelectedIds.Contains(q.id))
            .ToList();
    }

    private static bool IsAchievementDef(QuestDef def)
    {
        return def != null && def.IsAchievement;
    }

    private static void AddUniqueQuestDefs(List<QuestDef> target, IEnumerable<QuestDef> source)
    {
        if (target == null || source == null)
            return;

        var knownIds = new HashSet<string>(target
            .Where(def => def != null && !string.IsNullOrEmpty(def.id))
            .Select(def => def.id));

        foreach (var def in source)
        {
            if (def == null || string.IsNullOrEmpty(def.id))
                continue;
            if (!knownIds.Add(def.id))
                continue;

            target.Add(def);
        }
    }

    private List<QuestState> BuildStates(Dictionary<string, QuestTracker> dict, List<QuestDef> defs)
    {
        var states = new List<QuestState>();

        foreach (var def in defs)
        {
            if (def == null || string.IsNullOrEmpty(def.id))
                continue;
            if (!dict.TryGetValue(def.id, out var tr))
                continue;

            Dictionary<string, StepTracker> stepMap = tr.Steps
                .Where(step => step != null && !string.IsNullOrEmpty(step.StepId))
                .GroupBy(step => step.StepId)
                .ToDictionary(group => group.Key, group => group.First());

            var st = new QuestState
            {
                questId = def.id,
                completed = tr.Completed.Value,
                rewardClaimed = tr.RewardClaimed.Value,
                steps = def.steps.Select(sdef =>
                {
                    stepMap.TryGetValue(sdef.stepId, out var stepTracker);
                    return new QuestStepState
                    {
                        stepId = sdef.stepId,
                        currentAmount = stepTracker != null ? stepTracker.Current.Value : 0,
                        completed = stepTracker != null && stepTracker.Completed.Value
                    };
                }).ToList()
            };

            states.Add(st);
        }

        return states;
    }

    private List<QuestState> MergeStates(List<QuestState> primary, List<QuestState> secondary)
    {
        var map = (primary ?? new List<QuestState>()).ToDictionary(s => s.questId);
        foreach (var s in secondary ?? new List<QuestState>())
        {
            if (!map.TryGetValue(s.questId, out var p))
            {
                map[s.questId] = s;
                continue;
            }

            p.completed = p.completed || s.completed;
            p.rewardClaimed = p.rewardClaimed || s.rewardClaimed;

            if (s.steps != null)
            {
                foreach (var step in s.steps)
                {
                    var pStep = p.steps?.FirstOrDefault(x => x.stepId == step.stepId);
                    if (pStep == null)
                    {
                        p.steps ??= new List<QuestStepState>();
                        p.steps.Add(new QuestStepState
                        {
                            stepId = step.stepId,
                            currentAmount = step.currentAmount,
                            completed = step.completed
                        });
                    }
                    else
                    {
                        pStep.currentAmount = Mathf.Max(pStep.currentAmount, step.currentAmount);
                        pStep.completed = pStep.completed || step.completed;
                    }
                }
            }
        }

        return map.Values.ToList();
    }

    private static Dictionary<string, QuestState> BuildStateMap(List<QuestState> states)
    {
        var result = new Dictionary<string, QuestState>(StringComparer.Ordinal);
        if (states == null)
            return result;

        foreach (var state in states)
        {
            if (state == null || string.IsNullOrEmpty(state.questId) || result.ContainsKey(state.questId))
                continue;

            result[state.questId] = state;
        }

        return result;
    }

    private void MoveLegacyAchievementStates(ref List<QuestState> progressStates, ref List<QuestState> achievementStates)
    {
        var achievementIds = new HashSet<string>(GetAchievementDefs()
            .Where(def => def != null && !string.IsNullOrEmpty(def.id))
            .Select(def => def.id));

        if (achievementIds.Count == 0 || progressStates == null || progressStates.Count == 0)
            return;

        var legacyStates = progressStates
            .Where(state => state != null && achievementIds.Contains(state.questId))
            .ToList();

        if (legacyStates.Count == 0)
            return;

        achievementStates = MergeStates(achievementStates, legacyStates);
        progressStates = progressStates
            .Where(state => state == null || !achievementIds.Contains(state.questId))
            .ToList();
    }

    private void RefreshUnlockStates()
    {
        foreach (var bucket in bucketOrder)
            RefreshUnlockStates(bucket.Type);
    }

    private void RefreshUnlockStates(QuestType type)
    {
        var dict = GetTrackerDict(type);
        foreach (var def in GetDefs(type))
        {
            if (def != null && dict.TryGetValue(def.id, out var tr))
                tr.SetUnlocked(IsQuestUnlocked(def, type));
        }
    }

    private bool IsQuestUnlocked(QuestDef def)
    {
        return IsQuestUnlocked(def, def != null && IsAchievementDef(def) ? QuestType.Achievement : def?.type ?? QuestType.Progress);
    }

    private bool IsQuestUnlocked(QuestDef def, QuestType type)
    {
        if (def != null && type == QuestType.Daily)
            return true;
        if (def == null || string.IsNullOrEmpty(def.requiredQuestId))
            return true;
        return IsQuestCompleted(def.requiredQuestId);
    }

    private bool IsQuestCompleted(string questId)
    {
        if (string.IsNullOrEmpty(questId))
            return true;

        foreach (var bucket in bucketOrder)
        {
            if (bucket.Trackers.TryGetValue(questId, out var tracker))
                return tracker.Completed.Value;
        }

        return false;
    }

    private void StartDailyWatch()
    {
        if (dailyWatchRoutine != null)
            StopCoroutine(dailyWatchRoutine);
        dailyWatchRoutine = StartCoroutine(CoDailyWatch());
    }

    private IEnumerator CoDailyWatch()
    {
        float waitSeconds = Mathf.Max(5f, dailyCheckIntervalSeconds);
        var wait = new WaitForSecondsRealtime(waitSeconds);
        while (true)
        {
            yield return wait;
            ResetDailyIfNeeded();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ResetDailyIfNeeded();
    }

    private void ResetDailyIfNeeded()
    {
        if (questDB == null) return;
        string newKey = GetBangkokDateKey(DateTime.UtcNow);
        if (newKey == todayKey) return;

        todayKey = newKey;
        dailySelectedIds.Clear();

        var pick = PickDailyIds(questDB.dailyQuests, questDB.dailyPickCount);
        foreach (var id in pick) dailySelectedIds.Add(id);
        storage.SaveDailySelectedIds(pick);
        storage.SaveDailyKey(todayKey);

        var dailyStates = questDB.dailyQuests
            .Where(q => dailySelectedIds.Contains(q.id))
            .Select(q => NewQuestStateFromDef(q))
            .ToList();
        storage.SaveDailyStates(dailyStates);

        BuildTrackers(GetBucket(QuestType.Daily), dailyStates);
        RefreshUnlockStates();
        NotifyQuestListChanged(QuestType.Daily);
        RequestSave(QuestType.Daily);
    }

    private void EnsureBuckets()
    {
        if (bucketsByType.Count > 0)
            return;

        RegisterBucket(new QuestBucket(
            QuestType.Progress,
            GetProgressDefs,
            () => storage.LoadProgressStates(),
            states => storage.SaveProgressStates(states)));

        RegisterBucket(new QuestBucket(
            QuestType.Achievement,
            GetAchievementDefs,
            () => storage.LoadAchievementStates(),
            states => storage.SaveAchievementStates(states)));

        RegisterBucket(new QuestBucket(
            QuestType.Daily,
            GetActiveDailyDefs,
            () => storage.LoadDailyStates(),
            states => storage.SaveDailyStates(states)));
    }

    private void RegisterBucket(QuestBucket bucket)
    {
        if (bucket == null)
            return;

        bucketsByType[bucket.Type] = bucket;
        bucketOrder.Add(bucket);
    }

    private QuestBucket GetBucket(QuestType type)
    {
        EnsureBuckets();
        return bucketsByType[type];
    }

    private QuestState NewQuestStateFromDef(QuestDef def) => new QuestState
    {
        questId = def.id,
        completed = false,
        rewardClaimed = false,
        steps = def.steps.Select(s => new QuestStepState
        {
            stepId = s.stepId,
            currentAmount = 0,
            completed = false
        }).ToList()
    };

    private List<string> PickDailyIds(List<QuestDef> pool, int count)
    {
        var ids = pool.Select(q => q.id).ToList();
        for (int i=0;i<ids.Count;i++) { int j = UnityEngine.Random.Range(i, ids.Count); (ids[i], ids[j]) = (ids[j], ids[i]); }
        return ids.Take(Mathf.Clamp(count, 0, ids.Count)).ToList();
    }

    private string GetBangkokDateKey(DateTime utcNow)
    {
        DateTime local;
        try { local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")); }
        catch { local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Bangkok")); }
        return local.ToString("yyyyMMdd");
    }

    private bool TryGrantRewards(QuestDef def)
    {
        if (def == null)
            return false;

        if (def.rewards == null || def.rewards.Count == 0)
            return true;

        var inventory = InventoryController.Instance;
        var itemGrants = new List<(InventoryItem item, int requestedQty)>();

        foreach (var reward in def.rewards)
        {
            if (reward == null || reward.items == null)
                continue;

            foreach (var invItem in reward.items)
            {
                if (invItem == null || invItem.itemData == null || invItem.itemData.Type == ItemType.None || invItem.quantity == null)
                    continue;

                int qty = Mathf.Max(0, invItem.quantity.Value);
                if (qty <= 0)
                    continue;

                var runtimeItem = new InventoryItem(invItem.itemData, qty)
                {
                    prefix = invItem.prefix
                };
                itemGrants.Add((runtimeItem, qty));
            }
        }

        if (itemGrants.Count > 0)
        {
            if (inventory == null)
                return false;

            if (!inventory.CanFullyAddItems(itemGrants.Select(x => x.item)))
                return false;
        }

        foreach (var reward in def.rewards)
        {
            if (reward == null)
                continue;

            // --- 1) Give Gems ---
            if (reward.gemAmount > 0)
            {
                StatsManager.Ins.Add(StatType.Diamond, reward.gemAmount);
                DevLog.Log($"[QuestManager] +{reward.gemAmount} Gems");
                AnalyticsManager.Ins?.TrackCurrencyEarn("gems", reward.gemAmount, $"quest:{def.id}");
            }
        }

        // --- 2) Give Inventory Items ---
        for (int i = 0; i < itemGrants.Count; i++)
        {
            var grant = itemGrants[i];
            if (grant.item == null || grant.item.itemData == null)
                continue;

            bool added = inventory.TryAddItemToInventory(grant.item, requireFullAdd: true);
            if (!added)
            {
                Debug.LogWarning($"[QuestManager] Failed to grant item '{grant.item.itemData.name}' x{grant.requestedQty} for quest '{def.id}'.");
                return false;
            }

            DevLog.Log($"[QuestManager] +{grant.requestedQty}x {grant.item.itemData.name}");
            GameDebugHandler.LogStatic($"[Quest] +{grant.requestedQty}x {grant.item.itemData.name}");
        }

        return true;
    }

    private void NotifyQuestListChanged(QuestType type)
    {
        QuestListChanged?.Invoke(type);
        _onListChanged.OnNext(Unit.Default);
    }

    private void NotifyQuestChanged(QuestRuntimeEntry entry)
    {
        _onQuestUpdated.OnNext(entry.Id);
        QuestChanged?.Invoke(entry);
    }

    private void NotifyQuestCompleted(QuestRuntimeEntry entry)
    {
        QuestCompleted?.Invoke(entry);
        _onQuestCompleted.OnNext(entry.Def);
    }

}

