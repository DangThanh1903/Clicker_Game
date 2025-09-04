// #if UNITY_EDITOR
// using UnityEditor;
// using UnityEngine;
// using UnityEditor.AddressableAssets;
// using UnityEditor.AddressableAssets.Settings;
// using UnityEditor.AddressableAssets.Settings.GroupSchemas;

// [CustomEditor(typeof(Item), true)]
// public class ItemRenamerEditor : Editor
// {
//     private int itemGroupIndex = 2;
//     public override void OnInspectorGUI()
//     {
//         base.OnInspectorGUI();

//         Item item = (Item)target;

//         if (GUILayout.Button("Rename Asset & Set Addressable ID to itemName"))
//         {
//             RenameAssetAndConfigureAddressables(item);
//         }
//     }

//     private void RenameAssetAndConfigureAddressables(Item item)
//     {
//         string path = AssetDatabase.GetAssetPath(item);
//         if (string.IsNullOrEmpty(path)) return;

//         string newName = item.itemName;
//         if (string.IsNullOrWhiteSpace(newName))
//         {
//             Debug.LogWarning("itemName is empty. Cannot rename.");
//             return;
//         }

//         // Rename the .asset file
//         AssetDatabase.RenameAsset(path, newName);
//         AssetDatabase.SaveAssets();
//         Debug.Log($"Renamed asset to: {newName}");

//         // Get Addressable settings
//         var settings = AddressableAssetSettingsDefaultObject.Settings;
//         if (settings == null)
//         {
//             Debug.LogError("AddressableAssetSettings not found.");
//             return;
//         }

//         // Get group at index 2 (Items group)
//         if (settings.groups.Count <= itemGroupIndex)
//         {
//             Debug.LogError("Group index 1 (Items) does not exist.");
//             return;
//         }

//         AddressableAssetGroup itemsGroup = settings.groups[itemGroupIndex];
//         if (itemsGroup == null)
//         {
//             Debug.LogError("Items group is null.");
//             return;
//         }

//         // Add or move asset to group
//         string guid = AssetDatabase.AssetPathToGUID(path);
//         var entry = settings.CreateOrMoveEntry(guid, itemsGroup);
//         entry.SetAddress(newName);

//         settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
//         AssetDatabase.SaveAssets();

//         Debug.Log($"Set Addressable ID to: {newName} in group: {itemsGroup.Name}");

//         string pngName = item.itemName;
//         string[] pngGuids = AssetDatabase.FindAssets($"t:Texture2D", new[] { "Assets/DevWork/Graphics/UI-UX/Items/Materials" });

//         bool found = false;

//         foreach (string pngGuid in pngGuids)
//         {
//             string pngPath = AssetDatabase.GUIDToAssetPath(pngGuid);
//             string fileName = System.IO.Path.GetFileNameWithoutExtension(pngPath);

//             if (fileName == pngName)
//             {
//                 Sprite foundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
//                 if (foundSprite != null)
//                 {
//                     Debug.Log($"✅ Found exact match PNG at: {pngPath}");
//                     item.icon = foundSprite;
//                     EditorUtility.SetDirty(item);
//                 }
//                 else
//                 {
//                     Debug.LogWarning("❌ PNG file was found but could not be loaded.");
//                 }

//                 found = true;
//                 break;
//             }
//         }

//         if (!found)
//         {
//             Debug.LogWarning($"❌ No exact PNG match found for '{pngName}' in UI-UX/Items folder.");
//         }


//     }
// }
// #endif
