using UnityEngine;

public class PlayerController : MonoBehaviour, ICombatResourceReadModel, ICombatFeedbackSink, IRunFailNotifier
{
    private static PlayerController _instance;
    public static PlayerController Instance => _instance;
    private readonly PlayerCombatResourceService combatResourceService = new PlayerCombatResourceService();
    private readonly PlayerRunLifecycleService runLifecycleService = new PlayerRunLifecycleService();
    private readonly ClickPerTickService clickPerTickService = new ClickPerTickService();
    private PlayerCombatVfxService combatVfxService;

    public ClickerState currentState = new NormalState();
    public event System.Action<PlayerRunFailReason> OnRunFailed;
    public bool IsDead { get; private set; } // Reserved for future fail-state wiring.
    private IDamageReceiver pendingTarget;
    private ITargetRegistry targetRegistry = DamageTargetRegistryRuntimeAdapter.Instance;
    private IDamageTargetSelectionService targetSelectionService = PriorityDamageTargetSelectionService.Instance;
    private IPointerDamageTargetResolver pointerTargetResolver = PhysicsPointerDamageTargetResolver.Instance;
    private bool pointerHoldActive;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private int debugDispatchFrame = -1;
    private int debugClickDispatchCount;
    private int debugHoldDispatchCount;
#endif

    [SerializeField] bool useUnscaledTime = true;

    [Header("Pickaxe")]
    [SerializeField] InventoryData pickaxeData;
    [SerializeField] private Transform holdBeamOrigin;
    [SerializeField] private Transform idlePetVisualAnchor;

    private Pickaxe equippedPickaxe;
    private const float TimeUnset = float.NegativeInfinity;
    private float lastHoldUpdateTime = TimeUnset;
    private float idleAttackTimer;
    public int IdleStackCount => combatResourceService.IdleStackCount;
    public bool UseUnscaledTime => useUnscaledTime;
    private float CombatNow => useUnscaledTime ? Time.unscaledTime : Time.time;
    private float CombatDelta => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    // Health and state setup
    private void Awake()
    {
        if (_instance == null)
            _instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        CombatResourceReadModelRuntime.Bind(this);
        CombatFeedbackRuntime.Bind(this);
        RunFailNotifierRuntime.Bind(this);
        combatVfxService = new PlayerCombatVfxService(this);
        runLifecycleService.RunFailed += HandleRunFailed;

        SetUpCurrentStateItem();  
    }

    private void Start()
    {
        if (StatsManager.Ins == null)
            return;

        IsDead = false;
        combatResourceService.InitializeResources();
        clickPerTickService.ResetRuntime();
        StatsManager.Ins.ForceNotifyStatsChanged();
    }
    void Update()
    {
        ProcessPointerInput();
        clickPerTickService.Tick(CombatNow);
        combatResourceService.Tick(
            false,
            currentState is HoldState,
            CombatNow,
            CombatDelta,
            lastHoldUpdateTime);

        if (combatVfxService != null &&
            combatVfxService.UpdateHoldBeamLifecycle(
                IsDead,
                currentState is HoldState,
                Input.GetMouseButton(0),
                CombatNow,
                lastHoldUpdateTime))
        {
            lastHoldUpdateTime = TimeUnset;
        }
    }

    void LateUpdate()
    {
        RefreshPendingTargetFromRegistry();
        if (!targetSelectionService.CanReceiveDamage(pendingTarget))
        {
            pendingTarget = null;
            return;
        }

        currentState.OnUpdate(this, pendingTarget);
    }
    void OnDisable()
    {
        runLifecycleService.RunFailed -= HandleRunFailed;
        combatResourceService.ResetRuntime();
        runLifecycleService.ResetRuntime();
        clickPerTickService.ResetRuntime();
        CombatResourceReadModelRuntime.Unbind(this);
        CombatFeedbackRuntime.Unbind(this);
        RunFailNotifierRuntime.Unbind(this);
        IsDead = false;
        pointerHoldActive = false;
        pendingTarget = null;
        lastHoldUpdateTime = TimeUnset;
        combatVfxService?.ResetImmediate();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        debugDispatchFrame = -1;
        debugClickDispatchCount = 0;
        debugHoldDispatchCount = 0;
#endif
    }

    private void OnEnable()
    {
        CombatResourceReadModelRuntime.Bind(this);
        CombatFeedbackRuntime.Bind(this);
        RunFailNotifierRuntime.Bind(this);
        runLifecycleService.RunFailed -= HandleRunFailed;
        runLifecycleService.RunFailed += HandleRunFailed;
        clickPerTickService.ResetRuntime();
        IsDead = false;
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
        combatResourceService.OnStateChanged();

        currentState.OnEnter(this);

        if (newState is IdleState)
            idleAttackTimer = GetSummonAttackInterval(); // first idle hit can happen immediately
        else
            idleAttackTimer = 0f;

        if (newState is not HoldState)
            lastHoldUpdateTime = TimeUnset;

        combatVfxService?.HandleStateChanged(
            equippedPickaxe,
            newState,
            IsDead,
            idlePetVisualAnchor,
            transform);
    }
    public void OnClick(IDamageReceiver clickableObject)
    {
        combatResourceService.OnClickDispatched(currentState is IdleState);
        currentState.OnClick(clickableObject);
    }

