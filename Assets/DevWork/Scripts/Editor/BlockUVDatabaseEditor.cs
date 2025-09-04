// #if UNITY_EDITOR
// using UnityEngine;
// using UnityEditor;
// using System;
// using System.Linq;
// using System.IO;
// using System.Collections.Generic;

// [CustomEditor(typeof(BlockUVDatabase))]
// public class BlockUVDatabaseEditor : Editor
// {
//     public override void OnInspectorGUI()
//     {
//         DrawDefaultInspector();

//         BlockUVDatabase db = (BlockUVDatabase)target;

//         if (GUILayout.Button("Load From CSV"))
//         {
//             string path = EditorUtility.OpenFilePanel("Load Block CSV", "", "csv");
//             if (!string.IsNullOrEmpty(path))
//             {
//                 LoadFromCSV(db, path);
//             }
//         }
//     }

//     private void LoadFromCSV(BlockUVDatabase db, string path)
//     {
//         var lines = File.ReadAllLines(path)
//             .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("#"))
//             .Skip(1) // Skip header
//             .ToList();

//         db.blocks.Clear();

//         foreach (var line in lines)
//         {
//             var tokens = line.Split(',').Select(t => t.Trim()).ToArray(); // <-- split by tab
//             if (tokens.Length < 9)
//             {
//                 Debug.LogWarning($"⛔ Skipping malformed line: {tokens.Length}");
//                 continue;
//             }

//             try
//             {
//                 var entry = new BlockUVEntry
//                 {
//                     blockName = tokens[0],
//                     atlasIndex = int.Parse(tokens[1]),
//                     health = int.Parse(tokens[2]),
//                     BreakingSound = tokens[0] + "Breaking",
//                     locationCondition = (BlockSpawnLocation)Enum.Parse(typeof(BlockSpawnLocation), tokens[3]),
//                     timeStateCondition = (TimeState)Enum.Parse(typeof(TimeState), tokens[4]),
//                     normalWeatherCondition = (NormalWeatherName)Enum.Parse(typeof(NormalWeatherName), tokens[5]),
//                     specialWeatherCondition = (SpecialWeatherName)Enum.Parse(typeof(SpecialWeatherName), tokens[6]),
//                     weight = float.Parse(tokens[7]),
//                     drops = ParseDrops(tokens[8])
//                 };  

//                 db.blocks.Add(entry);
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"❌ Failed to parse line: {line}\n{ex.Message}");
//             }
//         }

//         EditorUtility.SetDirty(db);
//         Debug.Log($"✅ Loaded {db.blocks.Count} blocks from CSV.");
//     }



//     private List<ItemDrop> ParseDrops(string dropData)
//     {
//         var drops = new List<ItemDrop>();

//         if (string.IsNullOrWhiteSpace(dropData))
//             return drops;

//         var dropEntries = dropData.Split(';');
//         foreach (var drop in dropEntries)
//         {
//             var parts = drop.Split(':');
//             if (parts.Length != 4)
//             {
//                 Debug.LogWarning($"⚠️ Invalid drop format: {drop}");
//                 continue;
//             }

//             string itemName = parts[0];
//             int min = int.Parse(parts[1]);
//             int max = int.Parse(parts[2]);
//             float chance = float.Parse(parts[3]);

//             // Find the Item asset by name (assumes unique names and single asset per name)
//             string[] guids = AssetDatabase.FindAssets($"t:Item");
//             string path = guids
//                 .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
//                 .FirstOrDefault(p =>
//                 {
//                     var asset = AssetDatabase.LoadAssetAtPath<Item>(p);
//                     return asset != null && asset.name == itemName;
//                 });

//             if (string.IsNullOrEmpty(path))
//             {
//                 Debug.LogWarning($"❌ Item not found: {itemName}");
//                 continue;
//             }

//             Item item = AssetDatabase.LoadAssetAtPath<Item>(path);
//             if (item == null)
//             {
//                 Debug.LogWarning($"❌ Failed to load Item asset at path: {path}");
//                 continue;
//             }

//             drops.Add(new ItemDrop
//             {
//                 item = item,
//                 minAmount = min,
//                 maxAmount = max,
//                 dropChance = chance
//             });
//         }

//         return drops;
//     }


// }
// #endif