using System;
using System.Collections.Generic;
using UnityEngine;

public enum AnalyticsFlushMode
{
    PeriodicAndLifecycle,
    LifecycleOnly
}

public class AnalyticsManager : MonoBehaviour
{
    public static AnalyticsManager Ins { get; private set; }

    [Header("Click Aggregation")]
    [SerializeField] private AnalyticsFlushMode clickFlushMode = AnalyticsFlushMode.PeriodicAndLifecycle;
    [SerializeField, Min(10f)] private float clickFlushInterval = 300f;

    private readonly Dictionary<ClickKey, ClickAggregate> clickAgg = new Dictionary<ClickKey, ClickAggregate>(64);
    private float lastFlushTime;

    private string sessionId;
    private float sessionStartTime;
    private bool sessionActive;

    public static void EnsureExists()
    {
        if (Ins != null) return;
        var go = new GameObject("AnalyticsManager");
        Ins = go.AddComponent<AnalyticsManager>();
    }

    void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }
        Ins = this;
        DontDestroyOnLoad(gameObject);

        StartSession("app_start");
    }

    void Update()
    {
        if (clickFlushMode == AnalyticsFlushMode.LifecycleOnly)
            return;

        if (Time.unscaledTime - lastFlushTime >= clickFlushInterval)
        {
            FlushClicks();
            lastFlushTime = Time.unscaledTime;
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            EndSession("pause");
        else
            StartSession("resume");
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            FlushClicks();
    }

    void OnApplicationQuit()
    {
        EndSession("quit");
    }

    void StartSession(string source)
    {
        sessionId = $"s_{Guid.NewGuid():N}";
        sessionStartTime = Time.unscaledTime;
        sessionActive = true;

        AnalyticsService.LogEvent(
            "session_start",
            new AnalyticsService.AnalyticsParam("session_id", sessionId),
            new AnalyticsService.AnalyticsParam("source", source)
        );
    }

    void EndSession(string reason)
    {
        if (!sessionActive) return;
        sessionActive = false;

        FlushClicks();

        int duration = Mathf.Max(0, Mathf.RoundToInt(Time.unscaledTime - sessionStartTime));
        AnalyticsService.LogEvent(
            "session_end",
            new AnalyticsService.AnalyticsParam("session_id", sessionId),
            new AnalyticsService.AnalyticsParam("duration_sec", duration),
            new AnalyticsService.AnalyticsParam("reason", reason)
        );
    }

    public void TrackBlockClick(string blockId, string location, float damage, string source)
    {
        blockId = string.IsNullOrEmpty(blockId) ? "unknown" : blockId;
        location = string.IsNullOrEmpty(location) ? "unknown" : location;
        source = string.IsNullOrEmpty(source) ? "unknown" : source;

        var key = new ClickKey(blockId, location, source);
        if (!clickAgg.TryGetValue(key, out var agg))
            agg = new ClickAggregate { blockId = blockId, location = location, source = source };

        agg.count += 1;
        agg.damageSum += damage;
        clickAgg[key] = agg;
    }

    public void TrackBlockBreak(string blockId, string location, float timeToBreak)
    {
        AnalyticsService.LogEvent(
            "block_break",
            new AnalyticsService.AnalyticsParam("block_id", blockId),
            new AnalyticsService.AnalyticsParam("location", location),
            new AnalyticsService.AnalyticsParam("time_to_break_sec", timeToBreak)
        );
    }

    public void TrackMonsterSpawn(string monsterId, string location, string timeState, string normalWeather, string specialWeather)
    {
        AnalyticsService.LogEvent(
            "monster_spawn",
            new AnalyticsService.AnalyticsParam("monster_id", monsterId),
            new AnalyticsService.AnalyticsParam("location", location),
            new AnalyticsService.AnalyticsParam("time_state", timeState),
            new AnalyticsService.AnalyticsParam("normal_weather", normalWeather),
            new AnalyticsService.AnalyticsParam("special_weather", specialWeather)
        );
    }

    public void TrackMonsterKill(string monsterId, float timeAliveSec, string rewardId)
    {
        AnalyticsService.LogEvent(
            "monster_kill",
            new AnalyticsService.AnalyticsParam("monster_id", monsterId),
            new AnalyticsService.AnalyticsParam("time_alive_sec", timeAliveSec),
            new AnalyticsService.AnalyticsParam("reward_id", rewardId)
        );
    }

    public void TrackMonsterMiss(string monsterId, float timeAliveSec)
    {
        AnalyticsService.LogEvent(
            "monster_miss",
            new AnalyticsService.AnalyticsParam("monster_id", monsterId),
            new AnalyticsService.AnalyticsParam("time_alive_sec", timeAliveSec)
        );
    }

    public void TrackBossSpawn(string bossId, string location)
    {
        AnalyticsService.LogEvent(
            "boss_spawn",
            new AnalyticsService.AnalyticsParam("boss_id", bossId),
            new AnalyticsService.AnalyticsParam("location", location)
        );
    }

    public void TrackBossKill(string bossId, float timeToKillSec)
    {
        AnalyticsService.LogEvent(
            "boss_kill",
            new AnalyticsService.AnalyticsParam("boss_id", bossId),
            new AnalyticsService.AnalyticsParam("time_to_kill_sec", timeToKillSec)
        );
    }

    public void TrackLocationChange(string fromLocation, string toLocation)
    {
        AnalyticsService.LogEvent(
            "location_change",
            new AnalyticsService.AnalyticsParam("from_location", fromLocation),
            new AnalyticsService.AnalyticsParam("to_location", toLocation)
        );
    }

    public void TrackCraftComplete(string recipeId, int qty)
    {
        AnalyticsService.LogEvent(
            "craft_complete",
            new AnalyticsService.AnalyticsParam("recipe_id", recipeId),
            new AnalyticsService.AnalyticsParam("qty", qty)
        );
    }

    public void TrackCurrencyEarn(string currency, int amount, string source)
    {
        AnalyticsService.LogEvent(
            "currency_earn",
            new AnalyticsService.AnalyticsParam("currency", currency),
            new AnalyticsService.AnalyticsParam("amount", amount),
            new AnalyticsService.AnalyticsParam("source", source)
        );
    }

    public void UpdateUserProperties(DataSaver saver)
    {
        if (saver == null) return;

        AnalyticsService.SetUserProperty("platform", Application.platform.ToString());
        AnalyticsService.SetUserProperty("app_version", Application.version);
        AnalyticsService.SetUserProperty("current_location", saver.currentLocation?.ToString() ?? "unknown");
        AnalyticsService.SetUserProperty("peak_location", saver.PeakLocation?.ToString() ?? "unknown");
        AnalyticsService.SetUserProperty("current_block", saver.currentBlock ?? "unknown");
        AnalyticsService.SetUserProperty("progression_tier", GetProgressionTier(saver.PeakLocation ?? saver.currentLocation));
        AnalyticsService.SetUserProperty("total_playtime_min", Mathf.RoundToInt(saver.TotalPlaytime / 60f));
        AnalyticsService.SetUserProperty("payer_status", "non_payer");
        AnalyticsService.SetUserProperty("ad_engaged", false);
    }

    string GetProgressionTier(BlockSpawnLocation? location)
    {
        if (!location.HasValue) return "unknown";
        return location.Value switch
        {
            BlockSpawnLocation.Plain or BlockSpawnLocation.Ice => "early",
            BlockSpawnLocation.Underground or BlockSpawnLocation.SkyIsland or BlockSpawnLocation.Desert => "mid",
            _ => "late"
        };
    }

    void FlushClicks()
    {
        if (clickAgg.Count == 0) return;

        foreach (var kv in clickAgg)
        {
            var agg = kv.Value;
            AnalyticsService.LogEvent(
                "click_block",
                new AnalyticsService.AnalyticsParam("block_id", agg.blockId),
                new AnalyticsService.AnalyticsParam("location", agg.location),
                new AnalyticsService.AnalyticsParam("source", agg.source),
                new AnalyticsService.AnalyticsParam("click_count", agg.count),
                new AnalyticsService.AnalyticsParam("damage_sum", agg.damageSum)
            );
        }

        clickAgg.Clear();
    }

    struct ClickAggregate
    {
        public string blockId;
        public string location;
        public string source;
        public int count;
        public float damageSum;
    }

    private struct ClickKey : IEquatable<ClickKey>
    {
        private readonly string blockId;
        private readonly string location;
        private readonly string source;

        public ClickKey(string blockId, string location, string source)
        {
            this.blockId = blockId;
            this.location = location;
            this.source = source;
        }

        public bool Equals(ClickKey other)
        {
            return string.Equals(blockId, other.blockId, StringComparison.Ordinal) &&
                   string.Equals(location, other.location, StringComparison.Ordinal) &&
                   string.Equals(source, other.source, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ClickKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + (blockId != null ? blockId.GetHashCode() : 0);
                hash = (hash * 31) + (location != null ? location.GetHashCode() : 0);
                hash = (hash * 31) + (source != null ? source.GetHashCode() : 0);
                return hash;
            }
        }
    }
}
