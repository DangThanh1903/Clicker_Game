#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Lean.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public static class LocalizationDataUpdater
{
    private const string UrlPrefsKey = "DevWork.Localization.GoogleSheetCsvUrl";
    private const string OutputFolderPrefsKey = "DevWork.Localization.OutputFolder";
    private const string DefaultOutputFolder = "Assets/DevWork/Localization";
    private const string ManagerPrefabPath = "Assets/DevWork/Prefabs/Localization/LocalizationManager.prefab";
    private const string BootstrapScenePath = "Assets/Scenes/SplashScene.unity";

    [MenuItem("Tools/Update Localization Data")]
    public static void UpdateLocalizationData()
    {
        string url = EditorPrefs.GetString(UrlPrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(url))
        {
            LocalizationDataUpdaterWindow.Open();
            return;
        }

        string outputFolder = EditorPrefs.GetString(OutputFolderPrefsKey, DefaultOutputFolder);
        DownloadAndWrite(url, outputFolder);
    }

    [MenuItem("Tools/Localization Settings")]
    public static void OpenSettings()
    {
        LocalizationDataUpdaterWindow.Open();
    }

    [MenuItem("Tools/Localization/Create Localization Manager Prefab")]
    public static void CreateLocalizationManagerPrefab()
    {
        EnsureManagerPrefab(EditorPrefs.GetString(OutputFolderPrefsKey, DefaultOutputFolder));
    }

    [MenuItem("Tools/Localization/Install Localization Manager In Bootstrap Scene")]
    public static void InstallLocalizationManagerInBootstrapScene()
    {
        EnsureManagerPrefab(EditorPrefs.GetString(OutputFolderPrefsKey, DefaultOutputFolder));

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BootstrapScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogWarning($"[LocalizationDataUpdater] Bootstrap scene not found: {BootstrapScenePath}");
            return;
        }

        if (UnityEngine.Object.FindObjectOfType<LocalizationManager>() != null)
        {
            Debug.Log("[LocalizationDataUpdater] Bootstrap scene already contains a LocalizationManager.");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[LocalizationDataUpdater] LocalizationManager prefab not found: {ManagerPrefabPath}");
            return;
        }

        var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            Debug.LogWarning("[LocalizationDataUpdater] Failed to instantiate LocalizationManager prefab.");
            return;
        }

        instance.name = nameof(LocalizationManager);
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

        Debug.Log($"[LocalizationDataUpdater] Installed LocalizationManager prefab in {BootstrapScenePath}.");
    }

    internal static void SaveSettings(string url, string outputFolder)
    {
        EditorPrefs.SetString(UrlPrefsKey, url ?? string.Empty);
        EditorPrefs.SetString(OutputFolderPrefsKey, string.IsNullOrWhiteSpace(outputFolder) ? DefaultOutputFolder : outputFolder);
    }

    internal static string GetSavedUrl()
    {
        return EditorPrefs.GetString(UrlPrefsKey, string.Empty);
    }

    internal static string GetSavedOutputFolder()
    {
        return EditorPrefs.GetString(OutputFolderPrefsKey, DefaultOutputFolder);
    }

    internal static void DownloadAndWrite(string url, string outputFolder)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Debug.LogWarning("[LocalizationDataUpdater] Google Sheet CSV URL is empty.");
            return;
        }

        outputFolder = string.IsNullOrWhiteSpace(outputFolder) ? DefaultOutputFolder : outputFolder;
        EnsureFolder(outputFolder);

        string csv = DownloadText(url);
        if (string.IsNullOrWhiteSpace(csv))
            return;

        List<List<string>> rows = LocalizationCsvParser.Parse(csv);
        if (rows.Count < 2)
        {
            Debug.LogWarning("[LocalizationDataUpdater] CSV has no data rows.");
            return;
        }

        Dictionary<GameLanguage, Dictionary<string, string>> languageRows = SplitByLanguage(rows);
        if (languageRows.Count == 0)
        {
            Debug.LogWarning("[LocalizationDataUpdater] No language columns found. Expected headers like key, English, Vietnamese.");
            return;
        }

        foreach (var pair in languageRows)
        {
            string languageName = LocalizationLanguageUtility.GetDefaultLeanLanguageName(pair.Key);
            string path = $"{outputFolder}/{languageName}.csv";
            File.WriteAllText(path, BuildLeanCsv(pair.Value), new UTF8Encoding(false));
        }

        AssetDatabase.Refresh();

        SetupActiveManager(outputFolder);
        EnsureManagerPrefab(outputFolder);
        LocalizationManager.UpdateActiveLocalizationData();

        Debug.Log($"[LocalizationDataUpdater] Updated {languageRows.Count} language CSV file(s) in {outputFolder}.");
    }

    private static Dictionary<GameLanguage, Dictionary<string, string>> SplitByLanguage(List<List<string>> rows)
    {
        var result = new Dictionary<GameLanguage, Dictionary<string, string>>();
        var header = rows[0];
        int keyColumn = FindKeyColumn(header);
        if (keyColumn < 0)
            keyColumn = 0;

        var languageColumns = new Dictionary<int, GameLanguage>();
        for (int i = 0; i < header.Count; i++)
        {
            if (i == keyColumn)
                continue;

            if (LocalizationLanguageUtility.TryParseLanguage(header[i], out var language))
            {
                languageColumns[i] = language;
                if (result.ContainsKey(language) == false)
                    result[language] = new Dictionary<string, string>();
            }
        }

        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            string key = GetCell(row, keyColumn).Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;

            foreach (var pair in languageColumns)
            {
                string value = GetCell(row, pair.Key);
                result[pair.Value][key] = value;
            }
        }

        return result;
    }

    private static int FindKeyColumn(List<string> header)
    {
        for (int i = 0; i < header.Count; i++)
        {
            string value = header[i]?.Trim();
            if (string.Equals(value, "key", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "translationName", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string GetCell(List<string> row, int index)
    {
        if (row == null || index < 0 || index >= row.Count)
            return string.Empty;

        return row[index] ?? string.Empty;
    }

    private static string BuildLeanCsv(Dictionary<string, string> entries)
    {
        var builder = new StringBuilder();
        foreach (var pair in entries)
        {
            builder.Append(EscapeCsv(pair.Key));
            builder.Append(',');
            builder.Append(EscapeCsv(pair.Value));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value == null)
            return string.Empty;

        bool mustQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
        if (mustQuote == false)
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static string DownloadText(string url)
    {
        using (var request = UnityWebRequest.Get(url))
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
                Thread.Sleep(10);

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[LocalizationDataUpdater] Download failed: {request.error}");
                return string.Empty;
            }

            return request.downloadHandler.text;
        }
    }

    private static void SetupActiveManager(string outputFolder)
    {
        var manager = UnityEngine.Object.FindObjectOfType<LocalizationManager>();
        if (manager == null)
            return;

        SetupManagerObject(manager.gameObject, outputFolder);
        EditorUtility.SetDirty(manager);
    }

    private static void EnsureManagerPrefab(string outputFolder)
    {
        EnsureFolder(Path.GetDirectoryName(ManagerPrefabPath).Replace("\\", "/"));

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
        GameObject root;
        bool temporaryRoot = false;

        if (prefab != null)
        {
            root = PrefabUtility.LoadPrefabContents(ManagerPrefabPath);
        }
        else
        {
            root = new GameObject(nameof(LocalizationManager));
            root.AddComponent<LeanLocalization>();
            root.AddComponent<LocalizationManager>();
            temporaryRoot = true;
        }

        SetupManagerObject(root, outputFolder);
        PrefabUtility.SaveAsPrefabAsset(root, ManagerPrefabPath);

        if (prefab != null)
            PrefabUtility.UnloadPrefabContents(root);
        else if (temporaryRoot)
            UnityEngine.Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
    }

    private static void SetupManagerObject(GameObject root, string outputFolder)
    {
        var leanLocalization = root.GetComponent<LeanLocalization>();
        if (leanLocalization == null)
            leanLocalization = root.AddComponent<LeanLocalization>();

        var manager = root.GetComponent<LocalizationManager>();
        if (manager == null)
            manager = root.AddComponent<LocalizationManager>();

        leanLocalization.DetectLanguage = LeanLocalization.DetectType.None;
        leanLocalization.SaveLoad = LeanLocalization.SaveLoadType.None;
        leanLocalization.DefaultLanguage = LocalizationLanguageUtility.GetDefaultLeanLanguageName(GameLanguage.English);

        foreach (var language in LocalizationLanguageUtility.AllLanguages)
        {
            string languageName = LocalizationLanguageUtility.GetDefaultLeanLanguageName(language);
            string csvPath = $"{outputFolder}/{languageName}.csv";
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(csvPath);
            if (source == null)
                continue;

            Transform child = root.transform.Find(languageName);
            if (child == null)
            {
                var childGo = new GameObject(languageName);
                childGo.transform.SetParent(root.transform, false);
                child = childGo.transform;
            }

            var leanLanguage = child.GetComponent<LeanLanguage>();
            if (leanLanguage == null)
                leanLanguage = child.gameObject.AddComponent<LeanLanguage>();

            leanLanguage.TranslationCode = LocalizationLanguageUtility.GetDefaultCultureCode(language);

            var csv = child.GetComponent<LeanLanguageCSV>();
            if (csv == null)
                csv = child.gameObject.AddComponent<LeanLanguageCSV>();

            csv.Language = languageName;
            csv.Format = LeanLanguageCSV.FormatType.Comma;
            csv.Cache = LeanLanguageCSV.CacheType.LoadImmediately;
            csv.Source = source;
            csv.LoadFromSource();

            EditorUtility.SetDirty(child.gameObject);
        }

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(leanLocalization);
        EditorUtility.SetDirty(manager);
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
            return;

        string[] parts = assetFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (AssetDatabase.IsValidFolder(next) == false)
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}

public class LocalizationDataUpdaterWindow : EditorWindow
{
    private string url;
    private string outputFolder;

    public static void Open()
    {
        GetWindow<LocalizationDataUpdaterWindow>("Localization Data");
    }

    private void OnEnable()
    {
        url = LocalizationDataUpdater.GetSavedUrl();
        outputFolder = LocalizationDataUpdater.GetSavedOutputFolder();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Google Sheet CSV", EditorStyles.boldLabel);
        url = EditorGUILayout.TextField("CSV URL", url);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        EditorGUILayout.Space();

        if (GUILayout.Button("Save Settings"))
            LocalizationDataUpdater.SaveSettings(url, outputFolder);

        if (GUILayout.Button("Download And Update"))
        {
            LocalizationDataUpdater.SaveSettings(url, outputFolder);
            LocalizationDataUpdater.DownloadAndWrite(url, outputFolder);
        }
    }
}

internal static class LocalizationCsvParser
{
    public static List<List<string>> Parse(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                row.Add(cell.ToString());
                cell.Length = 0;
            }
            else if (c == '\r' || c == '\n')
            {
                row.Add(cell.ToString());
                cell.Length = 0;
                AddRow(rows, row);
                row = new List<string>();

                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                    i++;
            }
            else
            {
                cell.Append(c);
            }
        }

        row.Add(cell.ToString());
        AddRow(rows, row);

        return rows;
    }

    private static void AddRow(List<List<string>> rows, List<string> row)
    {
        for (int i = 0; i < row.Count; i++)
        {
            if (string.IsNullOrEmpty(row[i]) == false)
            {
                rows.Add(row);
                return;
            }
        }
    }
}
#endif
