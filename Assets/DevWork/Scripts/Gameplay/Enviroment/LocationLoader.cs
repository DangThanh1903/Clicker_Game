using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lean.Pool; // <- Lean Pool
using System;

public class LocationLoader : MonoBehaviour
{
    public BlockSpawnLocation currentLocation;

    [Header("UI")]
    [SerializeField] private Button[] LocationButton;
    [SerializeField] private TMP_Text[] LocationText;

    [Header("Data")]
    [SerializeField] private LocationSO locationSO;

    [Header("Spawn Settings")]
    [SerializeField] private Transform locationParent;

    // runtime
    private GameObject currentInstance;

    private void Start()
    {
        InitializeLocationButton();
        InitialLocation();
    }

    private void OnDisable()
    {
        // đảm bảo thu hồi instance khi object bị disable (tuỳ nhu cầu)
        DespawnCurrentLocation();
    }

    private void InitialLocation()
    {
        var loc = locationSO.GetByEnum(currentLocation);
        if (loc.HasValue == false)
        {
            Debug.Log($"No LocationData for {currentLocation}");
            return;
        }

        SpawnLocation(loc.Value);
    }

    private void InitializeLocationButton()
    {
        // giả định enum có giá trị 0 là None/Invalid và bạn muốn bỏ qua 0
        // Button/Text mảng tương ứng các location từ 1..N
        for (int i = 1; i < LocationButton.Length + 1; i++)
        {
            int cachedIndex = i;

            // bảo vệ index hợp lệ với enum
            if (!Enum.IsDefined(typeof(BlockSpawnLocation), cachedIndex))
                continue;

            // gán text
            if (cachedIndex - 1 < LocationText.Length)
                LocationText[cachedIndex - 1].text = ((BlockSpawnLocation)cachedIndex).ToString();

            // gán listener
            if (cachedIndex - 1 < LocationButton.Length)
            {
                LocationButton[cachedIndex - 1].onClick.AddListener(() =>
                {
                    UIManager.Ins.MoveToMain();
                    BlockSpawnLocation target = (BlockSpawnLocation)cachedIndex;
                    var locEnum = target.ToLocalized();
                    var handle = locEnum.GetLocalizedStringAsync();

                    if (currentLocation == (BlockSpawnLocation)cachedIndex)
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

    public void SetLocation(int index)
    {
        if (!Enum.IsDefined(typeof(BlockSpawnLocation), index))
        {
            Debug.Log($"Invalid location index: {index}");
            return;
        }

        BlockSpawnLocation newLoc = (BlockSpawnLocation)index;

        // cập nhật UI nền (giữ logic cũ)
        UIManager.Ins.SetLocationBackground(index - 1);

        // cập nhật state
        currentLocation = newLoc;
        DataSaver.Ins.currentLocation = newLoc;
        if (DataSaver.Ins.PeakLocation == null || DataSaver.Ins.PeakLocation < newLoc)
            DataSaver.Ins.PeakLocation = newLoc;

        // spawn/despawn qua LeanPool
        var data = locationSO.GetByEnum(newLoc);
        if (data.HasValue == false)
        {
            Debug.Log($"No LocationData for {newLoc}");
            return;
        }

        SpawnLocation(data.Value);
    }

    // ================= LeanPool helpers =================

    private void SpawnLocation(LocationSO.LocationData data)
    {
        DespawnCurrentLocation();

        if (data.prefab == null)
        {
            Debug.Log($"Prefab is null for {data.location}");
            return;
        }

        var rot = Quaternion.Euler(data.spawnRotationEuler);
        currentInstance = Lean.Pool.LeanPool.Spawn(
            data.prefab,
            data.spawnPosition,
            rot,
            locationParent
        );
    }


    private void DespawnCurrentLocation()
    {
        if (currentInstance == null) return;
        if (currentInstance.activeInHierarchy)
            LeanPool.Despawn(currentInstance);
        else
            LeanPool.Despawn(currentInstance); // an toàn dù inactive
        currentInstance = null;
    }
}
