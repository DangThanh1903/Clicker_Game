using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

public class CraftNodeManager : MonoBehaviour
{
    [Header("All nodes in this crafting tree")]
    public List<CraftNode> allNodes = new List<CraftNode>();

    [Header("Inventories to watch for items")]
    public List<InventoryData> inventoryDependencies = new List<InventoryData>();

    [Header("Save Scope")]
    [SerializeField] private string saveScope = "Default";

    [Header("Unlock Popup")]
    [SerializeField] private bool showRecipeUnlockPopup = true;
    [SerializeField] private bool autoFindRecipeUnlockPopUp = true;
    [SerializeField] private PopupView recipeUnlockPopUp;

    private const string SaveKeyPrefix = "CraftNodeStates";
    private const string RecipeUnlockPopupName = "RecipeUnlockPopUp";
    private bool suppressCloudSave;
    private bool hasWarnedMissingUnlockPopup;
    private bool hasWarnedMissingUnlockPopupView;
    private Coroutine unlockPopupRoutine;
    private readonly Queue<CraftNode> unlockPopupQueue = new Queue<CraftNode>();
    public string CurrentSaveScope => string.IsNullOrWhiteSpace(saveScope) ? "Default" : saveScope.Trim();
    public event Action<CraftNode> OnNodeUnlocked;
    public event Action<CraftNode> OnNodeFinished;

    private string SaveKey
    {
        get
        {
            return $"{SaveKeyPrefix}_{CurrentSaveScope}";
        }
    }

    private void Start()
    {
        TryResolveRecipeUnlockPopup();
        LoadNodeStates();

        if (DataSaver.Ins != null)
            DataSaver.Ins.RegisterCraftNodeManager(this);

        foreach (var node in allNodes)
        {
            if (node == null)
                continue;

            var capturedNode = node;
            node.OnStateChanged += (previous, current) =>
            {
                SaveNodeStates();
                if (previous != CraftNodeState.Unlocked && current == CraftNodeState.Unlocked)
                {
                    OnNodeUnlocked?.Invoke(capturedNode);
                    EnqueueUnlockPopup(capturedNode);
                }
                if (previous != CraftNodeState.Finished && current == CraftNodeState.Finished)
                    OnNodeFinished?.Invoke(capturedNode);
            };
        }

        foreach (var node in allNodes)
        {
            if (node == null)
                continue;

            node.Init(inventoryDependencies);
        }

        foreach (var node in allNodes)
        {
            node?.UpdateVisual();
        }

        TryStartUnlockPopupQueue();
    }

    private void OnEnable()
    {
        TryStartUnlockPopupQueue();
    }

    public void ConfigureSaveScope(string scope, bool reload = true)
    {
        string nextScope = string.IsNullOrWhiteSpace(scope) ? "Default" : scope.Trim();
        if (string.Equals(saveScope, nextScope, StringComparison.Ordinal))
            return;

        saveScope = nextScope;
        if (!reload)
            return;

        LoadNodeStates();
        foreach (var node in allNodes)
        {
            node?.RecheckState(true);
            node?.UpdateVisual();
        }
    }

    public void FinishNode(CraftNode node)
    {
        if (node == null)
            return;

        node.FinishNode();
        SaveNodeStates();
    }

    private void EnqueueUnlockPopup(CraftNode node)
    {
        if (!showRecipeUnlockPopup || node == null)
            return;

        if (recipeUnlockPopUp == null)
            TryResolveRecipeUnlockPopup();

        if (recipeUnlockPopUp == null)
        {
            if (!hasWarnedMissingUnlockPopup)
            {
                Debug.LogWarning("CraftNodeManager: RecipeUnlockPopUp is not assigned/found, unlock popup will be skipped.");
                hasWarnedMissingUnlockPopup = true;
            }
            return;
        }

        unlockPopupQueue.Enqueue(node);
        TryStartUnlockPopupQueue();
    }

    private void TryStartUnlockPopupQueue()
    {
        if (unlockPopupRoutine != null || unlockPopupQueue.Count == 0)
            return;

        if (PopupController.Instance != null && PopupController.Instance.isActiveAndEnabled)
        {
            unlockPopupRoutine = PopupController.Instance.StartCoroutine(RunUnlockPopupQueue());
            return;
        }

        if (!isActiveAndEnabled)
            return;

        unlockPopupRoutine = StartCoroutine(RunUnlockPopupQueue());
    }

    private IEnumerator RunUnlockPopupQueue()
    {
        while (unlockPopupQueue.Count > 0)
        {
            while (PopupController.Instance == null)
                yield return null;

            CraftNode node = unlockPopupQueue.Dequeue();
            var showTask = PopupController.Instance.Show(recipeUnlockPopUp, popup =>
            {
                if (popup is RecipeUnlockPopupView view)
                    view.Bind(node);
                else if (!hasWarnedMissingUnlockPopupView)
                {
                    Debug.LogWarning("CraftNodeManager: RecipeUnlockPopUp does not have RecipeUnlockPopupView. Popup will show but data won't bind.");
                    hasWarnedMissingUnlockPopupView = true;
                }
            });

            while (!showTask.IsCompleted)
                yield return null;

            while (PopupController.Instance != null && PopupController.Instance.IsAnyPopupOpen())
                yield return null;
        }

        unlockPopupRoutine = null;
    }

    private void TryResolveRecipeUnlockPopup()
    {
        if (recipeUnlockPopUp != null || !autoFindRecipeUnlockPopUp)
            return;

        PopupView[] popups = FindObjectsByType<PopupView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var popup in popups)
        {
            if (popup == null)
                continue;

            if (popup.gameObject.name.IndexOf(RecipeUnlockPopupName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                recipeUnlockPopUp = popup;
                return;
            }
        }
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
        var states = allNodes.Select(n => n != null ? (int)n.State : (int)CraftNodeState.Locked).ToArray();
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
        if (wrapper == null || wrapper.array == null)
            return;

        for (int i = 0; i < wrapper.array.Length && i < allNodes.Count; i++)
        {
            var node = allNodes[i];
            if (node == null)
                continue;

            node.State = (CraftNodeState)wrapper.array[i];
            node.UpdateVisual();
        }
    }

    public List<int> GetStates()
    {
        return allNodes.Select(n => n != null ? (int)n.State : (int)CraftNodeState.Locked).ToList();
    }

    public void ApplyStates(List<int> states, bool saveLocal = true)
    {
        if (states == null || states.Count == 0)
            return;

        suppressCloudSave = true;

        for (int i = 0; i < states.Count && i < allNodes.Count; i++)
        {
            var node = allNodes[i];
            if (node == null)
                continue;

            node.State = (CraftNodeState)states[i];
            node.UpdateVisual();
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
