using System;
using System.Collections;
using UniRx;
using UnityEngine;
using Lean.Pool;

public class PlayerController : MonoBehaviour
{
    private static PlayerController _instance;
    public static PlayerController Instance => _instance;
    private float manaRegenTimer = 0f;
    private float manaUsageTimer = 0f;
    private readonly float timeManaReset = 0.1f;

    public ClickerState currentState = new NormalState();
    public event Action OnDied;
    public bool IsDead { get; private set; }
    private IDamagable pendingTarget;
    private IDamagable pressedTarget;
    private bool pointerHoldActive;
    private static Camera cachedMainCamera;
    private static int cachedMainCameraFrame = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private int debugDispatchFrame = -1;
    private int debugClickDispatchCount;
    private int debugHoldDispatchCount;
#endif

    [Header("Tick Settings")]
    [SerializeField] float tickSeconds = 1f;
    [SerializeField] bool useUnscaledTime = true;
    [Header("Player Health")]
    [SerializeField] bool disablePlayerHealthSystem = true;
    IDisposable regenSub;
    IDisposable deathSub;

    [Header("Pickaxe")]
    [SerializeField] InventoryData pickaxeData;
    [SerializeField] private Transform holdBeamOrigin;
    [SerializeField] private Transform idlePetVisualAnchor;

    private Pickaxe equippedPickaxe;
    private GameObject activeHoldBeamObject;
    private HoldBeamVFX activeHoldBeam;
    private GameObject activeIdlePetObject;
    private GameObject activeIdlePetPrefab;
    private IIdlePetAttackFeedback activeIdlePetFeedback;
    private Animator activeIdlePetAnimatorFallback;
    private Vector3 pendingHoldPoint;
    private const float TimeUnset = float.NegativeInfinity;
    private float lastHoldUpdateTime = TimeUnset;
    private float timeSinceLastNormalClick = 999f;
    private float timeSinceIdleStackRefresh = 999f;
    private int idleStackCount;
    private float idleAttackTimer;
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");
    private const float StaminaRegenPercentPerSecond = 0.25f;
    public float CurrentStamina => StatsManager.Ins != null ? Mathf.Max(0f, StatsManager.Ins.Get(StatType.CurrentStamina)) : 0f;
    public float MaxStamina => StatsManager.Ins != null ? Mathf.Max(0f, StatsManager.Ins.Get(StatType.Stamina)) : 0f;
    public float StaminaPercent => MaxStamina > 0f ? Mathf.Clamp01(CurrentStamina / MaxStamina) : 0f;
    public int IdleStackCount => idleStackCount;
    public bool UseUnscaledTime => useUnscaledTime;
    private float CombatNow => useUnscaledTime ? Time.unscaledTime : Time.time;
    private float CombatDelta => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    // Health and state setup
    private void Awake()
    {
        if (_instance == null) _instance = this;
        else Destroy(gameObject);

        SetUpCurrentStateItem();  
    }

    private void Start()
    {
        if (StatsManager.Ins == null)
            return;

        if (disablePlayerHealthSystem)
            ForcePlayerHpToMax();
        else
            StatsManager.Ins.Set(StatType.CurrentHP, StatsManager.Ins.Get(StatType.HP));
        StatsManager.Ins.Set(StatType.CurrentMana, StatsManager.Ins.Get(StatType.Mana));
        StatsManager.Ins.Set(StatType.CurrentStamina, StatsManager.Ins.Get(StatType.Stamina));
        StatsManager.Ins.ForceNotifyStatsChanged();
    }
    void Update()
    {
        if (disablePlayerHealthSystem)
        {
            IsDead = false;
            ForcePlayerHpToMax();
        }

        ProcessPointerInput();
        UpdateIdleStackLifetime();
        UpdateStaminaOverTime();

        // Hold mana regen should not depend on a target dispatch path.
        if (!IsDead &&
            currentState is HoldState &&
            CombatNow - lastHoldUpdateTime > 0.08f)
        {
            RegenMana();
        }

        UpdateHoldBeamLifecycle();
    }

