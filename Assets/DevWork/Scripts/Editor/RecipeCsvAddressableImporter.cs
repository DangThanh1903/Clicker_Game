#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

[CustomEditor(typeof(RecipeDatabase))]
public class RecipeCsvAddressableImporter : Editor
{
    private const int ExpectedColumns = 10; // Result,ResultQty,Ing0,Qty0,Ing1,Qty1,Ing2,Qty2,Ing3,Qty3

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        RecipeDatabase db = (RecipeDatabase)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Load Recipes from CSV (Addressables)"))
        {
            string path = EditorUtility.OpenFilePanel("Select Recipes CSV", "", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                LoadFromCsvAddressables(db, path);
            }
        }
    }

    private static void LoadFromCsvAddressables(RecipeDatabase db, string path)
    {
        List<string> lines = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
            .ToList();

        if (lines.Count == 0)
        {
            EditorUtility.DisplayDialog("Import Recipes", "CSV is empty.", "OK");
            return;
        }

        List<string> header = ParseCsvLine(lines[0]);
        if (header.Count < ExpectedColumns ||
            !header[0].Trim().Equals("Result", StringComparison.OrdinalIgnoreCase) ||
            !header[1].Trim().Equals("ResultQty", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog(
                "Import Recipes",
                "Header must start with: Result,ResultQty,... (10 columns total).",
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
            if (cols.Count == 0)
            {
                continue;
            }

            if (cols.Count < ExpectedColumns)
            {
                Debug.LogWarning($"[RecipeCsvAddressableImporter] Skipping line {rowIndex + 1}: expected {ExpectedColumns} columns, got {cols.Count}.");
                continue;
            }

            try
            {
                string resultKey = cols[0].Trim();
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

        DevLog.Log($"[RecipeCsvAddressableImporter] Imported {created} recipe(s) from CSV.");
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

