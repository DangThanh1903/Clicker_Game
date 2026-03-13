using UnityEngine.UI;
using UniRx;
using UnityEngine;
using System.Collections.Generic;
using System;

public class Boss : MonoBehaviour, IDamagable
{
    [SerializeField] EnemyStatsManager enemyStatsManager;
    [SerializeField] Image HpUI;
    private float accumulatedHoldTime = 0f;
    private readonly float timeHoldReset = 0.1f;
    private readonly float timeIdleReset = 1f;
    public event Action<Boss> Died;
    public int InputPriority => 2;
    public bool CanReceiveDamage => isActiveAndEnabled;

    [SerializeField] private BossAnimManager bossAnimManager;

    private readonly Subject<long> clickStream = new Subject<long>();
    private readonly List<long> clickBuffer = new List<long>();

    [Header("Tick Settings")]
    [SerializeField] float tickSeconds = 1f;
    [SerializeField] bool useUnscaledTime = true; 
    IDisposable sub;
    private CompositeDisposable runtimeSubs;
    [Header("Boss Attack Settings")]
    [SerializeField] private float normalAttackInterval  = 2.0f;
    [SerializeField] private float specialAttackInterval = 8.0f;

    [Tooltip("Minimum time gap between two attacks to avoid same-frame double fire.")]
    [SerializeField] private float minSeparation = 0.3f;

    [Tooltip("Small random offset to avoid repeated re-alignment.")]
    [SerializeField] private float normalJitterRange  = 0.15f;
    [SerializeField] private float specialJitterRange = 0.15f;

    private float _nextNormalTime;
    private float _nextSpecialTime;
    private float spawnTime;
    private string bossId;
    private bool hasDied;
    private float BossNow => useUnscaledTime ? Time.unscaledTime : Time.time;
    private float BossDelta => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    void Start()
    {
        if (bossAnimManager != null)
            bossAnimManager.OnSkillFired += OnBossSkillFired;

        // Initialize next-fire times with jitter so they don't sync on start
        float now = BossNow;
        _nextNormalTime  = now + normalAttackInterval  + UnityEngine.Random.Range(0f, normalJitterRange);
        _nextSpecialTime = now + specialAttackInterval + UnityEngine.Random.Range(0f, specialJitterRange);
    }

    void Update()
    {
        float now = BossNow;
        bool normalReady  = now >= _nextNormalTime;
        bool specialReady = now >= _nextSpecialTime;

        // If both ready, SPECIAL has priority
        if (specialReady)
        {
            // Try play special; if it plays, schedule next times
            if (bossAnimManager != null && bossAnimManager.TryPlaySpecial())
            {
                // schedule next SPECIAL with jitter
                _nextSpecialTime = now + specialAttackInterval + UnityEngine.Random.Range(0f, specialJitterRange);

                // if normal was also ready, push it a bit forward to avoid overlap
                if (normalReady)
                    _nextNormalTime = Mathf.Max(_nextNormalTime, now + minSeparation);
            }
            // If anim couldn't play (rare), lightly defer and retry soon
            else
            {
                _nextSpecialTime = now + 0.1f;
            }
        }
        else if (normalReady)
        {
            if (bossAnimManager != null && bossAnimManager.TryPlayNormal())
            {
                _nextNormalTime = now + normalAttackInterval + UnityEngine.Random.Range(0f, normalJitterRange);
            }
            else
            {
                _nextNormalTime = now + 0.1f;
            }
        }
    }
    void OnEnable()
    {
        DamageTargetRegistry.Register(this);
        hasDied = false;
        if (spawnTime <= 0f)
            spawnTime = BossNow;

        SetUp();

        if (enemyStatsManager == null) return;

        var scheduler = useUnscaledTime ? Scheduler.MainThreadIgnoreTimeScale : Scheduler.MainThread;

        sub = Observable.Interval(TimeSpan.FromSeconds(Mathf.Max(0.01f, tickSeconds)), scheduler)
            .Subscribe(_ =>
            {
                float cur  = enemyStatsManager.Get(StatType.CurrentHP);
                float max  = enemyStatsManager.Get(StatType.HP);
                float rps  = enemyStatsManager.Get(StatType.HpRegen);
                float gain = rps * tickSeconds; 

                if (Mathf.Approximately(gain, 0f)) return;
                if (gain > 0f && cur >= max) return;
                if (gain < 0f && cur <= 0f)  return; 

                enemyStatsManager.Set(StatType.CurrentHP, Mathf.Clamp(cur + gain, 0f, max));
            });
    }