    void LateUpdate()
    {
        RefreshPendingTargetFromRegistry();
        if (!IsTargetDamageable(pendingTarget))
        {
            pendingTarget = null;
            return;
        }

        currentState.OnUpdate(this, pendingTarget);
    }
    void OnEnable()
    {
        regenSub?.Dispose(); regenSub = null;
        deathSub?.Dispose(); deathSub = null;

        if (disablePlayerHealthSystem)
        {
            IsDead = false;
            ForcePlayerHpToMax();
            return;
        }

        var scheduler = useUnscaledTime ? Scheduler.MainThreadIgnoreTimeScale : Scheduler.MainThread;

        // HP regen tick (skip when dead)
        regenSub = Observable.Interval(TimeSpan.FromSeconds(Mathf.Max(0.01f, tickSeconds)), scheduler)
            .Subscribe(_ =>
            {
                if (IsDead) return;

                float cur = StatsManager.Ins.Get(StatType.CurrentHP);
                float max = StatsManager.Ins.Get(StatType.HP);
                float rps = StatsManager.Ins.Get(StatType.HpRegen);
                float gain = rps * tickSeconds;

                if (Mathf.Approximately(gain, 0f)) return;
                if (gain > 0f && cur >= max) return;
                if (gain < 0f && cur <= 0f) return;

                StatsManager.Ins.Set(StatType.CurrentHP, Mathf.Clamp(cur + gain, 0f, max));
            })
            .AddTo(this);

        // Death detection (fires once)
        SubscribeDeath();
    }

