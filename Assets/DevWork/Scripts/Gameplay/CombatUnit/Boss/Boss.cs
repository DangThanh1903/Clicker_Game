using UnityEngine.UI;
using UniRx;
using UnityEngine;
using System.Collections.Generic;
using System;
using Lean.Pool;

public class Boss : MonoBehaviour, IDamagable
{
    [SerializeField] EnemyStatsManager enemyStatsManager;
    [SerializeField] Image HpUI;
    private bool isMouseHeld;
    private float accumulatedHoldTime = 0f;
    private readonly float timeHoldReset = 0.1f;
    private readonly float timeIdleReset = 1f;
    public event Action<Boss> Died;

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
    void Start()
    {
        if (bossAnimManager != null)
            bossAnimManager.OnSkillFired += OnBossSkillFired;

        // Initialize next-fire times with jitter so they don't sync on start
        float now = Time.time;
        _nextNormalTime  = now + normalAttackInterval  + UnityEngine.Random.Range(0f, normalJitterRange);
        _nextSpecialTime = now + specialAttackInterval + UnityEngine.Random.Range(0f, specialJitterRange);
    }

    void Update()
    {
        HandleClickDetection();

        float now = Time.time;
        bool normalReady  = now >= _nextNormalTime;
        bool specialReady = now >= _nextSpecialTime;

        // If both ready, SPECIAL has priority
        if (specialReady && (!normalReady || specialReady))
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
        if (spawnTime <= 0f)
            spawnTime = Time.unscaledTime;

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
        sub?.Dispose();
        sub = null;
        runtimeSubs?.Dispose();
        runtimeSubs = null;
        clickBuffer.Clear();
    }

    void SetUp()
    {
        runtimeSubs?.Dispose();
        runtimeSubs = new CompositeDisposable();

        Observable.Interval(TimeSpan.FromSeconds(1))
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
    public void HandleClickDetection()
    {
        var player = PlayerController.Instance;
        if (player == null) return;
        player.OnUpdate(this);

        var ui = UIManager.Ins;
        if (ui == null || !ui.IsBlockCanClick()) return;
        if (PopupController.Instance != null && PopupController.Instance.IsAnyPopupOpen()) return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                PlayerController.Instance.OnClick(this);
            }
        }

        if (Input.GetMouseButton(0))
        {
            if (!isMouseHeld)
            {
                isMouseHeld = true;
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                PlayerController.Instance.OnHold(this);
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (isMouseHeld)
            {
                isMouseHeld = false;
            }
        }
    }

    public void HandleClick()
    {
        float power = StatsManager.Ins.Get(StatType.NormalPower);
        TakeDamage(power);
    }

    public void HandleHold()
    {
        accumulatedHoldTime += Time.deltaTime;
        if (accumulatedHoldTime >= timeHoldReset)
        {
            float power = StatsManager.Ins.Get(StatType.HoldPower) * timeHoldReset;
            TakeDamage(power);
            accumulatedHoldTime = 0f;
        }
    }

    public void HandleIdle()
    {
        accumulatedHoldTime += Time.deltaTime;
        if (accumulatedHoldTime >= timeIdleReset)
        {
            float power = StatsManager.Ins.Get(StatType.IdlePower) * timeIdleReset;
            TakeDamage(power);
            accumulatedHoldTime = 0f;
        }
    }
    void TakeDamage(float power)
    {
        enemyStatsManager.Set(StatType.CurrentHP, Mathf.Max(0, enemyStatsManager.Get(StatType.CurrentHP) - power));
        StatsManager.Ins.Add(StatType.TotalDamageDealed, power);
        StatsManager.Ins.Add(StatType.Clicks, 1);
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
        StatsManager.Ins.Add(StatType.TotalBlockBreaked, 1);
        string id = string.IsNullOrEmpty(bossId) ? gameObject.name : bossId;
        AnalyticsManager.Ins?.TrackBossKill(id, Mathf.Max(0f, Time.unscaledTime - spawnTime));
        Died?.Invoke(this); 
    }

    void OnSpawn()
    {

    }

    public void SetAnalyticsContext(string id)
    {
        bossId = string.IsNullOrEmpty(id) ? gameObject.name : id;
        spawnTime = Time.unscaledTime;
    }
}
