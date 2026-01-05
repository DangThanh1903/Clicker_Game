using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lean.Pool;
using System;
using UniRx;

public class LocationLoader : MonoBehaviour
{
    public static LocationLoader Ins { get; private set; }

    public BlockSpawnLocation currentLocation;

    // Reactive stream for other systems (music, etc.)
    public ReactiveProperty<BlockSpawnLocation> ReactiveLocation { get; private set; }

    [Header("UI")]
    [SerializeField] private Button[] LocationButton;
    [SerializeField] private TMP_Text[] LocationText;

    [Header("Data")]
    [SerializeField] private LocationSO locationSO;

    [Header("Spawn Settings")]
    [SerializeField] private Transform locationParent;

    // runtime
    private GameObject currentInstance;
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
        InitializeLocationButton();

        if (!_bootstrapped)
            InitialLocation();
    }

    private void InitialLocation()
    {
        var loc = locationSO.GetByEnum(currentLocation);
        if (!loc.HasValue)
        {
            Debug.Log($"No LocationData for {currentLocation}");
            return;
        }

        SpawnLocation(loc.Value, isInitiate: true);
        _bootstrapped = true;

        // Ensure reactive matches current
        ReactiveLocation.Value = currentLocation;
    }

    private void InitializeLocationButton()
    {
        for (int i = 1; i < LocationButton.Length + 1; i++)
        {
            int cachedIndex = i;
            if (!Enum.IsDefined(typeof(BlockSpawnLocation), cachedIndex))
                continue;

            if (cachedIndex - 1 < LocationText.Length)
                LocationText[cachedIndex - 1].text = ((BlockSpawnLocation)cachedIndex).ToString();

            if (cachedIndex - 1 < LocationButton.Length)
            {
                LocationButton[cachedIndex - 1].onClick.AddListener(() =>
                {
                    UIManager.Ins.MoveToMain();
                    BlockSpawnLocation target = (BlockSpawnLocation)cachedIndex;

                    var locEnum = target.ToLocalized();
                    var handle = locEnum.GetLocalizedStringAsync();

                    if (currentLocation == target)
                    {
                        handle.Completed += h =>
                        {
                            GameDebugHandler.LogStaticKey("UI_Debug", "block_already_in", new { loc = h.Result });
                            UnityEngine.AddressableAssets.Addressables.Release(h);
                        };
                        return;
                    }

                    SetLocation(cachedIndex);

                    handle.Completed += h =>
                    {
                        GameDebugHandler.LogStaticKey("UI_Debug", "block_move_to", new { loc = h.Result });
                        UnityEngine.AddressableAssets.Addressables.Release(h);
                    };
                });
            }
        }
    }

    public void SetLocation(int index, bool isInitiate = false)
    {
        if (!Enum.IsDefined(typeof(BlockSpawnLocation), index))
        {
            Debug.Log($"Invalid location index: {index}");
            return;
        }

        BlockSpawnLocation newLoc = (BlockSpawnLocation)index;

        UIManager.Ins.SetLocationBackground(index - 1);

        currentLocation = newLoc;
        DataSaver.Ins.currentLocation = newLoc;
        if (DataSaver.Ins.PeakLocation == null || DataSaver.Ins.PeakLocation < newLoc)
            DataSaver.Ins.PeakLocation = newLoc;

        // Update reactive stream for music / other systems
        if (ReactiveLocation != null)
            ReactiveLocation.Value = newLoc;

        var data = locationSO.GetByEnum(newLoc);
        if (!data.HasValue)
        {
            Debug.Log($"No LocationData for {newLoc}");
            return;
        }

        SpawnLocation(data.Value, isInitiate);
        _bootstrapped = true;
    }

    // ================= LeanPool helpers =================

    public void SpawnLocation(LocationSO.LocationData data, bool isInitiate = false)
    {
        if (isInitiate || currentInstance == null)
        {
            DoSpawn(data);
            return;
        }

        DespawnCurrentLocationWithAnim(() => DoSpawn(data));
    }

    private void DoSpawn(LocationSO.LocationData data)
    {
        if (data.prefab == null)
        {
            Debug.Log($"Prefab is null for {data.location}");
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

        BlockManager.Ins.OnBlockBroken();
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