    void OnDisable()
    {
        regenSub?.Dispose(); regenSub = null;
        deathSub?.Dispose(); deathSub = null;
        ResetIdleStack();
        pressedTarget = null;
        pointerHoldActive = false;
        pendingTarget = null;
        StopHoldBeam(immediate: true);
        StopIdlePetVisual(immediate: true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        debugDispatchFrame = -1;
        debugClickDispatchCount = 0;
        debugHoldDispatchCount = 0;
#endif
    }

    void SubscribeDeath()
    {
        deathSub?.Dispose();
        if (disablePlayerHealthSystem)
            return;

        deathSub = StatsManager.Ins?.GetReactive(StatType.CurrentHP)
            .Where(hp => hp <= 0f)
            .Take(1)
            .Subscribe(_ => Die())
            .AddTo(this);
    }
    void SetUpCurrentStateItem()
    {
        if (pickaxeData == null || pickaxeData.GetSize() <= 0)
        {
            SetEquippedPickaxe(null);
            return;
        }

        var slot = pickaxeData.GetItem(0);
        var item = slot != null ? slot.itemData : null;
        SetEquippedPickaxe(item as Pickaxe);
    }
    public void SetStateByType(PickaxeType pickaxeType)
    {
        switch (pickaxeType)
        {
            case PickaxeType.Normal:
                SetState(new NormalState());
                return;
            case PickaxeType.Hold:
                SetState(new HoldState());
                return;
            case PickaxeType.Idle:
                SetState(new IdleState());
                return;
            default:
                SetState(new NormalState());
                return;
        }
    }
    public void SetState(ClickerState newState)
    {
        currentState.OnExit(this);

        currentState = newState;

        currentState.OnEnter(this);

        if (newState is IdleState)
            idleAttackTimer = GetSummonAttackInterval(); // first idle hit can happen immediately
        else
            idleAttackTimer = 0f;

        if (newState is not HoldState)
            StopHoldBeam();

        RefreshIdlePetVisual();
    }
    public void OnClick(IDamagable clickableObject)
    {
        TryAddIdleStackFromNormalClick();
        currentState.OnClick(clickableObject);
    }

    private void RefreshPendingTargetFromRegistry()
    {
        DamageTargetRegistry.CompactInvalidTargets();
        var targets = DamageTargetRegistry.ActiveTargets;

        IDamagable bestTarget = null;
        int bestPriority = int.MinValue;

        for (int i = 0; i < targets.Count; i++)
        {
            IDamagable target = targets[i];
            if (!IsTargetDamageable(target))
                continue;

            int priority = GetPriority(target);
            if (priority >= bestPriority)
            {
                bestPriority = priority;
                bestTarget = target;
            }
        }

        pendingTarget = bestTarget;
    }

    private void ProcessPointerInput()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        BeginPointerDispatchDiagnosticsFrame(Time.frameCount);
#endif

        if (Input.GetMouseButtonUp(0))
        {
            if (pointerHoldActive && StatsManager.Ins != null)
                StatsManager.Ins.Set(StatType.HoldedTime, 0f);

            pointerHoldActive = false;
            pressedTarget = null;
        }

        if (!IsGameplayInputAllowed())
            return;

        bool mouseDown = Input.GetMouseButtonDown(0);
        bool mouseHeld = Input.GetMouseButton(0);
        if (!mouseDown && !mouseHeld)
            return;

        if (mouseDown)
            pressedTarget = null;

        if (!TryResolvePointerTarget(out IDamagable target, out Vector3 hitPoint))
            return;
        if (!IsTargetDamageable(target))
            return;

        if (mouseDown)
        {
            ApplyPointerHitContext(target, hitPoint);
            pressedTarget = target;
            OnClick(target);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RegisterPointerClickDispatch();
#endif
        }

        if (mouseHeld && CanDispatchHoldToTarget(target))
        {
            if (!mouseDown)
                ApplyPointerHitContext(target, hitPoint);

            OnHold(target, hitPoint);
            pointerHoldActive = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RegisterPointerHoldDispatch();
#endif
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void BeginPointerDispatchDiagnosticsFrame(int frame)
    {
        if (debugDispatchFrame == frame)
            return;

        if (debugDispatchFrame >= 0)
        {
            if (debugClickDispatchCount > 1)
                Debug.LogWarning($"[PlayerController] Multiple click dispatches in one frame: {debugClickDispatchCount} (frame {debugDispatchFrame}).", this);

            if (debugHoldDispatchCount > 1)
                Debug.LogWarning($"[PlayerController] Multiple hold dispatches in one frame: {debugHoldDispatchCount} (frame {debugDispatchFrame}).", this);
        }

        debugDispatchFrame = frame;
        debugClickDispatchCount = 0;
        debugHoldDispatchCount = 0;
    }

    private void RegisterPointerClickDispatch()
    {
        debugClickDispatchCount++;
    }

    private void RegisterPointerHoldDispatch()
    {
        debugHoldDispatchCount++;
    }
#endif

    public float ApplyStaminaToFinalDamage(float finalClickDamage)
    {
        float safeFinalDamage = Mathf.Max(0f, finalClickDamage);
        if (safeFinalDamage <= 0f)
            return 0f;

        // "Normal only": stamina affects only normal-mode clicks.
        // Input here is already the final click damage before stamina penalty.
        if (!(currentState is NormalState))
            return safeFinalDamage;

        if (StatsManager.Ins == null)
            return safeFinalDamage;

        // Any normal click attempt should reset stamina regen delay, even if stamina effect is ignored.
        timeSinceLastNormalClick = 0f;

        float staminaCost = Mathf.Max(0.1f, StatsManager.Ins.Get(StatType.StaminaCostPerClick));
        float currentStamina = Mathf.Max(0f, StatsManager.Ins.Get(StatType.CurrentStamina));
        bool ignoreStaminaEffect = StatsManager.Ins.Get(StatType.IgnoreStaminaEffect) > 0f;

        if (currentStamina >= staminaCost)
        {
            StatsManager.Ins.Set(StatType.CurrentStamina, Mathf.Max(0f, currentStamina - staminaCost));

            if (ignoreStaminaEffect)
                return safeFinalDamage;

            float haveStaminaMul = Mathf.Max(0f, StatsManager.Ins.Get(StatType.HaveStaminaDamageMul));
            return safeFinalDamage * Mathf.Max(0f, haveStaminaMul);
        }

        if (ignoreStaminaEffect)
            return safeFinalDamage;

        float multiplier = Mathf.Max(0f, StatsManager.Ins.Get(StatType.LowStaminaDamageMultiplier));
        return safeFinalDamage * Mathf.Max(0f, multiplier);
    }

    public void UpdateStaminaOverTime()
    {
        if (IsDead || StatsManager.Ins == null)
            return;

        float dt = CombatDelta;
        if (dt > 0f)
            timeSinceLastNormalClick += dt;

        float maxStamina = Mathf.Max(0f, StatsManager.Ins.Get(StatType.Stamina));
        float currentStamina = Mathf.Max(0f, StatsManager.Ins.Get(StatType.CurrentStamina));
        float regenTick = Mathf.Max(0f, StatsManager.Ins.Get(StatType.StaminaRegenTick));

        if (maxStamina <= 0f || currentStamina >= maxStamina)
            return;

        if (timeSinceLastNormalClick < regenTick)
            return;

        float staminaRegenPerSecond = maxStamina * StaminaRegenPercentPerSecond;
        currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRegenPerSecond * Mathf.Max(0f, dt));
        StatsManager.Ins.Set(StatType.CurrentStamina, currentStamina);
    }

    public float GetStaminaPercent()
    {
        return StaminaPercent;
    }

    private void TryAddIdleStackFromNormalClick()
    {
        if (currentState is not IdleState)
            return;

        int maxStack = GetIdleMaxStack();
        if (maxStack <= 0)
            return;

        idleStackCount = Mathf.Min(maxStack, idleStackCount + 1);
        timeSinceIdleStackRefresh = 0f;
    }

    private void UpdateIdleStackLifetime()
    {
        if (idleStackCount <= 0)
            return;

        float dt = CombatDelta;
        if (dt > 0f)
            timeSinceIdleStackRefresh += dt;

        float resetTime = GetIdleStackResetTime();
        if (timeSinceIdleStackRefresh >= resetTime)
            ResetIdleStack();
    }

    private void ResetIdleStack()
    {
        idleStackCount = 0;
        timeSinceIdleStackRefresh = 999f;
    }

    public float GetIdleDamageMultiplier()
    {
        int maxStack = GetIdleMaxStack();
        if (maxStack <= 0 || idleStackCount <= 0)
            return 1f;

        int effectiveStack = Mathf.Clamp(idleStackCount, 0, maxStack);
        float perStack = GetIdleStackDamagePerStack();
        return 1f + effectiveStack * perStack;
    }

    public float GetIdleStackPercent()
    {
        int maxStack = GetIdleMaxStack();
        if (maxStack <= 0)
            return 0f;

        return Mathf.Clamp01((float)idleStackCount / maxStack);
    }

    private int GetIdleMaxStack()
    {
        float maxStack = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.IdleMaxStack) : 0f;
        return Mathf.Max(0, Mathf.RoundToInt(maxStack));
    }

