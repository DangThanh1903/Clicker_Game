using UnityEngine;
using UnityEngine.EventSystems;

public enum CombatMode
{
    Manual = 0,
    AutoPet = 1
}

public class PlayerController : MonoBehaviour, ICombatResourceReadModel, ICombatFeedbackSink, IRunFailNotifier
{
    private static PlayerController _instance;
    public static PlayerController Instance => _instance;
    private readonly GameplayInputGatePolicyService inputGatePolicyService = new GameplayInputGatePolicyService();
    private readonly PlayerCombatResourceService combatResourceService = new PlayerCombatResourceService();
    private readonly PlayerRunLifecycleService runLifecycleService = new PlayerRunLifecycleService();
    private readonly PlayerPointerDispatchService pointerDispatchService = new PlayerPointerDispatchService();
    private readonly PlayerDragRotateService dragRotateService = new PlayerDragRotateService();
    private readonly PlayerAutoAttackTickService autoAttackTickService = new PlayerAutoAttackTickService();
    private readonly ClickPerTickService clickPerTickService = new ClickPerTickService();
    private PlayerCombatVfxService combatVfxService;

    private CombatMode combatMode = CombatMode.Manual;
    public event System.Action<PlayerRunFailReason> OnRunFailed;
    public bool IsDead { get; private set; } // Reserved for future fail-state wiring.
    private IDamageReceiver pendingTarget;
    private ITargetRegistry targetRegistry;
    private RuntimeDamageTargetRegistry ownedTargetRegistry;
    private IDamageTargetSelectionService targetSelectionService = PriorityDamageTargetSelectionService.Instance;
    private IPointerDamageTargetResolver pointerTargetResolver = PhysicsPointerDamageTargetResolver.Instance;

    [SerializeField] bool useUnscaledTime = true;