    void OnDisable()
    {
        DamageTargetRegistry.Unregister(this);
        sub?.Dispose();
        sub = null;
        runtimeSubs?.Dispose();
        runtimeSubs = null;
        clickBuffer.Clear();
        hasDied = false;
    }

    void SetUp()
    {
        runtimeSubs?.Dispose();
        runtimeSubs = new CompositeDisposable();

        var scheduler = useUnscaledTime ? Scheduler.MainThreadIgnoreTimeScale : Scheduler.MainThread;
        Observable.Interval(TimeSpan.FromSeconds(1), scheduler)
            .Subscribe(_ =>
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                clickBuffer.RemoveAll(timestamp => now - timestamp > 1000);

                StatsManager.Ins.Set(StatType.ClickPerTick, clickBuffer.Count);
            })
            .AddTo(runtimeSubs);

        clickStream.Subscribe(time =>
        {
            clickBuffer.Add(time);
        }).AddTo(runtimeSubs);

        if (enemyStatsManager != null)
        {
            enemyStatsManager.GetReactive(StatType.CurrentHP)
                .Subscribe(val =>
                {
                    float maxHP = enemyStatsManager.Get(StatType.HP);
                    float fill = (maxHP > 0f) ? (val / maxHP) : 0f;
                    HpUI.fillAmount = Mathf.Clamp01(fill);
                    if (val <= 0f)
                        OnDying();
                })
                .AddTo(runtimeSubs);
        }
    }
    public void HandleClick()
    {
        float power = DamageInputPowerResolver.GetClickPower();
        TakeDamage(power);
    }

    public void ApplyDamageInput(DamageInputKind inputKind)
    {
        switch (inputKind)
        {
            case DamageInputKind.Click:
                HandleClick();
                return;
            case DamageInputKind.Hold:
                HandleHold();
                return;
            case DamageInputKind.Idle:
                HandleIdle();
                return;
            default:
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[Boss] Unknown damage input kind: {inputKind}", this);
#endif
                return;
        }
    }

    public void HandleHold()
    {
        if (DamageTickAccumulator.TryConsumeTick(ref accumulatedHoldTime, BossDelta, timeHoldReset))
        {
            float power = DamageInputPowerResolver.GetHoldTickPower(timeHoldReset);
            TakeDamage(power);
        }
    }

    public void HandleIdle()
    {
        float power = DamageInputPowerResolver.GetIdleTickPower(timeIdleReset);
        TakeDamage(power);
        PlayerController.Instance?.NotifyIdleDamageDealt(power, transform.position);
    }
    void TakeDamage(float power)
    {
        if (power <= 0f)
            return;

        enemyStatsManager.Set(StatType.CurrentHP, Mathf.Max(0, enemyStatsManager.Get(StatType.CurrentHP) - power));
        DamageStatsRecorder.RecordDamage(power, 1f);
        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        clickStream.OnNext(time);
    }
    void OnBossSkillFired(string skillId)
    {
        if (skillId == "Special")
        {
            // Example: double damage (or apply debuff/heal/etc.)
            StatsManager.Ins.Add(StatType.CurrentHP,
                -enemyStatsManager.Get(StatType.NormalPower) * 2f);
        }
        else // "Normal"
        {
            StatsManager.Ins.Add(StatType.CurrentHP,
                -enemyStatsManager.Get(StatType.NormalPower));
        }
    }

    void OnDying()
    {
        if (hasDied) return;
        hasDied = true;
        StatsManager.Ins.Add(StatType.TotalBlockBreaked, 1);
        string id = string.IsNullOrEmpty(bossId) ? gameObject.name : bossId;
        AnalyticsManager.Ins?.TrackBossKill(id, Mathf.Max(0f, BossNow - spawnTime));
        Died?.Invoke(this); 
    }

    public void SetAnalyticsContext(string id)
    {
        bossId = string.IsNullOrEmpty(id) ? gameObject.name : id;
        spawnTime = BossNow;
    }

    public void SetPointerHit(Vector3 worldPoint)
    {
        // Boss currently does not use pointer-hit context.
    }
}