    private float GetIdleStackDamagePerStack()
    {
        float perStack = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.IdleStackDamagePerStack) : 0f;
        return Mathf.Max(0f, perStack);
    }

    private float GetIdleStackResetTime()
    {
        float resetTime = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.IdleStackResetTime) : 0f;
        return Mathf.Max(0.1f, resetTime);
    }

    public void OnHold(IDamagable clickableObject, Vector3 holdPoint)
    {
        if (currentState is HoldState)
        {
            pendingHoldPoint = holdPoint;
            lastHoldUpdateTime = CombatNow;
            EnsureHoldBeam();
            UpdateHoldBeamPositions(pendingHoldPoint);
        }

        currentState.OnHold(this, clickableObject);
    }

    public float GetHoldDamageMultiplier()
    {
        if (StatsManager.Ins == null)
            return 1f;

        float maxMana = Mathf.Max(0f, StatsManager.Ins.Get(StatType.Mana));
        if (maxMana <= 0f)
            return 1f;

        float currentMana = Mathf.Max(0f, StatsManager.Ins.Get(StatType.CurrentMana));
        float manaPercent = currentMana / maxMana;
        float highThreshold = Mathf.Clamp01(GetStatOrDefault(StatType.HighManaThreshold, 0.5f));
        float middleThreshold = Mathf.Clamp01(GetStatOrDefault(StatType.MiddleManaThreshold, 0.2f));
        middleThreshold = Mathf.Min(middleThreshold, highThreshold);

        if (manaPercent >= highThreshold)
        {
            float high = StatsManager.Ins.Get(StatType.HighManaMul);
            return Mathf.Max(1f, high);
        }

        if (manaPercent >= middleThreshold)
        {
            float middle = StatsManager.Ins.Get(StatType.MiddleManaMul);
            return Mathf.Max(1f, middle);
        }

        return 1f;
    }

    private float GetStatOrDefault(StatType statType, float defaultValue)
    {
        if (StatsManager.Ins == null)
            return defaultValue;

        float value = StatsManager.Ins.Get(statType);
        return value > 0f ? value : defaultValue;
    }

    private static int GetPriority(IDamagable target)
    {
        if (IsNullTarget(target))
            return int.MinValue;

        return target.InputPriority;
    }

    private bool IsGameplayInputAllowed()
    {
        var ui = UIManager.Ins;
        if (ui == null || !ui.IsBlockCanClick())
            return false;

        if (PopupController.Instance != null && PopupController.Instance.IsAnyPopupOpen())
            return false;

        return true;
    }

    private bool CanDispatchHoldToTarget(IDamagable target)
    {
        if (target is not MonsterClickable)
            return true;

        return ReferenceEquals(pressedTarget, target);
    }

    private bool TryResolvePointerTarget(out IDamagable target, out Vector3 hitPoint)
    {
        target = null;
        hitPoint = Vector3.zero;

        Camera cam = ResolveMainCamera();
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return false;

        hitPoint = hit.point;

        if (!TryGetDamageTargetFromHit(hit.transform, out IDamagable damageTarget))
            return false;

        target = damageTarget;
        return true;
    }

    private static void ApplyPointerHitContext(IDamagable target, Vector3 hitPoint)
    {
        target?.SetPointerHit(hitPoint);
    }

    private static Camera ResolveMainCamera()
    {
        if (cachedMainCamera != null && cachedMainCamera.isActiveAndEnabled)
            return cachedMainCamera;

        if (cachedMainCameraFrame == Time.frameCount)
            return cachedMainCamera;

        cachedMainCameraFrame = Time.frameCount;
        cachedMainCamera = Camera.main;
        return cachedMainCamera;
    }

    private static bool IsTargetDamageable(IDamagable target)
    {
        if (IsNullTarget(target))
            return false;

        return target.CanReceiveDamage;
    }

    private static bool TryGetDamageTargetFromHit(Transform hitTransform, out IDamagable damageTarget)
    {
        damageTarget = null;
        if (hitTransform == null)
            return false;

        damageTarget = hitTransform.GetComponent(typeof(IDamagable)) as IDamagable;
        if (!IsNullTarget(damageTarget))
            return true;

        damageTarget = hitTransform.GetComponentInParent(typeof(IDamagable)) as IDamagable;
        return !IsNullTarget(damageTarget);
    }

    private static bool IsNullTarget(IDamagable target)
    {
        if (ReferenceEquals(target, null))
            return true;

        if (target is UnityEngine.Object unityObj)
            return unityObj == null;

        return false;
    }

    #region COMBAT_LOGIC -----------------------------------------------------------------------------------
    public void UseMana()
    {
        manaUsageTimer += CombatDelta;

        if (manaUsageTimer >= timeManaReset)
        {
            float manaLostPerSecond = 10f * timeManaReset;
            StatsManager.Ins.Set(
                StatType.CurrentMana,
                Mathf.Max(StatsManager.Ins.Get(StatType.CurrentMana) - manaLostPerSecond, 0));
            manaUsageTimer = 0f;
        }
    }
    public void RegenMana()
    {
        manaRegenTimer += CombatDelta;

        if (manaRegenTimer >= timeManaReset)
        {
            float manaRegenerationPerSecond = StatsManager.Ins.Get(StatType.ManaRegen) * timeManaReset;
            StatsManager.Ins.Set(
                StatType.CurrentMana,
                Mathf.Min(StatsManager.Ins.Get(StatType.CurrentMana) + manaRegenerationPerSecond, StatsManager.Ins.Get(StatType.Mana)));
            manaRegenTimer = 0f;
        }
    }
    void Die()
    {
        if (disablePlayerHealthSystem)
        {
            IsDead = false;
            ForcePlayerHpToMax();
            return;
        }

        if (IsDead) return;
        IsDead = true;
        StopHoldBeam();
        StopIdlePetVisual();

        OnDied?.Invoke();

        Respawn(1f);
    }
    public void Respawn(float hpPercent = 1f)
    {
        if (disablePlayerHealthSystem)
        {
            IsDead = false;
            ForcePlayerHpToMax();
            return;
        }

        float percent = Mathf.Clamp01(hpPercent);
        float max = StatsManager.Ins.Get(StatType.HP);
        StatsManager.Ins.Set(StatType.CurrentHP, max * percent);
        StatsManager.Ins.ForceNotifyStatsChanged();
        IsDead = false;

        SubscribeDeath();
    }

    private void ForcePlayerHpToMax()
    {
        if (StatsManager.Ins == null)
            return;

        float maxHp = Mathf.Max(0f, StatsManager.Ins.Get(StatType.HP));
        float currentHp = Mathf.Max(0f, StatsManager.Ins.Get(StatType.CurrentHP));
        if (!Mathf.Approximately(currentHp, maxHp))
            StatsManager.Ins.Set(StatType.CurrentHP, maxHp);
    }

    public void SetEquippedPickaxe(Pickaxe pickaxe)
    {
        equippedPickaxe = pickaxe;

        if (equippedPickaxe == null || equippedPickaxe.Type == ItemType.None)
        {
            SetState(new NormalState());
            StopHoldBeam();
            StopIdlePetVisual();
            return;
        }

        SetStateByType(equippedPickaxe.currentState);
        RefreshIdlePetVisual();
    }

    public void NotifyIdleDamageDealt(float damage, Vector3 targetWorldPosition)
    {
        if (IsDead || currentState is not IdleState)
            return;
        if (activeIdlePetObject == null)
            return;

        if (activeIdlePetFeedback == null && activeIdlePetAnimatorFallback == null)
            CacheIdlePetFeedbackRefs();

        if (activeIdlePetFeedback != null)
        {
            activeIdlePetFeedback.PlayIdleAttack(Mathf.Max(0f, damage), targetWorldPosition);
            return;
        }

        if (activeIdlePetAnimatorFallback != null)
            activeIdlePetAnimatorFallback.SetTrigger(AttackTriggerHash);
    }

    public void ProcessIdleAttack(IDamagable target)
    {
        if (target == null || currentState is not IdleState || IsDead)
            return;

        float interval = GetSummonAttackInterval();
        float dt = CombatDelta;
        idleAttackTimer += dt;
        if (idleAttackTimer < interval)
            return;

        int ticks = Mathf.Min(3, Mathf.FloorToInt(idleAttackTimer / interval));
        idleAttackTimer -= ticks * interval;

        for (int i = 0; i < ticks; i++)
            target.ApplyDamageInput(DamageInputKind.Idle);
    }

    private float GetSummonAttackInterval()
    {
        float summonAttackSpeed = StatsManager.Ins != null
            ? StatsManager.Ins.Get(StatType.SummonAttackSpeed)
            : 0f;
        summonAttackSpeed = Mathf.Max(0.05f, summonAttackSpeed);
        return 1f / summonAttackSpeed;
    }

    private void RefreshIdlePetVisual()
    {
        bool shouldShow =
            !IsDead &&
            equippedPickaxe != null &&
            equippedPickaxe.Type != ItemType.None &&
            currentState is IdleState &&
            equippedPickaxe.IdlePetVisualPrefab != null;

        if (!shouldShow)
        {
            StopIdlePetVisual();
            return;
        }

        GameObject prefab = equippedPickaxe.IdlePetVisualPrefab;
        Transform anchor = idlePetVisualAnchor != null ? idlePetVisualAnchor : transform;

        if (activeIdlePetObject == null || activeIdlePetPrefab != prefab)
        {
            StopIdlePetVisual(immediate: true);
            activeIdlePetObject = LeanPool.Spawn(
                prefab,
                anchor.position,
                anchor.rotation,
                anchor);

            // Ensure pooled instance starts exactly at anchor local origin.
            Transform spawnedPetTransform = activeIdlePetObject.transform;
            if (spawnedPetTransform.parent != anchor)
                spawnedPetTransform.SetParent(anchor, false);
            spawnedPetTransform.localPosition = Vector3.zero;
            spawnedPetTransform.localEulerAngles = equippedPickaxe.IdlePetSpawnLocalEuler;

            activeIdlePetPrefab = prefab;
            CacheIdlePetFeedbackRefs();
            RefreshIdlePetLookYawBase();
        }

        if (activeIdlePetObject == null)
            return;

        Transform petTransform = activeIdlePetObject.transform;
        if (petTransform.parent != anchor)
            petTransform.SetParent(anchor, false);
        petTransform.localPosition = Vector3.zero;
        petTransform.localEulerAngles = equippedPickaxe.IdlePetSpawnLocalEuler;
        RefreshIdlePetLookYawBase();
    }

    private void StopIdlePetVisual(bool immediate = false)
    {
        if (activeIdlePetObject == null)
        {
            activeIdlePetPrefab = null;
            activeIdlePetFeedback = null;
            activeIdlePetAnimatorFallback = null;
            return;
        }

        GameObject petToDespawn = activeIdlePetObject;
        activeIdlePetObject = null;
        activeIdlePetPrefab = null;
        activeIdlePetFeedback = null;
        activeIdlePetAnimatorFallback = null;

        // No custom fade-out contract yet, keep lifecycle simple.
        if (immediate)
            LeanPool.Despawn(petToDespawn);
        else
            LeanPool.Despawn(petToDespawn);
    }

    private void CacheIdlePetFeedbackRefs()
    {
        activeIdlePetFeedback = null;
        activeIdlePetAnimatorFallback = null;

        if (activeIdlePetObject == null)
            return;

        var petBehaviours = activeIdlePetObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < petBehaviours.Length; i++)
        {
            if (activeIdlePetFeedback == null && petBehaviours[i] is IIdlePetAttackFeedback feedback)
                activeIdlePetFeedback = feedback;
        }

        if (activeIdlePetFeedback == null)
            activeIdlePetAnimatorFallback = activeIdlePetObject.GetComponentInChildren<Animator>(true);
    }

    private void RefreshIdlePetLookYawBase()
    {
        if (activeIdlePetFeedback is IdlePetAttackFeedback feedback)
            feedback.RefreshLookYawBaseFromCurrentPose();
    }

    private void UpdateHoldBeamLifecycle()
    {
        if (activeHoldBeamObject == null) return;

        bool invalidHold =
            IsDead ||
            currentState is not HoldState ||
            !Input.GetMouseButton(0);

        // Small tolerance avoids despawn/spawn jitter when one raycast frame is missed.
        if (!invalidHold && CombatNow - lastHoldUpdateTime > 0.15f)
            invalidHold = true;

        if (invalidHold)
            StopHoldBeam();
    }

    private void EnsureHoldBeam()
    {
        if (activeHoldBeamObject != null) return;
        if (equippedPickaxe == null) return;
        if (equippedPickaxe.HoldBeamVfxPrefab == null) return;

        activeHoldBeamObject = LeanPool.Spawn(equippedPickaxe.HoldBeamVfxPrefab);
        activeHoldBeam = activeHoldBeamObject.GetComponent<HoldBeamVFX>();
        Vector3 start = GetHoldBeamStartPosition();

        if (activeHoldBeam != null)
        {
            activeHoldBeam.Begin(start);
        }
        else
        {
            Debug.LogWarning("[PlayerController] Hold beam prefab is missing HoldBeamVFX component.");
        }
    }

    private void UpdateHoldBeamPositions(Vector3 endPoint)
    {
        if (activeHoldBeamObject == null) return;

        if (activeHoldBeam != null)
        {
            activeHoldBeam.SetEndPoint(endPoint);
        }
    }

    private Vector3 GetHoldBeamStartPosition()
    {
        Transform origin = holdBeamOrigin;

        if (origin == null && Camera.main != null)
            origin = Camera.main.transform;

        if (origin == null)
            return Vector3.zero;

        Vector3 offset = equippedPickaxe != null ? equippedPickaxe.HoldBeamStartOffset : Vector3.zero;
        return origin.position + origin.TransformDirection(offset);
    }

    private void StopHoldBeam(bool immediate = false)
    {
        if (activeHoldBeamObject == null)
        {
            activeHoldBeam = null;
            lastHoldUpdateTime = TimeUnset;
            return;
        }

        GameObject beamToStop = activeHoldBeamObject;
        HoldBeamVFX beamVfx = activeHoldBeam;

        activeHoldBeamObject = null;
        activeHoldBeam = null;
        lastHoldUpdateTime = TimeUnset;

        if (immediate || beamVfx == null)
        {
            LeanPool.Despawn(beamToStop);
            return;
        }

        beamVfx.EndBeam();
        float delay = beamVfx.EndDespawnDelay;
        if (delay <= 0f)
        {
            LeanPool.Despawn(beamToStop);
            return;
        }

        StartCoroutine(DespawnHoldBeamAfterDelay(beamToStop, delay));
    }

    private IEnumerator DespawnHoldBeamAfterDelay(GameObject beamObject, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (beamObject != null)
            LeanPool.Despawn(beamObject);
    }

    #endregion
}
