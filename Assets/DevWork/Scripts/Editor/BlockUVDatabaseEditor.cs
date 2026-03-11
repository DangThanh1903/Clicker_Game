#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BlockUVDatabase))]
public class BlockUVDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BlockUVDatabase db = (BlockUVDatabase)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Load From CSV"))
        {
            string path = EditorUtility.OpenFilePanel("Load Block CSV", "", "csv");
            if (!string.IsNullOrEmpty(path))
            {
                LoadFromCsv(db, path);
            }
        }
    }

    private static void LoadFromCsv(BlockUVDatabase db, string path)
    {
        List<string> lines = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("#"))
            .ToList();

        if (lines.Count <= 1)
        {
            Debug.LogWarning("[BlockUVDatabaseEditor] CSV is empty or missing data rows.");
            return;
        }

        Undo.RecordObject(db, "Import Block UV CSV");
        db.blocks.Clear();

        int imported = 0;
        foreach (string line in lines.Skip(1))
        {
            string[] tokens = line.Split(',').Select(t => t.Trim()).ToArray();
            if (tokens.Length < 9)
            {
                Debug.LogWarning($"[BlockUVDatabaseEditor] Skipping malformed row ({tokens.Length} columns): {line}");
                continue;
            }

            try
            {
                ParseOutlineVisual(
                    tokens.Length > 9 ? tokens[9] : string.Empty,
                    Color.black,
                    0f,
                    out Color outlineColor,
                    out float glowIntensity
                );

                BlockUVEntry entry = new BlockUVEntry
                {
                    blockName = tokens[0],
                    atlasIndex = int.Parse(tokens[1], CultureInfo.InvariantCulture),
                    health = int.Parse(tokens[2], CultureInfo.InvariantCulture),
                    BreakingSound = tokens[0] + "Breaking",
                    locationCondition = (BlockSpawnLocation)Enum.Parse(typeof(BlockSpawnLocation), tokens[3], true),
                    timeStateCondition = (TimeState)Enum.Parse(typeof(TimeState), tokens[4], true),
                    normalWeatherCondition = (NormalWeatherName)Enum.Parse(typeof(NormalWeatherName), tokens[5], true),
                    specialWeatherCondition = (SpecialWeatherName)Enum.Parse(typeof(SpecialWeatherName), tokens[6], true),
                    weight = float.Parse(tokens[7], CultureInfo.InvariantCulture),
                    drops = ParseDrops(tokens[8]),
                    outlineColor = outlineColor,
                    glowIntensity = glowIntensity
                };

                db.blocks.Add(entry);
                imported++;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BlockUVDatabaseEditor] Failed to parse row:\n{line}\n{ex.Message}");
            }
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BlockUVDatabaseEditor] Imported {imported} block rows from CSV.");
    }

    private static void ParseOutlineVisual(string raw, Color fallbackColor, float fallbackIntensity, out Color color, out float intensity)
    {
        color = fallbackColor;
        intensity = Mathf.Max(0f, fallbackIntensity);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        raw = raw.Trim();

        if (ColorUtility.TryParseHtmlString(raw, out Color htmlColor))
        {
            color = htmlColor;
            return;
        }

        // New CSV format: R:G:B:A:I (RGBA in 0-255, intensity as float)
        string[] rgba255 = raw.Split(':');
        if (TryParseRgba255AndIntensity(rgba255, out Color color255, out float glowIntensity))
        {
            color = color255;
            intensity = Mathf.Max(0f, glowIntensity);
            return;
        }

        // Legacy format: R:G:B:A (0-255)
        if (TryParseRgba255(rgba255, out color255))
        {
            color = color255;
            intensity = 1f;
            return;
        }

        string[] parts = raw.Split('|');
        if (parts.Length != 3 && parts.Length != 4)
        {
            return;
        }

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ||
            !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g) ||
            !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
        {
            return;
        }

        float a = 1f;
        if (parts.Length == 4 &&
            !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out a))
        {
            return;
        }

        color = new Color(r, g, b, a);
        intensity = 1f;
    }

    private static bool TryParseRgba255AndIntensity(string[] parts, out Color color, out float intensity)
    {
        color = Color.black;
        intensity = 0f;

        if (parts == null || parts.Length != 5)
            return false;

        if (!TryParseByteComponent(parts[0], out float r) ||
            !TryParseByteComponent(parts[1], out float g) ||
            !TryParseByteComponent(parts[2], out float b) ||
            !TryParseByteComponent(parts[3], out float a))
        {
            return false;
        }

        if (!float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float rawIntensity))
            return false;

        color = new Color(r, g, b, a);
        intensity = Mathf.Max(0f, rawIntensity);
        return true;
    }

    private static bool TryParseRgba255(string[] parts, out Color color)
    {
        color = Color.black;
        if (parts == null || (parts.Length != 3 && parts.Length != 4))
        {
            return false;
        }

        if (!TryParseByteComponent(parts[0], out float r) ||
            !TryParseByteComponent(parts[1], out float g) ||
            !TryParseByteComponent(parts[2], out float b))
        {
            return false;
        }

        float a = 1f;
        if (parts.Length == 4 && !TryParseByteComponent(parts[3], out a))
        {
            return false;
        }

        color = new Color(r, g, b, a);
        return true;
    }

    private static bool TryParseByteComponent(string raw, out float value01)
    {
        value01 = 0f;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value255))
        {
            return false;
        }

        value255 = Mathf.Clamp(value255, 0, 255);
        value01 = value255 / 255f;
        return true;
    }

    private static List<ItemDrop> ParseDrops(string dropData)
    {
        List<ItemDrop> drops = new List<ItemDrop>();
        if (string.IsNullOrWhiteSpace(dropData))
        {
            return drops;
        }

        Dictionary<string, Item> itemLookup = BuildItemLookup();

        string[] dropEntries = dropData.Split(';');
        foreach (string drop in dropEntries)
        {
            string[] parts = drop.Split(':');
            if (parts.Length != 4)
            {
                Debug.LogWarning($"[BlockUVDatabaseEditor] Invalid drop format: {drop}");
                continue;
            }

            string itemName = parts[0].Trim();
            if (!itemLookup.TryGetValue(itemName, out Item item) || item == null)
            {
                Debug.LogWarning($"[BlockUVDatabaseEditor] Item not found: {itemName}");
                continue;
            }

            drops.Add(new ItemDrop
            {
                item = item,
                minAmount = int.Parse(parts[1], CultureInfo.InvariantCulture),
                maxAmount = int.Parse(parts[2], CultureInfo.InvariantCulture),
                dropChance = float.Parse(parts[3], CultureInfo.InvariantCulture)
            });
        }

        return drops;
    }

    private static Dictionary<string, Item> BuildItemLookup()
    {
        Dictionary<string, Item> lookup = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets("t:Item");

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            Item item = AssetDatabase.LoadAssetAtPath<Item>(assetPath);
            if (item == null)
            {
                continue;
            }

            if (!lookup.ContainsKey(item.name))
            {
                lookup[item.name] = item;
            }

            if (!string.IsNullOrEmpty(item.itemName) && !lookup.ContainsKey(item.itemName))
            {
                lookup[item.itemName] = item;
            }
        }

        return lookup;
    }
}
#endif
