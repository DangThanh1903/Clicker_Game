using UnityEngine;
using UniRx;
using System;
using Lean.Pool;
using GooglePlayGames.BasicApi;
using Unity.VisualScripting.Antlr3.Runtime.Misc;

public class MonsterClickable : MonoBehaviour, IDamagable
{
    private MonsterDef def;
     public float MaxHealth { get; private set; }
    public ReactiveProperty<float> CurrentHealth { get; private set; } = new ReactiveProperty<float>();

    private MonsterSpawner owner;
    private IDisposable lifeTimer;

    private bool isMouseHeld;
    private float accumulatedHoldTime = 0f;
    private readonly float timeHoldReset = 0.1f;
    private readonly float timeIdleReset = 1f;

    private bool resolved;

    // gọi từ spawner
    public void Init(MonsterDef d, MonsterSpawner spawner)
    {
        def = d;
        owner = spawner;
        resolved = false;

        MaxHealth = Mathf.Max(1f, def.MaxHP);
        CurrentHealth.Value = MaxHealth;

        // listen HP
        CurrentHealth
            .DistinctUntilChanged()
            .Subscribe(hp =>
            {
                if (hp <= 0f)
                    OnKilled();
            })
            .AddTo(this);

        // lifetime
        lifeTimer?.Dispose();
        lifeTimer = Observable.Timer(TimeSpan.FromSeconds(def.lifetime))
            .Subscribe(_ => OnMiss())
            .AddTo(this);
    }

    void Update()
    {
        HandleClickDetection();
    }

    // ===== IDamagable =====

    public void HandleClickDetection()
    {
        // giống Boss/Block
        PlayerController.Instance.OnUpdate(this);

        if (resolved) return;
        if (!UIManager.Ins.IsBlockCanClick()) return;
        if (PopupController.Instance != null && PopupController.Instance.IsAnyPopupOpen()) return;

        // Mouse Down
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                PlayerController.Instance.OnClick(this);
            }
        }

        // Mouse Held
        if (Input.GetMouseButton(0))
        {
            if (!isMouseHeld) isMouseHeld = true;

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
                StatsManager.Ins.Set(StatType.HoldedTime, 0);
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
        StatsManager.Ins.Add(StatType.HoldedTime, Time.deltaTime);

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

    // ===== Combat =====

    void TakeDamage(float power)
    {
        if (resolved) return;

        CurrentHealth.Value = Mathf.Max(0f, CurrentHealth.Value - power);

        // Optional: stats tracking
        StatsManager.Ins.Add(StatType.TotalDamageDealed, power);
        StatsManager.Ins.Add(StatType.Clicks, 1);

        // Optional: hit feedback (toast / anim)
        // Toaster.Show($"-{power:F1}", null, 0.2f, somePos);
    }

    void OnKilled()
    {
        if (resolved) return;
        resolved = true;

        // reward buff
        if (def != null && def.buffReward != null)
            StatsManager.Ins.ApplyConsumableBuff(def.buffReward);

        // sfx
        if (def != null && def.successSfx != null)
            SoundEffectController.Ins?.PlaySFX(def.successSfx);

        ResolveAndDespawn();
    }

    void OnMiss()
    {
        if (resolved) return;
        resolved = true;
        ResolveAndDespawn();
    }

    void ResolveAndDespawn()
    {
        lifeTimer?.Dispose();
        owner?.NotifyResolved(this);
        LeanPool.Despawn(gameObject);
    }

    void OnDisable()
    {
        lifeTimer?.Dispose();
        lifeTimer = null;
        resolved = false;
        isMouseHeld = false;
        accumulatedHoldTime = 0f;
    }
}
