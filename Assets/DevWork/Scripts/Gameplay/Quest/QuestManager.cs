using System;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Ins { get; private set; }

    [Header("Database")]
    public QuestSO questDB;

    [Header("Storage")]
    [SerializeField] private bool usePlayerPrefsStorage = true;
    private IQuestStorage storage;

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
        Initialize();
    }

    public void Initialize()
    {
        _cd?.Dispose();
        _cd = new CompositeDisposable();

        // 1) LOAD STATE
        var progressStates = storage.LoadProgressStates();
        var dailyStates    = storage.LoadDailyStates();
        todayKey = GetBangkokDateKey(DateTime.UtcNow);

        // Daily rotate
        var savedKey = storage.LoadDailyKey();
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
            foreach (var id in storage.LoadDailySelectedIds()) dailySelectedIds.Add(id);
            // ensure states exist even if DB changed
            foreach (var q in questDB.dailyQuests.Where(d => dailySelectedIds.Contains(d.id)))
                if (!dailyStates.Any(s => s.questId == q.id))
                    dailyStates.Add(NewQuestStateFromDef(q));
            storage.SaveDailyStates(dailyStates);
        }

        // 2) BUILD TRACKERS (UniRx)
        BuildTrackers(progressTrackers, questDB.progressQuests, progressStates);
        BuildTrackers(dailyTrackers,    questDB.dailyQuests.Where(q=>dailySelectedIds.Contains(q.id)).ToList(), dailyStates);

        _onListChanged.OnNext(Unit.Default);
    }

    private void BuildTrackers(Dictionary<string, QuestTracker> dict, List<QuestDef> defs, List<QuestState> states)
    {
        dict.Clear();
        foreach (var def in defs)
        {
            var saved = states.FirstOrDefault(s => s.questId == def.id) ?? NewQuestStateFromDef(def);
            var tracker = new QuestTracker(def, saved);
            dict[def.id] = tracker;

            // Auto-save khi có tiến độ đổi (throttle nhẹ để giảm I/O)
            tracker.OnStepProgressChanged += t =>
            {
                SaveDict(dict, defs, def.type);
                _onQuestUpdated.OnNext(t.QuestId);
            };

            // Auto-complete → save
            tracker.Completed
                .DistinctUntilChanged()
                .Where(done => done)
                .Subscribe(_ =>
                {
                    SaveDict(dict, defs, def.type);
                    _onQuestUpdated.OnNext(def.id);
                })
                .AddTo(_cd);
        }
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

        if (type == QuestType.Progress) storage.SaveProgressStates(states);
        else storage.SaveDailyStates(states);
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