    [Header("Pointer Raycast")]
    [SerializeField] private LayerMask pointerRaycastLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.1f)] private float pointerRaycastDistance = 200f;
    [SerializeField] private QueryTriggerInteraction pointerRaycastTriggerInteraction = QueryTriggerInteraction.Ignore;
    [Header("Screen Drag Rotate")]
    [SerializeField] private bool enableScreenDragRotate = true;
    [SerializeField, Min(0f)] private float dragRotateImpulsePerPixel = 2.8f;
    [SerializeField, Min(0f)] private float dragRotateMinDeltaPixels = 0.5f;
    [SerializeField, Range(0.05f, 2f)] private float dragRotateInputScale = 0.5f;
    [SerializeField, Min(1f)] private float dragRotateMaxImpulsePerFrame = 60f;
    [SerializeField, Min(0.1f)] private float dragRotateSpinDamping = 2.05f;
    [SerializeField, Min(0f)] private float dragRotateSpinMaxAngularSpeed = 0f;
    [SerializeField, Min(0.001f)] private float dragRotateSpinStopSpeedThreshold = 0.11f;

    [Header("Equipment Visuals")]
    [SerializeField] private Transform idlePetVisualAnchor;

    private PetItem equippedPet;
    private InventoryUIManager subscribedInventoryUiManager;
    public CombatMode CurrentCombatMode => combatMode;
    public bool IsAutoCombatMode => combatMode == CombatMode.AutoPet;
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

        if (ownedTargetRegistry == null)
            ownedTargetRegistry = new RuntimeDamageTargetRegistry();

        BindCombatRuntimeBootstrap();
        ConfigurePointerResolver();
        combatVfxService = new PlayerCombatVfxService();
        runLifecycleService.RunFailed += HandleRunFailed;
    }

    private void Start()
    {
        if (StatsManager.Ins == null)
            return;

        TryBindInventoryEquipmentEvents();
        IsDead = false;
        combatResourceService.InitializeResources();
        clickPerTickService.ResetRuntime();
        StatsManager.Ins.ForceNotifyStatsChanged();
        TryResolveEquippedItemsFromInventory();
    }
    void Update()
    {
        if (subscribedInventoryUiManager == null)
            TryBindInventoryEquipmentEvents();

        bool gameplayInputAllowed = IsGameplayInputAllowed();

        pointerDispatchService.Tick(
            gameplayInputAllowed,
            pointerTargetResolver,
            targetSelectionService,
            !IsPetEquipped(),
            OnClick);

        dragRotateService.Tick(
            gameplayInputAllowed,
            enableScreenDragRotate,
            pointerTargetResolver,
            ResolveCurrentBlockForDragRotate,
            IsPointerOverUiForDragRotate,
            dragRotateImpulsePerPixel,
            dragRotateMinDeltaPixels,
            dragRotateInputScale,
            dragRotateMaxImpulsePerFrame,
            dragRotateSpinDamping,
            dragRotateSpinMaxAngularSpeed,
            useUnscaledTime,
            dragRotateSpinStopSpeedThreshold);

        clickPerTickService.Tick(CombatNow);
        combatResourceService.Tick(
            false,
            CombatDelta);
    }

    void LateUpdate()
    {
        RefreshPendingTargetFromRegistry();
        if (!targetSelectionService.CanReceiveDamage(pendingTarget))
        {
            pendingTarget = null;
            return;
        }

        if (IsAutoCombatMode)
            ProcessAutoAttack(pendingTarget);
    }
    void OnDisable()
    {
        runLifecycleService.RunFailed -= HandleRunFailed;
        UnbindInventoryEquipmentEvents();
        combatResourceService.ResetRuntime();
        runLifecycleService.ResetRuntime();
        clickPerTickService.ResetRuntime();
        pointerDispatchService.ResetRuntime();
        dragRotateService.ResetRuntime();
        autoAttackTickService.ResetRuntime();
        CombatRuntimeBootstrap.UnbindOwner(this);
        IsDead = false;
        pendingTarget = null;
        equippedPet = null;
        combatMode = CombatMode.Manual;
        combatVfxService?.ResetImmediate();
    }

    private void OnEnable()
    {
        if (ownedTargetRegistry == null)
            ownedTargetRegistry = new RuntimeDamageTargetRegistry();

        BindCombatRuntimeBootstrap();
        ConfigurePointerResolver();
        runLifecycleService.RunFailed -= HandleRunFailed;
        runLifecycleService.RunFailed += HandleRunFailed;
        TryBindInventoryEquipmentEvents();
        clickPerTickService.ResetRuntime();
        pointerDispatchService.ResetRuntime();
        dragRotateService.ResetRuntime();
        IsDead = false;
        TryResolveEquippedItemsFromInventory();
    }

    private void BindCombatRuntimeBootstrap()
    {
        CombatRuntimeBootstrap.BindAll(
            this,
            this,
            this,
            this,
            ownedTargetRegistry);

        targetRegistry = ownedTargetRegistry;
    }
    public void OnClick(IDamageReceiver clickableObject)
    {
        if (IsAutoCombatMode)
            return;

        if (clickableObject == null || !clickableObject.CanReceiveDamage)
            return;

        clickableObject.ApplyDamageInput(DamageInputKind.Click);
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
        ConfigurePointerResolver();
    }

    private void RefreshPendingTargetFromRegistry()
    {
        pendingTarget = targetSelectionService.SelectBestTarget(targetRegistry);
    }

    private void ConfigurePointerResolver()
    {
        if (pointerTargetResolver is PhysicsPointerDamageTargetResolver physicsResolver)
        {
            physicsResolver.ConfigureRaycast(
                pointerRaycastLayers.value,
                Mathf.Max(0.1f, pointerRaycastDistance),
                pointerRaycastTriggerInteraction);
        }
    }

    public float ApplyStaminaToFinalDamage(float finalClickDamage)
    {
        return combatResourceService.ApplyStaminaToFinalDamage(
            finalClickDamage,
            !IsAutoCombatMode);
    }

    public float GetStaminaPercent()
    {
        return combatResourceService.GetStaminaPercent();
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
        pendingTarget = null;
        combatVfxService?.ResetImmediate();
        OnRunFailed?.Invoke(reason);
    }

    private bool IsGameplayInputAllowed()
    {
        return inputGatePolicyService.IsCombatInputAllowed();
    }

    private ClickableObject ResolveCurrentBlockForDragRotate()
    {
        if (BlockManager.Ins == null)
            return null;

        return BlockManager.Ins.CurrentBlock;
    }

    private static bool IsPointerOverUiForDragRotate()
    {
        if (EventSystem.current == null)
            return false;

        return EventSystem.current.IsPointerOverGameObject();
    }

    #region COMBAT_LOGIC -----------------------------------------------------------------------------------
    public void SetEquippedPet(PetItem pet)
    {
        equippedPet = pet;
        ReconcileCombatState();
    }

    public void NotifyAutoAttackDamageDealt(float damage, Vector3 targetWorldPosition)
    {
        combatVfxService?.NotifyAutoAttackDamageDealt(
            IsDead,
            IsAutoCombatMode,
            damage,
            targetWorldPosition);
    }

    public void ProcessAutoAttack(IDamageReceiver target)
    {
        autoAttackTickService.TickAndDispatch(
            target,
            IsAutoCombatMode,
            IsDead,
            CombatDelta,
            GetSummonAttackInterval());
    }

    private float GetSummonAttackInterval()
    {
        float summonAttackSpeed = StatsManager.Ins != null
            ? StatsManager.Ins.Get(StatType.SummonAttackSpeed)
            : 0f;
        summonAttackSpeed = Mathf.Max(0.05f, summonAttackSpeed);
        return 1f / summonAttackSpeed;
    }

    private void TryResolveEquippedItemsFromInventory()
    {
        var uiManager = InventoryController.Instance != null
            ? InventoryController.Instance.InventoryUIManager
            : null;
        if (uiManager == null)
            return;

        SetEquippedPet(uiManager.GetEquippedPet());
    }

    private void TryBindInventoryEquipmentEvents()
    {
        InventoryUIManager nextUiManager = InventoryController.Instance != null
            ? InventoryController.Instance.InventoryUIManager
            : null;

        if (ReferenceEquals(subscribedInventoryUiManager, nextUiManager))
            return;

        if (subscribedInventoryUiManager != null)
            subscribedInventoryUiManager.OnEquippedItemsChanged -= HandleEquippedItemsChanged;

        subscribedInventoryUiManager = nextUiManager;

        if (subscribedInventoryUiManager != null)
        {
            subscribedInventoryUiManager.OnEquippedItemsChanged += HandleEquippedItemsChanged;
            HandleEquippedItemsChanged();
        }
    }

    private void UnbindInventoryEquipmentEvents()
    {
        if (subscribedInventoryUiManager == null)
            return;

        subscribedInventoryUiManager.OnEquippedItemsChanged -= HandleEquippedItemsChanged;
        subscribedInventoryUiManager = null;
    }

    private void HandleEquippedItemsChanged()
    {
        TryResolveEquippedItemsFromInventory();
    }

    private void ReconcileCombatState()
    {
        bool hasPet = IsPetEquipped();
        CombatMode nextMode = hasPet ? CombatMode.AutoPet : CombatMode.Manual;
        bool modeChanged = combatMode != nextMode;
        combatMode = nextMode;

        if (modeChanged)
        {
            autoAttackTickService.OnCombatModeChanged(
                IsAutoCombatMode,
                GetSummonAttackInterval()); // first auto attack can happen immediately
        }

        combatVfxService?.HandleStateChanged(
            equippedPet,
            IsAutoCombatMode,
            IsDead,
            idlePetVisualAnchor,
            transform);
    }

    private bool IsPetEquipped()
    {
        return equippedPet != null && equippedPet.Type == ItemType.Pet;
    }

    #endregion
}
