#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(JournalDatabaseSO))]
public sealed class JournalGoogleSheetImporter : Editor
{
    private const string SpreadsheetId = "1MS8PZC-8mWTw9KHIdOyyE7x8BtiaHfdgqY3KOXAbJRM";
    private const string SheetGid = "28210190";
    private const string SheetName = "Journal";
    private const string ToolMenuPath = "Tools/ARealClickerGame/Load Journal From Google Sheet (Journal)";

    private static readonly string[] ExpectedHeader =
    {
        "BiomeId",
        "StepOrder",
        "StepId",
        "GoalType",
        "TargetId",
        "RequiredAmount",
        "Title",
        "Toast",
        "Rewards",
        "Unlocks"
    };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Load Journal from Google Sheet (Journal)"))
            LoadFromGoogleSheet((JournalDatabaseSO)target);
    }

    [MenuItem(ToolMenuPath)]
    private static void LoadSelectedOrProjectJournalDatabaseFromGoogleSheet()
    {
        JournalDatabaseSO db = ResolveTargetDatabase();
        if (db == null)
        {
            EditorUtility.DisplayDialog(
                "Load Journal",
                "No JournalDatabaseSO asset found. Select one in Project view or create a Journal Database asset first.",
                "OK");
            return;
        }

        LoadFromGoogleSheet(db);
    }

    [MenuItem(ToolMenuPath, true)]
    private static bool ValidateLoadSelectedOrProjectJournalDatabaseFromGoogleSheet()
    {
        return ResolveTargetDatabase(showWarnings: false) != null;
    }

    private static JournalDatabaseSO ResolveTargetDatabase(bool showWarnings = true)
    {
        if (Selection.activeObject is JournalDatabaseSO selectedDb)
            return selectedDb;

        string[] guids = AssetDatabase.FindAssets("t:JournalDatabaseSO");
        if (guids == null || guids.Length == 0)
            return null;

        if (guids.Length > 1 && showWarnings)
        {
            Debug.LogWarning(
                $"[JournalGoogleSheetImporter] Multiple JournalDatabaseSO assets found. Using first result: {AssetDatabase.GUIDToAssetPath(guids[0])}. Select a specific database asset before running the tool to choose another.");
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<JournalDatabaseSO>(path);
    }

    private static void LoadFromGoogleSheet(JournalDatabaseSO db)
    {
        string url = BuildGoogleSheetCsvUrl();
        try
        {
            using WebClient client = new WebClient { Encoding = Encoding.UTF8 };
            string csv = client.DownloadString(url);
            ImportFromCsvText(db, csv, $"Google Sheet tab '{SheetName}'");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[JournalGoogleSheetImporter] Failed to load Google Sheet tab '{SheetName}'.\nURL: {url}\n{ex}");
        }
    }

    private static string BuildGoogleSheetCsvUrl()
    {
        return $"https://docs.google.com/spreadsheets/d/{SpreadsheetId}/export?format=csv&gid={SheetGid}";
    }

    private static void ImportFromCsvText(JournalDatabaseSO db, string csv, string sourceName)
    {
        List<List<string>> rows = LocalizationCsvParser.Parse(csv);
        if (rows.Count <= 1)
        {
            EditorUtility.DisplayDialog("Import Journal", $"{sourceName} is empty or missing data rows.", "OK");
            return;
        }

        if (!HasExpectedHeader(rows[0]))
        {
            EditorUtility.DisplayDialog(
                "Import Journal",
                $"{sourceName} header must be:\n{string.Join(",", ExpectedHeader)}",
                "OK");
            return;
        }

        Dictionary<string, Item> itemLookup = BuildItemLookup();
        Dictionary<string, JournalBiomeData> biomeLookup = new(StringComparer.OrdinalIgnoreCase);
        List<JournalBiomeData> importedBiomes = new();
        int importedSteps = 0;

        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            List<string> row = rows[rowIndex];
            if (IsBlankRow(row))
                continue;

            try
            {
                string biomeId = GetCell(row, 0).Trim();
                int stepOrder = ParseRequiredInt(GetCell(row, 1), rowIndex + 1, "StepOrder");
                string stepId = GetCell(row, 2).Trim();
                JournalGoalType goalType = ParseGoalType(GetCell(row, 3), rowIndex + 1);
                string targetId = GetCell(row, 4).Trim();
                int requiredAmount = ParseRequiredInt(GetCell(row, 5), rowIndex + 1, "RequiredAmount");
                string title = GetCell(row, 6).Trim();
                string toast = GetCell(row, 7).Trim();
                string rewardsRaw = GetCell(row, 8).Trim();
                string unlocksRaw = GetCell(row, 9).Trim();

                if (string.IsNullOrWhiteSpace(biomeId))
                    throw new FormatException($"Line {rowIndex + 1}: BiomeId is empty.");
                if (string.IsNullOrWhiteSpace(stepId))
                    throw new FormatException($"Line {rowIndex + 1}: StepId is empty.");
                if (string.IsNullOrWhiteSpace(targetId) && goalType != JournalGoalType.CompleteBiome)
                    throw new FormatException($"Line {rowIndex + 1}: TargetId is empty.");

                if (!biomeLookup.TryGetValue(biomeId, out JournalBiomeData biome))
                {
                    biome = new JournalBiomeData
                    {
                        biomeId = biomeId,
                        title = biomeId,
                        order = importedBiomes.Count,
                        steps = new List<JournalStepData>()
                    };

                    biomeLookup.Add(biomeId, biome);
                    importedBiomes.Add(biome);
                }

                JournalStepData step = new JournalStepData
                {
                    id = stepId,
                    biomeId = biomeId,
                    order = stepOrder,
                    title = string.IsNullOrWhiteSpace(title) ? stepId : title,
                    description = string.Empty,
                    completionToast = toast,
                    goalType = goalType,
                    targetId = targetId,
                    requiredAmount = Mathf.Max(1, requiredAmount),
                    rewards = ParseRewards(rewardsRaw, itemLookup, rowIndex + 1),
                    unlocks = ParseUnlocks(unlocksRaw, rowIndex + 1)
                };

                biome.steps.Add(step);
                importedSteps++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[JournalGoogleSheetImporter] Failed to parse line {rowIndex + 1}: {ex.Message}");
            }
        }

        for (int i = 0; i < importedBiomes.Count; i++)
            importedBiomes[i].steps = importedBiomes[i].steps.OrderBy(step => step.order).ToList();

        Undo.RecordObject(db, "Import Journal from Google Sheet");
        db.biomes = importedBiomes;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        DevLog.Log($"[JournalGoogleSheetImporter] Imported {importedSteps} journal step(s) across {importedBiomes.Count} biome(s) from {sourceName}.");
    }

    private static bool HasExpectedHeader(List<string> header)
    {
        if (header == null || header.Count < ExpectedHeader.Length)
            return false;

        for (int i = 0; i < ExpectedHeader.Length; i++)
        {
            if (!string.Equals(NormalizeHeader(GetCell(header, i)), ExpectedHeader[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static List<JournalRewardData> ParseRewards(string raw, Dictionary<string, Item> itemLookup, int lineNumber)
    {
        List<JournalRewardData> rewards = new();
        foreach (string entry in SplitPipeEntries(raw))
        {
            string[] parts = entry.Split(':');
            if (parts.Length != 2)
                throw new FormatException($"Line {lineNumber}: reward '{entry}' must be in format Target:Amount.");

            string target = parts[0].Trim();
            int amount = ParseRequiredInt(parts[1], lineNumber, $"Reward amount for '{target}'");
            if (amount <= 0)
                continue;

            if (string.Equals(target, "Diamond", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target, "Diamonds", StringComparison.OrdinalIgnoreCase))
            {
                rewards.Add(new JournalRewardData { diamonds = amount });
                continue;
            }

            if (!itemLookup.TryGetValue(target, out Item item) || item == null)
                throw new FormatException($"Line {lineNumber}: reward item '{target}' not found.");

            rewards.Add(new JournalRewardData
            {
                item = item,
                amount = amount
            });
        }

        return rewards;
    }

    private static List<JournalUnlockData> ParseUnlocks(string raw, int lineNumber)
    {
        List<JournalUnlockData> unlocks = new();
        foreach (string entry in SplitPipeEntries(raw))
        {
            string[] parts = entry.Split(':');
            if (parts.Length != 2)
                throw new FormatException($"Line {lineNumber}: unlock '{entry}' must be in format Type:Target.");

            if (!Enum.TryParse(parts[0].Trim(), true, out JournalUnlockType unlockType))
                throw new FormatException($"Line {lineNumber}: unlock type '{parts[0]}' is invalid.");

            string targetId = parts[1].Trim();
            if (string.IsNullOrWhiteSpace(targetId))
                throw new FormatException($"Line {lineNumber}: unlock target is empty in '{entry}'.");

            unlocks.Add(new JournalUnlockData
            {
                type = unlockType,
                targetId = targetId
            });
        }

        return unlocks;
    }

    private static IEnumerable<string> SplitPipeEntries(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            yield break;

        string[] entries = raw.Split('|');
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i].Trim();
            if (!string.IsNullOrWhiteSpace(entry))
                yield return entry;
        }
    }

    private static JournalGoalType ParseGoalType(string raw, int lineNumber)
    {
        if (Enum.TryParse(raw?.Trim(), true, out JournalGoalType goalType))
            return goalType;

        throw new FormatException($"Line {lineNumber}: GoalType '{raw}' is invalid.");
    }

    private static int ParseRequiredInt(string raw, int lineNumber, string fieldName)
    {
        if (int.TryParse(raw?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            return value;

        throw new FormatException($"Line {lineNumber}: {fieldName} must be an integer (got '{raw}').");
    }

    private static string GetCell(List<string> row, int index)
    {
        if (row == null || index < 0 || index >= row.Count)
            return string.Empty;

        return row[index] ?? string.Empty;
    }

    private static string NormalizeHeader(string value)
    {
        return (value ?? string.Empty).Trim().TrimStart('\uFEFF');
    }

    private static bool IsBlankRow(List<string> row)
    {
        if (row == null)
            return true;

        for (int i = 0; i < row.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(row[i]))
                return false;
        }

        return true;
    }

    private static Dictionary<string, Item> BuildItemLookup()
    {
        Dictionary<string, Item> lookup = new(StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets("t:Item");

        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            Item item = AssetDatabase.LoadAssetAtPath<Item>(assetPath);
            if (item == null)
                continue;

            AddLookup(lookup, item.name, item);
            AddLookup(lookup, item.itemName, item);
        }

        return lookup;
    }

    private static void AddLookup(Dictionary<string, Item> lookup, string key, Item item)
    {
        if (string.IsNullOrWhiteSpace(key) || item == null || lookup.ContainsKey(key))
            return;

        lookup.Add(key, item);
    }
}
#endif
