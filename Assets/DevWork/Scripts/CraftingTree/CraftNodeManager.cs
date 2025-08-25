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

    private void Start()
    {
        LoadNodeStates();

        foreach (var node in allNodes)
        {
            node.Init(inventoryDependencies);
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
        var states = allNodes.Select(n => (int)n.State).ToArray();
        string json = JsonUtility.ToJson(new IntArrayWrapper { array = states });
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
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
    #endregion
}
