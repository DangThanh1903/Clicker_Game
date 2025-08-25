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

    [SerializeField] private BuffManager buffManager;

    private readonly Subject<long> clickStream = new Subject<long>();
    private readonly List<long> clickBuffer = new List<long>();
    [Header("Tick Settings")]
    [SerializeField] float tickSeconds = 1f;
    [SerializeField] bool useUnscaledTime = true; 
    IDisposable sub;

    void Awake()
    {
        buffManager.Initialize(enemyStatsManager);
    }
    void Start()
    {
        SetUp();
    }

    void Update()
    {
        HandleClickDetection();
    }
    void OnEnable()
    {
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
            })
            .AddTo(this);
    }

    void OnDisable()
    {
        sub?.Dispose();
        sub = null;
    }

    void SetUp()
    {
        Observable.Interval(TimeSpan.FromSeconds(1))
            .Subscribe(_ =>
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                clickBuffer.RemoveAll(timestamp => now - timestamp > 1000);

                StatsManager.Ins.Set(StatType.ClickPerTick, clickBuffer.Count);
            })
            .AddTo(this);

        clickStream.Subscribe(time =>
        {
            clickBuffer.Add(time);
        }).AddTo(this);

        enemyStatsManager.GetReactive(StatType.CurrentHP)
            .Subscribe(val =>
            {
                float maxHP = enemyStatsManager.Get(StatType.HP);
                float fill = (maxHP > 0f) ? (val / maxHP) : 0f;
                HpUI.fillAmount = Mathf.Clamp01(fill);
                if (val <= 0f)
                    OnDying();
            })
            .AddTo(this);
        ApplyStaterBuff();
    }
    void ApplyStaterBuff()
    {
        foreach (ConditionalBuffSO conditionalBuffSO in enemyStatsManager.GetAllStarterBuff())
        {
            buffManager.ApplyBuff(conditionalBuffSO);
        }
    }
    public void HandleClickDetection()
    {
        PlayerController.Instance.OnUpdate(this);

        if (!UIManager.Ins.IsMenuPanel()) return;

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

        StatsManager.Ins.Add(StatType.Clicks, power);

        TakeDamage(power);

        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        clickStream.OnNext(time);
    }

    public void HandleHold()
    {
        accumulatedHoldTime += Time.deltaTime;
        if (accumulatedHoldTime >= timeHoldReset)
        {
            float power = StatsManager.Ins.Get(StatType.HoldPower) * timeHoldReset;
            TakeDamage(power);
            accumulatedHoldTime = 0f;
            StatsManager.Ins.Add(StatType.Clicks, power);
            long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            clickStream.OnNext(time);
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
            StatsManager.Ins.Add(StatType.Clicks, power);
            long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            clickStream.OnNext(time);
        }
    }
    void TakeDamage(float power)
    {
        enemyStatsManager.Set(StatType.CurrentHP, Mathf.Max(0, enemyStatsManager.Get(StatType.CurrentHP) - power));
    }
    void DealDamage()
    {

    }
    void OnDying()
    {
        Died?.Invoke(this); 
    }

    void OnSpawn()
    {

    }
}
