using UnityEngine;
using Lean.Pool;
using System;
using UniRx;

public class LocationLoader : MonoBehaviour
{
    public static LocationLoader Ins { get; private set; }

    public BlockSpawnLocation currentLocation;
    private const BlockSpawnLocation DefaultUnlockedLocation = BlockSpawnLocation.Plain;

    // Reactive stream for other systems (music, etc.)
    public ReactiveProperty<BlockSpawnLocation> ReactiveLocation { get; private set; }

    [Header("Data")]
    [SerializeField] private LocationSO locationSO;

    [Header("Spawn Settings")]
    [SerializeField] private Transform locationParent;

    [Header("Biome Crafting Tree")]
    [SerializeField] private Transform craftingTreeParent;
    [SerializeField] private GameObject fallbackCraftingTreeRoot;

    // runtime
    private GameObject currentInstance;
    private GameObject currentCraftingTreeInstance;
    private bool _bootstrapped = false;

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }

        Ins = this;

        // Init reactive location with whatever currentLocation is (inspector / save)
        ReactiveLocation = new ReactiveProperty<BlockSpawnLocation>(currentLocation);
    }

    private void Start()
    {
        EnsureProgressInitialized();

        if (!_bootstrapped)
            InitialLocation();
    }

    private void EnsureProgressInitialized()
    {
        if (DataSaver.Ins == null)
            return;

        var peak = DataSaver.Ins.PeakLocation;
        if (!peak.HasValue || peak.Value < DefaultUnlockedLocation)
            DataSaver.Ins.PeakLocation = DefaultUnlockedLocation;

        if (DataSaver.Ins.currentLocation.HasValue &&
            DataSaver.Ins.PeakLocation.HasValue &&
            DataSaver.Ins.PeakLocation.Value < DataSaver.Ins.currentLocation.Value)
        {
            DataSaver.Ins.PeakLocation = DataSaver.Ins.currentLocation.Value;
        }
    }

    private void InitialLocation()
    {
        var loc = locationSO.GetByEnum(currentLocation);
        if (!loc.HasValue)
        {
            DevLog.Log($"No LocationData for {currentLocation}");
            return;
        }

        SpawnLocation(loc.Value, isInitiate: true);
        SwapCraftingTree(loc.Value);
        _bootstrapped = true;

        // Ensure reactive matches current
        ReactiveLocation.Value = currentLocation;
    }

    public void SetLocation(int index, bool isInitiate = false)
    {
        if (!Enum.IsDefined(typeof(BlockSpawnLocation), index))
        {
            DevLog.Log($"Invalid location index: {index}");
            return;
        }

        BlockSpawnLocation newLoc = (BlockSpawnLocation)index;

        if (!isInitiate && !IsLocationUnlocked(newLoc))
        {
            DevLog.Log($"Location is locked: {newLoc}");
            return;
        }

        UIManager.Ins?.SetLocationBackground(index - 1);

        if (isInitiate && _bootstrapped && currentLocation == newLoc)
        {
            if (ReactiveLocation != null)
                ReactiveLocation.Value = newLoc;
            return;
        }

        BlockSpawnLocation previousLocation = currentLocation;


        currentLocation = newLoc;
        if (DataSaver.Ins != null)
            DataSaver.Ins.currentLocation = newLoc;

        // Update reactive stream for music / other systems
        if (ReactiveLocation != null)
            ReactiveLocation.Value = newLoc;

        var data = locationSO.GetByEnum(newLoc);
        if (!data.HasValue)
        {
            DevLog.Log($"No LocationData for {newLoc}");
            return;
        }

        SpawnLocation(data.Value, isInitiate);
        SwapCraftingTree(data.Value);
        _bootstrapped = true;

        if (!isInitiate && previousLocation != newLoc)
            AnalyticsManager.Ins?.TrackLocationChange(previousLocation.ToString(), newLoc.ToString());
    }

    public BlockSpawnLocation GetHighestUnlockedLocation()
    {
        var peak = DataSaver.Ins != null ? DataSaver.Ins.PeakLocation : null;
        if (!peak.HasValue || peak.Value < DefaultUnlockedLocation)
            return DefaultUnlockedLocation;
        return peak.Value;
    }

    public bool IsLocationUnlocked(BlockSpawnLocation location)
    {
        if (location == BlockSpawnLocation.Any)
            return true;
        return location <= GetHighestUnlockedLocation();
    }

    public bool TryUnlockNextLocationFromBoss(BlockSpawnLocation clearedLocation)
    {
        int nextIndex = (int)clearedLocation + 1;
        if (!Enum.IsDefined(typeof(BlockSpawnLocation), nextIndex))
            return false;

        BlockSpawnLocation nextLocation = (BlockSpawnLocation)nextIndex;
        if (nextLocation == BlockSpawnLocation.Any)
            return false;

        BlockSpawnLocation peak = GetHighestUnlockedLocation();
        if (peak >= nextLocation)
            return false;

        if (DataSaver.Ins != null)
        {
            DataSaver.Ins.PeakLocation = nextLocation;
            DataSaver.Ins.SaveDataFn(true);
        }

        return true;
    }

    // ================= LeanPool helpers =================

    public void SpawnLocation(LocationSO.LocationData data, bool isInitiate = false)
    {
        if (currentInstance == null)
        {
            DoSpawn(data);
            return;
        }

        if (isInitiate)
        {
            DespawnCurrentLocationImmediate();
            DoSpawn(data);
            return;
        }

        DespawnCurrentLocationWithAnim(() => DoSpawn(data));
    }

    private void DespawnCurrentLocationImmediate()
    {
        if (!currentInstance)
            return;

        LeanPool.Despawn(currentInstance);
        currentInstance = null;
    }

    private void DoSpawn(LocationSO.LocationData data)
    {
        if (data.prefab == null)
        {
            DevLog.Log($"Prefab is null for {data.location}");
            return;
        }

        var rot = Quaternion.Euler(data.spawnRotationEuler);
        currentInstance = LeanPool.Spawn(
            data.prefab,
            data.spawnPosition,
            rot,
            locationParent
        );

        if (currentInstance.TryGetComponent<LocationAnimator>(out var anim))
        {
            anim.PlaySpawn().Subscribe().AddTo(this);
        }

        if (BlockManager.Ins != null)
            BlockManager.Ins.RefreshBlockForLocationChange();
    }

    private void SwapCraftingTree(LocationSO.LocationData data)
    {
        GameObject prefab = data.craftingTreePrefab;

        if (prefab == null)
        {
            ClearCraftingTreeInstance();
            if (fallbackCraftingTreeRoot != null)
                fallbackCraftingTreeRoot.SetActive(true);
            return;
        }

        if (fallbackCraftingTreeRoot != null)
            fallbackCraftingTreeRoot.SetActive(false);

        ClearCraftingTreeInstance();

        Transform parent = craftingTreeParent != null ? craftingTreeParent : null;
        currentCraftingTreeInstance = parent != null
            ? Instantiate(prefab, parent, false)
            : Instantiate(prefab);

        if (currentCraftingTreeInstance == null)
            return;

        if (!currentCraftingTreeInstance.activeSelf)
            currentCraftingTreeInstance.SetActive(true);

        CraftNodeManager manager = currentCraftingTreeInstance.GetComponent<CraftNodeManager>();
        if (manager == null)
            manager = currentCraftingTreeInstance.GetComponentInChildren<CraftNodeManager>(true);
        if (manager == null)
            return;

        manager.ConfigureSaveScope(data.location.ToString(), reload: true);
        if (DataSaver.Ins != null)
            DataSaver.Ins.RegisterCraftNodeManager(manager);
    }

    private void ClearCraftingTreeInstance()
    {
        if (currentCraftingTreeInstance == null)
            return;

        Destroy(currentCraftingTreeInstance);
        currentCraftingTreeInstance = null;
    }

    private void DespawnCurrentLocationWithAnim(Action onDone)
    {
        if (!currentInstance)
        {
            onDone?.Invoke();
            return;
        }

        if (currentInstance.TryGetComponent<LocationAnimator>(out var anim))
        {
            anim.PlayDespawn()
                .Subscribe(_ =>
                {
                    LeanPool.Despawn(currentInstance);
                    currentInstance = null;
                    onDone?.Invoke();
                })
                .AddTo(this);
        }
        else
        {
            LeanPool.Despawn(currentInstance);
            currentInstance = null;
            onDone?.Invoke();
        }
    }
}