    public void SetTargetRegistry(ITargetRegistry registry)
    {
        if (registry == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[PlayerController] SetTargetRegistry requires a non-null registry.", this);
#endif
            return;
        }

        targetRegistry = registry;
    }

    public void SetTargetSelectionService(IDamageTargetSelectionService selectionService)
    {
        if (selectionService == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[PlayerController] SetTargetSelectionService requires a non-null selection service.", this);
#endif
            return;
        }

        targetSelectionService = selectionService;
    }

    public void SetPointerTargetResolver(IPointerDamageTargetResolver resolver)
    {
        if (resolver == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[PlayerController] SetPointerTargetResolver requires a non-null resolver.", this);
#endif
            return;
        }

        pointerTargetResolver = resolver;
    }

    private void RefreshPendingTargetFromRegistry()
    {
        pendingTarget = targetSelectionService.SelectBestTarget(targetRegistry);
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
        }

        if (!IsGameplayInputAllowed())
            return;

        bool mouseDown = Input.GetMouseButtonDown(0);
        bool mouseHeld = Input.GetMouseButton(0);
        if (!mouseDown && !mouseHeld)
            return;

        if (!pointerTargetResolver.TryResolvePointerTarget(out IDamageReceiver target, out Vector3 hitPoint))
            return;
        if (!targetSelectionService.CanReceiveDamage(target))
            return;

        if (mouseDown)
        {
            pointerTargetResolver.ApplyPointerHitContext(target, hitPoint);
            OnClick(target);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RegisterPointerClickDispatch();
#endif
        }

        if (mouseHeld)
        {
            if (!mouseDown)
                pointerTargetResolver.ApplyPointerHitContext(target, hitPoint);

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
        return combatResourceService.ApplyStaminaToFinalDamage(
            finalClickDamage,
            currentState is NormalState);
    }

    public float GetStaminaPercent()
    {
        return combatResourceService.GetStaminaPercent();
    }

    public float GetIdleDamageMultiplier()
    {
        return combatResourceService.GetIdleDamageMultiplier();
    }

    public float GetIdleStackPercent()
    {
        return combatResourceService.GetIdleStackPercent();
    }

    public void OnHold(IDamageReceiver clickableObject, Vector3 holdPoint)
    {
        if (currentState is HoldState)
        {
            lastHoldUpdateTime = CombatNow;
            combatVfxService?.OnHold(equippedPickaxe, holdBeamOrigin, holdPoint);
        }

        currentState.OnHold(this, clickableObject);
    }

    public float GetHoldDamageMultiplier()
    {
        return combatResourceService.GetHoldDamageMultiplier();
    }

    public void NotifyRunFailed(PlayerRunFailReason reason)
    {
        runLifecycleService.NotifyRunFailed(reason);
    }

    public void NotifyDamageHit()
    {
        clickPerTickService.RecordHit();
    }

    public int GetRecentHitCount(float windowSeconds = 1f)
    {
        return clickPerTickService.GetRecentHitCount(windowSeconds);
    }

    private void HandleRunFailed(PlayerRunFailReason reason)
    {
        pointerHoldActive = false;
        pendingTarget = null;
        lastHoldUpdateTime = TimeUnset;
        combatVfxService?.ResetImmediate();
        OnRunFailed?.Invoke(reason);
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

    #region COMBAT_LOGIC -----------------------------------------------------------------------------------
    public void UseMana()
    {
        combatResourceService.ConsumeMana(CombatDelta);
    }

    public void SetEquippedPickaxe(Pickaxe pickaxe)
    {
        equippedPickaxe = pickaxe;

        if (equippedPickaxe == null || equippedPickaxe.Type == ItemType.None)
        {
            SetState(new NormalState());
            lastHoldUpdateTime = TimeUnset;
            combatVfxService?.HandleEquippedPickaxeCleared();
            return;
        }

        SetStateByType(equippedPickaxe.currentState);
    }

    public void NotifyIdleDamageDealt(float damage, Vector3 targetWorldPosition)
    {
        combatVfxService?.NotifyIdleDamageDealt(
            IsDead,
            currentState is IdleState,
            damage,
            targetWorldPosition);
    }

    public void ProcessIdleAttack(IDamageReceiver target)
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

    #endregion
}
