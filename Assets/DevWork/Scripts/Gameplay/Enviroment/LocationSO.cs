using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

[CreateAssetMenu(fileName = "LocationDatabase", menuName = "Game/LocationSO", order = 0)]
public class LocationSO : ScriptableObject
{
    [Serializable]
    public struct LocationData
    {
        [BoxGroup("Location"), LabelText("Location"), EnumToggleButtons]
        public BlockSpawnLocation location;

        [BoxGroup("Settings")]
        [LabelText("Prefab")]
        public GameObject prefab;

        [BoxGroup("Settings")]
        [LabelText("Spawn Pos")]
        public Vector3 spawnPosition;

        [BoxGroup("Settings")]
        [LabelText("Spawn Rot (Euler)")]
        public Vector3 spawnRotationEuler;

        [BoxGroup("Settings")]
        [LabelText("Crafting Tree Prefab")]
        public GameObject craftingTreePrefab;
    }



    [TableList(AlwaysExpanded = true, ShowPaging = false)]
    public List<LocationData> locations = new List<LocationData>();

    // ===== Buttons (Odin) =====

    [Button(ButtonSizes.Medium), GUIColor(0.2f, 0.6f, 1f)]
    private void GenerateAll_SkipZero_Replace()
    {
        locations = BuildAllLocations(skipZero: true, keepExistingPrefabs: false);
    }

    [Button(ButtonSizes.Medium), GUIColor(0.2f, 1f, 0.4f)]
    private void SyncMissing_SkipZero_KeepPrefabs()
    {
        var merged = BuildAllLocations(skipZero: true, keepExistingPrefabs: true);
        locations = merged;
    }

    // ===== Helpers =====

    private List<LocationData> BuildAllLocations(bool skipZero, bool keepExistingPrefabs)
    {
        var oldMap = new Dictionary<BlockSpawnLocation, GameObject>();
        if (keepExistingPrefabs)
        {
            foreach (var ld in locations)
                if (!oldMap.ContainsKey(ld.location))
                    oldMap.Add(ld.location, ld.prefab);
        }

        var result = new List<LocationData>();
        var seen = new HashSet<BlockSpawnLocation>();

        foreach (BlockSpawnLocation loc in Enum.GetValues(typeof(BlockSpawnLocation)))
        {
            int intVal = Convert.ToInt32(loc);
            if (skipZero && intVal == 0) continue;
            if (seen.Contains(loc)) continue;

            var data = new LocationData
            {
                location = loc,
                prefab = keepExistingPrefabs && oldMap.TryGetValue(loc, out var pf) ? pf : null
            };

            result.Add(data);
            seen.Add(loc);
        }

        result.Sort((a, b) => Convert.ToInt32(a.location).CompareTo(Convert.ToInt32(b.location)));
        return result;
    }

    // ===== Lookup Functions =====

    /// <summary>
    /// Lấy LocationData theo index trong list.
    /// </summary>
    public LocationData? GetByIndex(int index)
    {
        if (index < 0 || index >= locations.Count) return null;
        return locations[index];
    }

    /// <summary>
    /// Lấy LocationData theo enum.
    /// </summary>
    public LocationData? GetByEnum(BlockSpawnLocation loc)
    {
        return locations.Find(ld => ld.location.Equals(loc));
    }

    /// <summary>
    /// Lấy LocationData theo tên enum (string).
    /// </summary>
    public LocationData? GetByName(string enumName, bool ignoreCase = true)
    {
        if (Enum.TryParse<BlockSpawnLocation>(enumName, ignoreCase, out var loc))
        {
            return GetByEnum(loc);
        }
        return null;
    }
}
