using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Firebase.Firestore;
using UniRx;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Ins { get; private set; }
    public bool IsReady { get; private set; }

    [Header("Database")]
    public QuestSO questDB;

    [Header("Storage")]
    [SerializeField] private bool usePlayerPrefsStorage = true;
    private IQuestStorage storage;

    [Header("Cloud Sync")]
    [SerializeField] private bool useCloudStorage = true;
    [SerializeField] private float cloudInitWaitSeconds = 5f;
    [SerializeField] private float cloudLoadTimeoutSeconds = 8f;

    [Header("Save Throttle")]
    [SerializeField] private float saveThrottleSeconds = 0.5f;
    private readonly Subject<QuestType> _saveRequests = new();

    private FirebaseFirestore db;
    private string uid;
    private bool cloudReady;
    private bool cloudInitInProgress;
    private CompositeDisposable _progressTrackersCd = new();
    private CompositeDisposable _dailyTrackersCd = new();

    [Header("Daily Reset")]
    [SerializeField] private float dailyCheckIntervalSeconds = 60f;
    private Coroutine dailyWatchRoutine;

    // Runtime trackers
    private readonly Dictionary<string, QuestTracker> progressTrackers = new();
    private readonly Dictionary<string, QuestTracker> dailyTrackers = new();
    private readonly HashSet<string> dailySelectedIds = new();

    private string todayKey;
    private CompositeDisposable _cd = new();

    // UI events
    public IObservable<Unit> OnAnyQuestListChanged => _onListChanged; 
    private readonly Subject<Unit> _onListChanged = new();
    public IObservable<string> OnQuestUpdated => _onQuestUpdated;
    private readonly Subject<string> _onQuestUpdated = new();

    void Awake()
    {
        if (Ins != null && Ins != this) { Destroy(gameObject); return; }
        Ins = this;
        DontDestroyOnLoad(gameObject);
        storage = usePlayerPrefsStorage ? new PlayerPrefsQuestStorage() : null;
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

        storage = usePlayerPrefsStorage ? new PlayerPrefsQuestStorage() : new PlayerPrefsQuestStorage();

        // 1) LOAD STATE
        var progressStates = new List<QuestState>();
        var dailyStates = new List<QuestState>();
        var savedDailyKey = string.Empty;
        var savedDailyIds = new List<string>();
        bool loadedFromCloud = false;

        if (useCloudStorage)
        {
            yield return TryLoadFromCloud((ok, p, d, key, ids) =>
            {
                loadedFromCloud = ok;
                if (p != null) progressStates = p;
                if (d != null) dailyStates = d;
                if (!string.IsNullOrEmpty(key)) savedDailyKey = key;
                if (ids != null) savedDailyIds = ids;
            });
        }

        if (!loadedFromCloud)
        {
            progressStates = storage.LoadProgressStates();
            dailyStates = storage.LoadDailyStates();
            savedDailyKey = storage.LoadDailyKey();
            savedDailyIds = storage.LoadDailySelectedIds();
        }
        else
        {
            storage.SaveProgressStates(progressStates);
            storage.SaveDailyStates(dailyStates);
            storage.SaveDailyKey(savedDailyKey ?? "");
            storage.SaveDailySelectedIds(savedDailyIds ?? new List<string>());
        }

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
        _progressTrackersCd = BuildTrackers(progressTrackers, questDB.progressQuests, progressStates, _progressTrackersCd);
        _dailyTrackersCd = BuildTrackers(dailyTrackers, GetActiveDailyDefs(), dailyStates, _dailyTrackersCd);

        BindSaveThrottle();
        RefreshUnlockStates();
        _onQuestUpdated.Subscribe(_ => RefreshUnlockStates()).AddTo(_cd);

        _onListChanged.OnNext(Unit.Default);

        if (cloudReady)
            SaveCloudSnapshot();

        StartDailyWatch();
        IsReady = true;
    }

    private CompositeDisposable BuildTrackers(
        Dictionary<string, QuestTracker> dict,
        List<QuestDef> defs,
        List<QuestState> states,
        CompositeDisposable trackerCd)
    {
        trackerCd?.Dispose();
        trackerCd = new CompositeDisposable();

        foreach (var tr in dict.Values)
            tr.Dispose();
        dict.Clear();
        foreach (var def in defs)
        {
            var saved = states.FirstOrDefault(s => s.questId == def.id) ?? NewQuestStateFromDef(def);
            var tracker = new QuestTracker(def, saved);
            dict[def.id] = tracker;

            // Auto-save khi có tiến độ đổi (throttle nhẹ để giảm I/O)
            tracker.OnStepProgressChanged += t =>
            {
                RequestSave(def.type);
                _onQuestUpdated.OnNext(t.QuestId);
            };

            // Auto-complete → save
            tracker.Completed
                .DistinctUntilChanged()
                .Where(done => done)
                .Subscribe(_ =>
                {
                    RequestSave(def.type);
                    _onQuestUpdated.OnNext(def.id);
                })
                .AddTo(trackerCd);
        }

        return trackerCd;
    }

    // ---- Public getters cho UI ----
    public IEnumerable<(QuestDef def, QuestTracker tr)> GetAllProgress()
        => questDB.progressQuests.Select(def => (def, progressTrackers[def.id]));

    public IEnumerable<(QuestDef def, QuestTracker tr)> GetActiveDaily()
        => questDB.dailyQuests
            .Where(def => dailySelectedIds.Contains(def.id))
            .Select(def => (def, dailyTrackers[def.id]));

    public void ClaimReward(string questId)
    {
        if (progressTrackers.TryGetValue(questId, out var p))
        {
            var def = questDB.progressQuests.FirstOrDefault(q => q.id == questId);
            if (def == null) { Debug.LogWarning($"QuestDef not found: {questId}"); return; }
            if (!IsQuestUnlocked(def)) { Debug.LogWarning($"Quest locked: {questId}"); return; }

            if (p.Completed.Value && !p.RewardClaimed.Value)
            {
                GrantRewards(def);
                p.RewardClaimed.Value = true;
                SaveDict(progressTrackers, questDB.progressQuests, QuestType.Progress);
                _onQuestUpdated.OnNext(questId);
            }
            return;
        }
        if (dailyTrackers.TryGetValue(questId, out var d))
        {
            var def = questDB.dailyQuests.FirstOrDefault(q => q.id == questId);
            if (def == null) { Debug.LogWarning($"Daily QuestDef not found: {questId}"); return; }
            if (!IsQuestUnlocked(def)) { Debug.LogWarning($"Daily quest locked: {questId}"); return; }

            if (d.Completed.Value && !d.RewardClaimed.Value)
            {
                GrantRewards(def);
                d.RewardClaimed.Value = true;
                SaveDict(dailyTrackers,
                questDB.dailyQuests.Where(q => dailySelectedIds.Contains(q.id)).ToList(),
                    QuestType.Daily);
                _onQuestUpdated.OnNext(questId);
            }
        }
    }

    // ---- Save/Load helpers ----
    private void SaveDict(Dictionary<string, QuestTracker> dict, List<QuestDef> defs, QuestType type)
    {
        var states = BuildStates(dict, defs);

        if (type == QuestType.Progress) storage.SaveProgressStates(states);
        else storage.SaveDailyStates(states);
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
        if (type == QuestType.Progress)
            SaveDict(progressTrackers, questDB.progressQuests, QuestType.Progress);
        else
            SaveDict(dailyTrackers, GetActiveDailyDefs(), QuestType.Daily);

        SaveCloudSnapshot();
    }

    private List<QuestDef> GetActiveDailyDefs()
    {
        return questDB.dailyQuests.Where(q => dailySelectedIds.Contains(q.id)).ToList();
    }

    private List<QuestState> BuildStates(Dictionary<string, QuestTracker> dict, List<QuestDef> defs)
    {
        var states = new List<QuestState>();

        foreach (var def in defs)
        {
            if (!dict.TryGetValue(def.id, out var tr)) continue;

            var st = new QuestState
            {
                questId = def.id,
                completed = tr.Completed.Value,
                rewardClaimed = tr.RewardClaimed.Value,
                steps = def.steps.Select(sdef =>
                {
                    var t = tr.Steps.First(x => x.StepId == sdef.stepId);
                    return new QuestStepState
                    {
                        stepId = sdef.stepId,
                        currentAmount = t.Current.Value,
                        completed = t.Completed.Value
                    };
                }).ToList()
            };

            states.Add(st);
        }

        return states;
    }

    private void RefreshUnlockStates()
    {
        foreach (var def in questDB.progressQuests)
        {
            if (progressTrackers.TryGetValue(def.id, out var tr))
                tr.SetUnlocked(IsQuestUnlocked(def));
        }

        foreach (var def in questDB.dailyQuests)
        {
            if (dailyTrackers.TryGetValue(def.id, out var tr))
                tr.SetUnlocked(IsQuestUnlocked(def));
        }
    }

    private bool IsQuestUnlocked(QuestDef def)
    {
        if (def != null && def.type == QuestType.Daily)
            return true;
        if (def == null || string.IsNullOrEmpty(def.requiredQuestId))
            return true;
        return IsQuestCompleted(def.requiredQuestId);
    }

    private bool IsQuestCompleted(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return true;
        if (progressTrackers.TryGetValue(questId, out var p))
            return p.Completed.Value;
        if (dailyTrackers.TryGetValue(questId, out var d))
            return d.Completed.Value;
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

        _dailyTrackersCd = BuildTrackers(dailyTrackers, GetActiveDailyDefs(), dailyStates, _dailyTrackersCd);
        RefreshUnlockStates();
        _onListChanged.OnNext(Unit.Default);
        RequestSave(QuestType.Daily);
    }

    private IEnumerator TryInitCloud()
    {
        cloudReady = false;
        db = null;
        uid = null;

        if (!useCloudStorage)
            yield break;

        float t = 0f;
        while (FirebaseBootstrap.Ins == null && t < cloudInitWaitSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (FirebaseBootstrap.Ins == null)
            yield break;

        t = 0f;
        while (!FirebaseBootstrap.Ins.IsReady && !FirebaseBootstrap.Ins.IsFailed && t < cloudInitWaitSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!FirebaseBootstrap.Ins.IsReady)
            yield break;

        db = FirebaseBootstrap.Ins.Db;
        uid = FirebaseBootstrap.Ins.Uid;
        cloudReady = db != null && !string.IsNullOrEmpty(uid);
    }

    private IEnumerator TryLoadFromCloud(Action<bool, List<QuestState>, List<QuestState>, string, List<string>> onDone)
    {
        yield return TryInitCloud();
        if (!cloudReady)
        {
            onDone?.Invoke(false, null, null, null, null);
            yield break;
        }

        var docRef = db.Collection("users").Document(uid).Collection("quests").Document("state");
        var task = FirebaseTaskTracker.Track(docRef.GetSnapshotAsync());

        float t = 0f;
        while (!task.IsCompleted && t < cloudLoadTimeoutSeconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!task.IsCompleted || task.Exception != null || task.Result == null || !task.Result.Exists)
        {
            onDone?.Invoke(false, null, null, null, null);
            yield break;
        }

        var snap = task.Result;
        snap.TryGetValue("progressJson", out string progressJson);
        snap.TryGetValue("dailyJson", out string dailyJson);
        snap.TryGetValue("dailyKey", out string dailyKey);
        snap.TryGetValue("dailyIdsJson", out string dailyIdsJson);

        var progressStates = ParseStateList(progressJson);
        var dailyStates = ParseStateList(dailyJson);
        var dailyIds = ParseStringList(dailyIdsJson);

        onDone?.Invoke(true, progressStates, dailyStates, dailyKey, dailyIds);
    }

    private void SaveCloudSnapshot()
    {
        if (!useCloudStorage) return;
        if (!cloudReady)
        {
            if (!cloudInitInProgress)
                StartCoroutine(CoInitCloudAndSave());
            return;
        }

        SaveCloudSnapshotInternal();
    }

    private void SaveCloudSnapshotInternal()
    {
        if (!cloudReady) return;

        var progressStates = BuildStates(progressTrackers, questDB.progressQuests);
        var dailyStates = BuildStates(dailyTrackers, GetActiveDailyDefs());

        var payload = new Dictionary<string, object>
        {
            ["progressJson"] = ToJsonStateList(progressStates),
            ["dailyJson"] = ToJsonStateList(dailyStates),
            ["dailyKey"] = todayKey,
            ["dailyIdsJson"] = ToJsonStringList(dailySelectedIds.ToList()),
            ["updatedAt"] = Timestamp.GetCurrentTimestamp()
        };

        var docRef = db.Collection("users").Document(uid).Collection("quests").Document("state");
        FirebaseTaskTracker.Track(docRef.SetAsync(payload, SetOptions.MergeAll));
    }

    private IEnumerator CoInitCloudAndSave()
    {
        cloudInitInProgress = true;
        yield return TryInitCloud();
        cloudInitInProgress = false;
        if (cloudReady)
            SaveCloudSnapshotInternal();
    }

    [Serializable]
    private class QuestStateListWrapper
    {
        public List<QuestState> states = new List<QuestState>();
    }

    [Serializable]
    private class StringListWrapper
    {
        public List<string> items = new List<string>();
    }

    private string ToJsonStateList(List<QuestState> states)
    {
        var wrapper = new QuestStateListWrapper { states = states ?? new List<QuestState>() };
        return JsonUtility.ToJson(wrapper, false);
    }

    private List<QuestState> ParseStateList(string json)
    {
        if (string.IsNullOrEmpty(json)) return new List<QuestState>();
        var wrapper = JsonUtility.FromJson<QuestStateListWrapper>(json);
        return wrapper?.states ?? new List<QuestState>();
    }

    private string ToJsonStringList(List<string> items)
    {
        var wrapper = new StringListWrapper { items = items ?? new List<string>() };
        return JsonUtility.ToJson(wrapper, false);
    }

    private List<string> ParseStringList(string json)
    {
        if (string.IsNullOrEmpty(json)) return new List<string>();
        var wrapper = JsonUtility.FromJson<StringListWrapper>(json);
        return wrapper?.items ?? new List<string>();
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

    private void GrantRewards(QuestDef def)
    {
        if (def.rewards == null || def.rewards.Count == 0)
            return;

        foreach (var reward in def.rewards)
        {
            // --- 1) Give Gems ---
            if (reward.gemAmount > 0)
            {
                StatsManager.Ins.Add(StatType.Diamond, reward.gemAmount);
                Debug.Log($"[QuestManager] +{reward.gemAmount} Gems");
                AnalyticsManager.Ins?.TrackCurrencyEarn("gems", reward.gemAmount, $"quest:{def.id}");
            }

            // --- 2) Give Inventory Items ---
            if (reward.items != null)
            {
                foreach (var invItem in reward.items)
                {
                    if (invItem == null || invItem.itemData == null || invItem.quantity.Value <= 0)
                        continue;

                    InventoryController.Instance.AddItemToInventory(invItem);

                    Debug.Log($"[QuestManager] +{invItem.quantity.Value}x {invItem.itemData.name}");

                    GameDebugHandler.LogStatic($"[Quest] +{invItem.quantity.Value}x {invItem.itemData.name}");
                }
            }
        }
    }

}
