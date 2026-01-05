using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Discovery
{
    /// <summary>
    /// Persists discovered blocks + discovered drops (per block).
    /// Works with your existing "blockName" and Item assets.
    /// </summary>
    public sealed class BlockDiscoveryService : MonoBehaviour
    {
        public static BlockDiscoveryService Ins { get; private set; }

        [SerializeField] private string prefsKey = "BLOCK_DISCOVERY_V1";

        private readonly HashSet<string> _blocks = new();
        private readonly HashSet<string> _drops  = new();

        public event Action<string> OnBlockDiscovered;              // blockName
        public event Action<string, string> OnDropDiscovered;       // blockName, itemId

        private void Awake()
        {
            if (Ins != null) { Destroy(gameObject); return; }
            Ins = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        // --- Block ---
        public bool IsBlockDiscovered(string blockName) => _blocks.Contains(blockName);

        public void DiscoverBlock(string blockName)
        {
            if (string.IsNullOrWhiteSpace(blockName)) return;

            if (_blocks.Add(blockName))
            {
                Save();
                OnBlockDiscovered?.Invoke(blockName);
            }
        }

        // --- Drop ---
        public bool IsDropDiscovered(string blockName, string itemId) => _drops.Contains(MakeDropKey(blockName, itemId));

        public void DiscoverDrop(string blockName, string itemId)
        {
            if (string.IsNullOrWhiteSpace(blockName) || string.IsNullOrWhiteSpace(itemId)) return;

            string key = MakeDropKey(blockName, itemId);
            if (_drops.Add(key))
            {
                Save();
                OnDropDiscovered?.Invoke(blockName, itemId);
            }
        }

        public static string MakeDropKey(string blockName, string itemId) => $"{blockName}:{itemId}";

        // IMPORTANT: choose stable id for Item
        public static string GetItemId(Item item)
        {
            if (item == null) return "";
            // Prefer asset name (stable), fallback to itemName if you rename assets a lot.
            return string.IsNullOrEmpty(item.name) ? item.itemName : item.name;
        }

        // --- Save/Load ---
        public void Save()
        {
            var data = new BlockDiscoverySaveData
            {
                discoveredBlocks = new List<string>(_blocks),
                discoveredDrops  = new List<string>(_drops),
            };

            PlayerPrefs.SetString(prefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void Load()
        {
            _blocks.Clear();
            _drops.Clear();

            if (!PlayerPrefs.HasKey(prefsKey)) return;

            string json = PlayerPrefs.GetString(prefsKey, "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var data = JsonUtility.FromJson<BlockDiscoverySaveData>(json);
                if (data?.discoveredBlocks != null)
                    foreach (var b in data.discoveredBlocks) _blocks.Add(b);

                if (data?.discoveredDrops != null)
                    foreach (var d in data.discoveredDrops) _drops.Add(d);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Discovery] Load failed: {e.Message}");
            }
        }

        // Dev helper
        public void ResetAll()
        {
            _blocks.Clear();
            _drops.Clear();
            PlayerPrefs.DeleteKey(prefsKey);
        }
    }
}
