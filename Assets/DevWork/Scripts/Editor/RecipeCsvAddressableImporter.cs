#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

[CustomEditor(typeof(RecipeDatabase))]
public class RecipeCsvAddressableImporter : Editor
{
    private const int ExpectedColumns = 10; // Result,ResultQty,Ing0,Qty0,Ing1,Qty1,Ing2,Qty2,Ing3,Qty3

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var db = (RecipeDatabase)target;

        GUILayout.Space(8);
        if (GUILayout.Button("Load Recipes from CSV (Addressables)"))
        {
            string path = EditorUtility.OpenFilePanel("Select Recipes CSV", "", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                LoadFromCSV_Addressables(db, path);
            }
        }
    }

    private static void LoadFromCSV_Addressables(RecipeDatabase db, string path)
    {
        var lines = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
            .ToList();

        if (lines.Count == 0)
        {
            EditorUtility.DisplayDialog("Import Recipes", "CSV is empty.", "OK");
            return;
        }

        // Parse header (loose check)
        var header = ParseCsvLine(lines[0]);
        if (header.Count < ExpectedColumns ||
            !header[0].Trim().Equals("Result", StringComparison.OrdinalIgnoreCase) ||
            !header[1].Trim().Equals("ResultQty", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Import Recipes",
                "Header must start with: Result,ResultQty,... (10 columns total).",
                "OK");
            return;
        }

        // Addressables settings
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            EditorUtility.DisplayDialog("Import Recipes",
                "AddressableAssetSettings not found. Please initialize Addressables (Window → Asset Management → Addressables).",
                "OK");
            return;
        }

        // Build a quick lookup (address → GUID) and (name → GUID)
        var (byAddress, byName) = BuildAddressablesItemLookup(settings);

        // Access to the private 'recipes' list via reflection (matches your ScriptableObject code)
        var recipesField = typeof(RecipeDatabase).GetField("recipes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (recipesField == null)
        {
            Debug.LogError("❌ Field 'recipes' not found on RecipeDatabase (expected private List<Recipe>).");
            return;
        }

        var currentList = (List<RecipeDatabase.Recipe>)recipesField.GetValue(db);
        bool replace = EditorUtility.DisplayDialog("Import Mode",
            "Replace existing recipes (Yes) or Append (No)?",
            "Replace", "Append");
        if (replace) currentList.Clear();

        int created = 0;
        Undo.RecordObject(db, "Import Recipes from CSV");

        // Rows
        for (int rowIndex = 1; rowIndex < lines.Count; rowIndex++)
        {
            var raw = lines[rowIndex];
            var cols = ParseCsvLine(raw);
            if (cols.Count == 0) continue;

            if (cols.Count < ExpectedColumns)
            {
                Debug.LogWarning($"⛔ Skipping line {rowIndex + 1}: expected {ExpectedColumns} columns, got {cols.Count}");
                continue;
            }

            try
            {
                string resultKey = cols[0].Trim(); // Addressable address (preferred) or Item name
                int resultQty = ParseInt(cols[1], 1, rowIndex + 1, "ResultQty");

                // Ingredients
                var ingredients = new List<InventoryItem>(4);
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
                    var item = ResolveItemViaAddressables(key, byAddress, byName);
                    if (item == null)
                    {
                        Debug.LogWarning($"⚠️ Line {rowIndex + 1}: Item not found for key '{key}' (address or name). Leaving slot empty.");
                        ingredients.Add(new InventoryItem(null, 0));
                    }
                    else
                    {
                        ingredients.Add(new InventoryItem(item, qty));
                    }
                }

                // Result
                if (string.IsNullOrEmpty(resultKey))
                    throw new FormatException($"Line {rowIndex + 1}: Result is empty.");

                var resultItem = ResolveItemViaAddressables(resultKey, byAddress, byName);
                if (resultItem == null)
                    throw new FormatException($"Line {rowIndex + 1}: Result item '{resultKey}' not found in Addressables (by address or by name).");

                var resultInv = new InventoryItem(resultItem, Math.Max(1, resultQty));

                // Create recipe entry
                var recipe = new RecipeDatabase.Recipe
                {
                    ingredients = RecipeDatabase.NormalizeIngredients(ingredients),
                    result = resultInv
                };

                currentList.Add(recipe);
                created++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ Failed to parse line {rowIndex + 1}:\n{raw}\n{ex.Message}");
            }
        }

        // Persist & init lookups
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        db.Initialize();

        Debug.Log($"✅ Imported {created} recipe(s) from CSV (Addressables).");
    }

    // ------------------------ Addressables helpers ------------------------

    private static (Dictionary<string, string> byAddress, Dictionary<string, List<string>> byName)
        BuildAddressablesItemLookup(AddressableAssetSettings settings)
    {
        var byAddress = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // address -> GUID
        var byName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); // asset name -> GUIDs

        foreach (var group in settings.groups.Where(g => g != null))
        {
            foreach (var e in group.entries.Where(e => e != null))
            {
                // Filter: only entries whose main asset is an Item scriptable object
                string assetPath = AssetDatabase.GUIDToAssetPath(e.guid);
                if (string.IsNullOrEmpty(assetPath)) continue;

                var item = AssetDatabase.LoadAssetAtPath<Item>(assetPath);
                if (item == null) continue; // not an Item

                // address → guid
                if (!string.IsNullOrEmpty(e.address))
                {
                    byAddress[e.address] = e.guid;
                }

                // name → guid list (support duplicates; warn later if multiple)
                if (!byName.TryGetValue(item.name, out var list))
                {
                    list = new List<string>();
                    byName[item.name] = list;
                }
                list.Add(e.guid);
            }
        }

        return (byAddress, byName);
    }

    private static Item ResolveItemViaAddressables(string key,
        Dictionary<string, string> byAddress,
        Dictionary<string, List<string>> byName)
    {
        // 1) Try by Addressables address
        if (byAddress.TryGetValue(key, out var guidByAddr))
        {
            string path = AssetDatabase.GUIDToAssetPath(guidByAddr);
            var item = AssetDatabase.LoadAssetAtPath<Item>(path);
            if (item != null) return item;
        }

        // 2) Fallback by Item asset name
        if (byName.TryGetValue(key, out var guidsByName) && guidsByName.Count > 0)
        {
            if (guidsByName.Count > 1)
            {
                Debug.LogWarning($"🔎 Multiple addressable Items named '{key}'. Using the first found.");
            }
            string path = AssetDatabase.GUIDToAssetPath(guidsByName[0]);
            var item = AssetDatabase.LoadAssetAtPath<Item>(path);
            if (item != null) return item;
        }

        return null;
    }

    // ------------------------ CSV helpers ------------------------

    private static int ParseInt(string s, int fallback, int lineNo, string field)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            return v;
        throw new FormatException($"Line {lineNo}: '{field}' must be an integer (got '{s}').");
    }

    /// <summary>
    /// Simple CSV line parser: supports quoted fields, embedded commas, and double-quote escaping ("").
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (line == null) return result;

        bool inQuotes = false;
        var cur = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '\"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        cur.Append('\"');
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
                else if (c == '\"')
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
