using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

public class CraftNodeManager : MonoBehaviour
{
    // Runtime owner: current crafting tree state for the active biome.
    [Header("All nodes in this crafting tree")]
    public List<CraftNode> allNodes = new List<CraftNode>();

    [Header("Inventories to watch for items")]
    public List<InventoryData> inventoryDependencies = new List<InventoryData>();

    [Header("Save Scope")]
    [SerializeField] private string saveScope = "Default";

    [Header("Unlock Popup")]
    [SerializeField] private bool showRecipeUnlockPopup = true;
    [SerializeField] private bool useTopNotificationForUnlock = true;
    [SerializeField] private string unlockNotificationPrefix = "Unlocked recipe";
    [SerializeField, Min(0.2f)] private float unlockNotificationDuration = 1.6f;
    [SerializeField] private bool autoFindRecipeUnlockPopUp = true;
    [SerializeField] private PopupView recipeUnlockPopUp;

    private const string SaveKeyPrefix = "CraftNodeStates";
    private const string RecipeUnlockPopupName = "RecipeUnlockPopUp";
    private bool suppressDataSaverSave;
    private bool hasWarnedMissingUnlockPopup;
    private bool hasWarnedMissingUnlockPopupView;
    private bool hasWarnedMissingTopNotificationManager;
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
        if (!useTopNotificationForUnlock)
            TryResolveRecipeUnlockPopup();

        if (DataSaver.Ins != null)
            DataSaver.Ins.BindCraftNodeManager(this);

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

        if (!useTopNotificationForUnlock)
            TryStartUnlockPopupQueue();
    }

    private void OnEnable()
    {
        if (!useTopNotificationForUnlock)
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

        if (DataSaver.Ins != null)
            DataSaver.Ins.BindCraftNodeManager(this);
    }

    public void FinishNode(CraftNode node)
    {
        if (node == null)
            return;

        node.FinishNode();
        SaveNodeStates();
    }

    public void ApplyExternalRecipeUnlocks(IReadOnlyCollection<string> allowedRecipeIds)
    {
        for (int i = 0; i < allNodes.Count; i++)
        {
            CraftNode node = allNodes[i];
            if (node == null)
                continue;

            Item primaryItem = node.GetPrimaryRecipeItem();
            string recipeId = primaryItem != null && !string.IsNullOrWhiteSpace(primaryItem.itemName)
                ? primaryItem.itemName
                : node.nodeName;
            bool unlocked = ContainsIgnoreCase(allowedRecipeIds, recipeId);
            node.SetExternalUnlocked(unlocked);
        }

        RecheckAllNodes();
    }

    private void EnqueueUnlockPopup(CraftNode node)
    {
        if (!showRecipeUnlockPopup || node == null)
            return;

        if (useTopNotificationForUnlock)
        {
            if (TopNotificationManager.Ins == null)
            {
                if (!hasWarnedMissingTopNotificationManager)
                {
                    hasWarnedMissingTopNotificationManager = true;
                    Debug.LogWarning("CraftNodeManager: TopNotificationManager is missing, unlock notification will be skipped.");
                }
                return;
            }

            Item recipeItem = node.GetPrimaryRecipeItem();
            string recipeName = recipeItem != null && !string.IsNullOrWhiteSpace(recipeItem.itemName)
                ? recipeItem.itemName
                : (!string.IsNullOrWhiteSpace(node.nodeName) ? node.nodeName : "New Recipe");
            TopNotificationManager.NotifyQuest($"{unlockNotificationPrefix}: {recipeName}", unlockNotificationDuration);
            return;
        }

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
        public int[] array = Array.Empty<int>();
    }

    public void SaveNodeStates()
    {
        SaveNodeStates(queueDataSaverSave: true);
    }

    public void SaveNodeStates(bool queueDataSaverSave)
    {
        if (queueDataSaverSave && !suppressDataSaverSave && DataSaver.Ins != null)
            DataSaver.Ins.SaveDataFn();
    }

    public bool TryLoadLegacyPlayerPrefsStates(out List<int> states)
    {
        states = null;
        if (!PlayerPrefs.HasKey(SaveKey))
            return false;

        string json = PlayerPrefs.GetString(SaveKey);
        try
        {
            var wrapper = JsonUtility.FromJson<IntArrayWrapper>(json);
            if (wrapper == null || wrapper.array == null)
                return false;

            states = wrapper.array.ToList();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"CraftNodeManager: failed to migrate legacy PlayerPrefs states for scope '{CurrentSaveScope}': {ex.Message}");
            return false;
        }
    }

    public void DeleteLegacyPlayerPrefsStates()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
            return;

        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }

    public List<int> GetStates()
    {
        return allNodes.Select(n => n != null ? (int)n.State : (int)CraftNodeState.Locked).ToList();
    }

    public void ApplyStates(List<int> states, bool saveLocal = true)
    {
        if (states == null || states.Count == 0)
        {
            ResetStates(saveLocal);
            return;
        }

        suppressDataSaverSave = true;
        try
        {
            SetStates(states);
            RecheckAllNodes();

            if (saveLocal)
                SaveNodeStates(queueDataSaverSave: false);
        }
        finally
        {
            suppressDataSaverSave = false;
        }
    }

    public void ResetStates(bool saveLocal = true)
    {
        suppressDataSaverSave = true;
        try
        {
            SetStates(null);
            RecheckAllNodes();

            if (saveLocal)
                SaveNodeStates(queueDataSaverSave: false);
        }
        finally
        {
            suppressDataSaverSave = false;
        }
    }

    private void SetStates(List<int> states)
    {
        for (int i = 0; i < allNodes.Count; i++)
        {
            var node = allNodes[i];
            if (node == null)
                continue;

            node.State = states != null && i < states.Count
                ? (CraftNodeState)states[i]
                : CraftNodeState.Locked;
            node.UpdateVisual();
        }
    }

    private void RecheckAllNodes()
    {
        foreach (var node in allNodes)
            node?.RecheckState(true);
    }

    private static bool ContainsIgnoreCase(IReadOnlyCollection<string> source, string value)
    {
        if (source == null || string.IsNullOrWhiteSpace(value))
            return false;

        foreach (string entry in source)
        {
            if (string.Equals(entry, value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
    #endregion
}
