using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DevWorkUiValidation
{
    [MenuItem("Tools/Validate DevWork UI Wiring")]
    public static void Validate()
    {
        int issueCount = 0;

        foreach (var manager in Object.FindObjectsByType<GameplayUIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            issueCount += ValidateGameplayUi(manager);

        foreach (var manager in Object.FindObjectsByType<UIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            issueCount += ValidateUiManager(manager);

        foreach (var toaster in Object.FindObjectsByType<Toaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            issueCount += ValidateToaster(toaster);

        foreach (var popup in Object.FindObjectsByType<PopupController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            issueCount += ValidatePopupController(popup);

        foreach (var manager in Object.FindObjectsByType<TopNotificationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            issueCount += ValidateTopNotificationManager(manager);

        issueCount += ValidatePrefabs();

        if (issueCount == 0)
            Debug.Log("[DevWork UI Validation] No wiring issues found.");
        else
            Debug.LogWarning($"[DevWork UI Validation] Found {issueCount} wiring issue(s). See logs above.");
    }

    private static int ValidatePrefabs()
    {
        int issueCount = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DevWork" });
        var seenPaths = new HashSet<string>();

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!seenPaths.Add(path))
                continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                issueCount += ValidatePrefab<GameplayUIManager>(root, path, ValidateGameplayUi);
                issueCount += ValidatePrefab<UIManager>(root, path, ValidateUiManager);
                issueCount += ValidatePrefab<Toaster>(root, path, ValidateToaster);
                issueCount += ValidatePrefab<PopupController>(root, path, ValidatePopupController);
                issueCount += ValidatePrefab<TopNotificationManager>(root, path, ValidateTopNotificationManager);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return issueCount;
    }

    private static int ValidatePrefab<T>(GameObject root, string path, System.Func<T, int> validator) where T : Component
    {
        int issueCount = 0;
        foreach (var component in root.GetComponentsInChildren<T>(true))
        {
            issueCount += validator(component);
            if (issueCount > 0)
                Debug.Log($"[DevWork UI Validation] Checked prefab: {path}", component);
        }

        return issueCount;
    }

    private static int ValidateGameplayUi(GameplayUIManager manager)
    {
        var so = new SerializedObject(manager);
        int issues = 0;
        issues += Require(so, "clickNumberUI", manager);
        issues += Require(so, "clickPerTickUI", manager);
        issues += Require(so, "diamondUI", manager);
        issues += Require(so, "blockNameUI", manager);
        issues += Require(so, "blockHealthUI", manager);
        issues += Require(so, "runTimerUI", manager);
        issues += Require(so, "manaUI", manager);
        return issues;
    }

    private static int ValidateUiManager(UIManager manager)
    {
        var so = new SerializedObject(manager);
        int issues = 0;
        issues += Require(so, "buttons", manager);
        issues += Require(so, "panels", manager);
        issues += Require(so, "uIPanel", manager);

        var buttons = so.FindProperty("buttons");
        var panels = so.FindProperty("panels");
        if (buttons != null && panels != null && buttons.arraySize != panels.arraySize)
        {
            Debug.LogWarning("[DevWork UI Validation] UIManager buttons/panels count mismatch.", manager);
            issues++;
        }

        return issues;
    }

    private static int ValidateToaster(Toaster toaster)
    {
        var so = new SerializedObject(toaster);
        int issues = 0;
        issues += Require(so, "canvas", toaster);
        issues += Require(so, "toastPrefab", toaster);
        return issues;
    }

    private static int ValidatePopupController(PopupController popup)
    {
        var so = new SerializedObject(popup);
        int issues = 0;
        issues += Require(so, "popupRoot", popup);
        issues += Require(so, "backdrop", popup);
        return issues;
    }

    private static int ValidateTopNotificationManager(TopNotificationManager manager)
    {
        var so = new SerializedObject(manager);
        int issues = 0;
        issues += Require(so, "viewPrefab", manager);
        issues += Require(so, "viewContainer", manager);
        return issues;
    }

    private static int Require(SerializedObject so, string propertyName, Object context)
    {
        var property = so.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning($"[DevWork UI Validation] Missing serialized property '{propertyName}'.", context);
            return 1;
        }

        if (property.propertyType == SerializedPropertyType.ObjectReference)
        {
            if (property.objectReferenceValue == null)
            {
                Debug.LogWarning($"[DevWork UI Validation] '{propertyName}' is not assigned.", context);
                return 1;
            }

            return 0;
        }

        if (property.isArray && property.arraySize == 0)
        {
            Debug.LogWarning($"[DevWork UI Validation] '{propertyName}' is empty.", context);
            return 1;
        }

        return 0;
    }
}
