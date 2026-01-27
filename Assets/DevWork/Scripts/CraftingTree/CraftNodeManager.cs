using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CraftNodeManager : MonoBehaviour
{
    [Header("All nodes in this crafting tree")]
    public List<CraftNode> allNodes = new List<CraftNode>();

    [Header("Inventories to watch for items")]
    public List<InventoryData> inventoryDependencies = new List<InventoryData>();

    private const string SaveKey = "CraftNodeStates";
    private bool suppressCloudSave;

    private void Start()
    {
        LoadNodeStates();

        if (DataSaver.Ins != null)
            DataSaver.Ins.RegisterCraftNodeManager(this);

        foreach (var node in allNodes)
        {
            node.Init(inventoryDependencies);
        }

        foreach (var node in allNodes)
        {
            node.OnStateChanged += (_, __) => SaveNodeStates();
        }

        foreach (var node in allNodes)
        {
            node.UpdateVisual();
        }
    }

    public void FinishNode(CraftNode node)
    {
        node.FinishNode();
        SaveNodeStates();
    }

    #region Save/Load
    [System.Serializable]
    private class IntArrayWrapper
    {
        public int[] array;
    }

    public void SaveNodeStates()
    {
        SaveNodeStates(saveCloud: true);
    }

    public void SaveNodeStates(bool saveCloud)
    {
        var states = allNodes.Select(n => (int)n.State).ToArray();
        string json = JsonUtility.ToJson(new IntArrayWrapper { array = states });
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();

        if (saveCloud && !suppressCloudSave && DataSaver.Ins != null)
            DataSaver.Ins.SaveDataFn();
    }

    public void LoadNodeStates()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return;

        string json = PlayerPrefs.GetString(SaveKey);
        var wrapper = JsonUtility.FromJson<IntArrayWrapper>(json);
        for (int i = 0; i < wrapper.array.Length && i < allNodes.Count; i++)
        {
            allNodes[i].State = (CraftNodeState)wrapper.array[i];
            allNodes[i].UpdateVisual();
        }
    }

    public List<int> GetStates()
    {
        return allNodes.Select(n => (int)n.State).ToList();
    }

    public void ApplyStates(List<int> states, bool saveLocal = true)
    {
        if (states == null || states.Count == 0)
            return;

        suppressCloudSave = true;

        for (int i = 0; i < states.Count && i < allNodes.Count; i++)
        {
            allNodes[i].State = (CraftNodeState)states[i];
            allNodes[i].UpdateVisual();
        }

        // Re-evaluate unlocks based on newly applied finished nodes
        foreach (var node in allNodes)
        {
            node?.RecheckState(true);
        }

        if (saveLocal)
            SaveNodeStates(saveCloud: false);

        suppressCloudSave = false;
    }
    #endregion
}
