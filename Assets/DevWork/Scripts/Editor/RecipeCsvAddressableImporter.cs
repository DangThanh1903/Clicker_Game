#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

[CustomEditor(typeof(RecipeDatabase))]
public class RecipeCsvAddressableImporter : Editor
{
    private const int ExpectedColumns = 10; // Result,ResultQty,Ing0,Qty0,Ing1,Qty1,Ing2,Qty2,Ing3,Qty3
    private const string RecipeSheetId = "1MS8PZC-8mWTw9KHIdOyyE7x8BtiaHfdgqY3KOXAbJRM";
    private const string RecipeSheetGid = "1490325417";
    private const string RecipeSheetName = "Recipe";
    private const string ToolMenuPath = "Tools/ARealClickerGame/Load Recipes From Google Sheet (Recipe)";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RecipeDatabase db = (RecipeDatabase)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Load Recipes from Google Sheet (Recipe)"))
        {
            LoadFromGoogleSheet(db);
        }

        if (GUILayout.Button("Load Recipes from CSV (Addressables)"))
        {
            string path = EditorUtility.OpenFilePanel("Select Recipes CSV", "", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                LoadFromCsvFile(db, path);
            }
        }
    }

    [MenuItem(ToolMenuPath)]
    private static void LoadSelectedOrProjectRecipeDatabaseFromGoogleSheet()
    {
        RecipeDatabase db = ResolveTargetDatabase();
        if (db == null)
        {
            EditorUtility.DisplayDialog(
                "Load Recipes",
                "No RecipeDatabase asset found. Select one in Project view or create a Recipe Database asset first.",
                "OK");
            return;
        }

        LoadFromGoogleSheet(db);
    }

    [MenuItem(ToolMenuPath, true)]
    private static bool ValidateLoadSelectedOrProjectRecipeDatabaseFromGoogleSheet()
    {
        return ResolveTargetDatabase(showWarnings: false) != null;
    }

    private static RecipeDatabase ResolveTargetDatabase(bool showWarnings = true)
    {
        if (Selection.activeObject is RecipeDatabase selectedDb)
        {
            return selectedDb;
        }

        string[] guids = AssetDatabase.FindAssets("t:RecipeDatabase");
        if (guids == null || guids.Length == 0)
        {
            return null;
        }

        if (guids.Length > 1 && showWarnings)
        {
            Debug.LogWarning(
                $"[RecipeCsvAddressableImporter] Multiple RecipeDatabase assets found. Using first result: {AssetDatabase.GUIDToAssetPath(guids[0])}. Select a specific database asset before running the tool to choose another.");
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<RecipeDatabase>(path);
    }

    private static void LoadFromGoogleSheet(RecipeDatabase db)
    {
        string url = BuildGoogleSheetCsvUrl();
        try
        {
            using WebClient client = new WebClient { Encoding = Encoding.UTF8 };
            string csv = client.DownloadString(url);
            ImportFromCsvText(db, csv, $"Google Sheet tab '{RecipeSheetName}'");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RecipeCsvAddressableImporter] Failed to load Google Sheet tab '{RecipeSheetName}'.\nURL: {url}\n{ex}");
        }
    }

    private static string BuildGoogleSheetCsvUrl()
    {
        return $"https://docs.google.com/spreadsheets/d/{RecipeSheetId}/export?format=csv&gid={RecipeSheetGid}";
    }

    private static void LoadFromCsvFile(RecipeDatabase db, string path)
    {
        string csv = File.ReadAllText(path);
        ImportFromCsvText(db, csv, Path.GetFileName(path));
    }

    private static void ImportFromCsvText(RecipeDatabase db, string csv, string sourceName)
    {
        List<string> lines = SplitCsvLines(csv)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
            .ToList();

        if (lines.Count == 0)
        {
            EditorUtility.DisplayDialog("Import Recipes", $"{sourceName} is empty.", "OK");
            return;
        }

        List<string> header = ParseCsvLine(lines[0]);
        if (header.Count < ExpectedColumns ||
            !header[0].Trim().Equals("Result", StringComparison.OrdinalIgnoreCase) ||
            !header[1].Trim().Equals("ResultQty", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Import Recipes",
                $"{sourceName} header must start with: Result,ResultQty,... (10 columns total).",
                "OK");
            return;
        }

        var recipesField = typeof(RecipeDatabase).GetField(
            "recipes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (recipesField == null)
        {
            Debug.LogError("[RecipeCsvAddressableImporter] Field 'recipes' not found on RecipeDatabase.");
            return;
        }

        List<Recipe> currentList = recipesField.GetValue(db) as List<Recipe>;
        if (currentList == null)
        {
            currentList = new List<Recipe>();
            recipesField.SetValue(db, currentList);
        }

        bool replace = EditorUtility.DisplayDialog(
            "Import Mode",
            "Replace existing recipes (Yes) or Append (No)?",
            "Replace",
            "Append");

        Undo.RecordObject(db, "Import Recipes from CSV");

        if (replace)
        {
            currentList.Clear();
        }

        BuildItemLookup(out Dictionary<string, Item> byAddress, out Dictionary<string, List<Item>> byName);

        int created = 0;

        for (int rowIndex = 1; rowIndex < lines.Count; rowIndex++)
        {
            string raw = lines[rowIndex];
            List<string> cols = ParseCsvLine(raw);
            if (cols.Count == 0 || IsBlankRow(cols))
            {
                continue;
            }

            if (cols.Count < ExpectedColumns)
            {
                if (IsPlaceholderRow(cols))
                {
                    continue;
                }

                Debug.LogWarning($"[RecipeCsvAddressableImporter] Skipping line {rowIndex + 1}: expected {ExpectedColumns} columns, got {cols.Count}.");
                continue;
            }

            try
            {
                string resultKey = cols[0].Trim();
                if (IsHeaderLikeRow(cols) || IsPlaceholderRow(cols))
                {
                    continue;
                }

                int resultQty = ParseInt(cols[1], 1, rowIndex + 1, "ResultQty");

                if (string.IsNullOrEmpty(resultKey))
                {
                    throw new FormatException($"Line {rowIndex + 1}: Result is empty.");
                }

                Item resultItem = ResolveItem(resultKey, byAddress, byName);
                if (resultItem == null)
                {
                    throw new FormatException(
                        $"Line {rowIndex + 1}: Result item '{resultKey}' not found (address or name).");
                }

                List<InventoryItem> ingredients = new List<InventoryItem>(4);
                for (int i = 0; i < 4; i++)
                {
                    string key = cols[2 + i * 2].Trim();
                    string qtyStr = cols[3 + i * 2].Trim();

                    if (string.IsNullOrEmpty(key))
                    {
                        ingredients.Add(new InventoryItem(null, 0));
                        continue;
                    }

                    int qty = ParseInt(qtyStr, 1, rowIndex + 1, $"Qty{i}");
                    Item item = ResolveItem(key, byAddress, byName);

                    if (item == null)
                    {
                        Debug.LogWarning(
                            $"[RecipeCsvAddressableImporter] Line {rowIndex + 1}: Ingredient '{key}' not found. Leaving slot empty.");
                        ingredients.Add(new InventoryItem(null, 0));
                    }
                    else
                    {
                        ingredients.Add(new InventoryItem(item, qty));
                    }
                }

                Recipe recipe = new Recipe
                {
                    ingredients = RecipeDatabase.NormalizeIngredients(ingredients),
                    result = new InventoryItem(resultItem, Math.Max(1, resultQty))
                };

                currentList.Add(recipe);
                created++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RecipeCsvAddressableImporter] Failed to parse line {rowIndex + 1}:\n{raw}\n{ex.Message}");
            }
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        db.Initialize();

        DevLog.Log($"[RecipeCsvAddressableImporter] Imported {created} recipe(s) from {sourceName}.");
    }

    private static void BuildItemLookup(
        out Dictionary<string, Item> byAddress,
        out Dictionary<string, List<Item>> byName)
    {
        byAddress = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        byName = new Dictionary<string, List<Item>>(StringComparer.OrdinalIgnoreCase);

        string[] itemGuids = AssetDatabase.FindAssets("t:Item");
        Dictionary<string, Item> itemsByGuid = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);

        foreach (string guid in itemGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Item item = AssetDatabase.LoadAssetAtPath<Item>(assetPath);
            if (item == null)
            {
                continue;
            }

            itemsByGuid[guid] = item;

            AddByName(byName, item.name, item);
            if (!string.IsNullOrWhiteSpace(item.itemName))
            {
                AddByName(byName, item.itemName, item);
            }
        }

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            return;
        }

        foreach (AddressableAssetGroup group in settings.groups.Where(g => g != null))
        {
            foreach (AddressableAssetEntry entry in group.entries.Where(e => e != null))
            {
                if (string.IsNullOrWhiteSpace(entry.address))
                {
                    continue;
                }

                if (!itemsByGuid.TryGetValue(entry.guid, out Item item) || item == null)
                {
                    continue;
                }

                byAddress[entry.address] = item;
            }
        }
    }

    private static void AddByName(Dictionary<string, List<Item>> byName, string key, Item item)
    {
        if (string.IsNullOrWhiteSpace(key) || item == null)
        {
            return;
        }

        if (!byName.TryGetValue(key, out List<Item> list))
        {
            list = new List<Item>();
            byName[key] = list;
        }

        if (!list.Contains(item))
        {
            list.Add(item);
        }
    }

    private static Item ResolveItem(
        string key,
        Dictionary<string, Item> byAddress,
        Dictionary<string, List<Item>> byName)
    {
        if (byAddress.TryGetValue(key, out Item byAddr) && byAddr != null)
        {
            return byAddr;
        }

        if (byName.TryGetValue(key, out List<Item> byNameList) && byNameList.Count > 0)
        {
            if (byNameList.Count > 1)
            {
                Debug.LogWarning($"[RecipeCsvAddressableImporter] Multiple items named '{key}'. Using the first one.");
            }

            return byNameList[0];
        }

        return null;
    }

    private static int ParseInt(string s, int fallback, int lineNo, string field)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return fallback;
        }

        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
        {
            return v;
        }

        throw new FormatException($"Line {lineNo}: '{field}' must be an integer (got '{s}').");
    }

    private static bool IsBlankRow(List<string> cols)
    {
        return cols == null || cols.All(string.IsNullOrWhiteSpace);
    }

    private static bool IsHeaderLikeRow(List<string> cols)
    {
        return cols.Count > 1 &&
               cols[0].Trim().Equals("Result", StringComparison.OrdinalIgnoreCase) &&
               cols[1].Trim().Equals("ResultQty", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlaceholderRow(List<string> cols)
    {
        if (cols == null || cols.Count == 0)
        {
            return false;
        }

        return cols[0].Trim().Equals("Result", StringComparison.OrdinalIgnoreCase) &&
               cols.Skip(1).All(string.IsNullOrWhiteSpace);
    }

    private static List<string> SplitCsvLines(string csv)
    {
        List<string> lines = new List<string>();
        if (string.IsNullOrEmpty(csv))
        {
            return lines;
        }

        StringBuilder line = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    line.Append(c);
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                line.Append(c);
                continue;
            }

            if (!inQuotes && (c == '\n' || c == '\r'))
            {
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                {
                    i++;
                }

                lines.Add(line.ToString());
                line.Clear();
                continue;
            }

            line.Append(c);
        }

        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }

        return lines;
    }

    private static List<string> ParseCsvLine(string line)
    {
        List<string> result = new List<string>();
        if (line == null)
        {
            return result;
        }

        bool inQuotes = false;
        System.Text.StringBuilder cur = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        cur.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cur.Append(c);
                }
            }
            else
            {
                if (c == ',')
                {
                    result.Add(cur.ToString());
                    cur.Length = 0;
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else
                {
                    cur.Append(c);
                }
            }
        }

        result.Add(cur.ToString());
        return result;
    }
}
#endif

