#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class Item3DReferenceUpdater
{
    private const string ItemFolder = "Assets/DevWork/Scriptable Objects/Items";
    private const string MeshFolder = "Assets/DevWork/Graphics/3D models/Items/Meshes";
    private const string MaterialFolder = "Assets/DevWork/Graphics/3D models/Items/Materials";
    private const string SideMaterialPath = "Assets/DevWork/Graphics/3D models/Items/Materials/Item3D_SideBlack.mat";

    [MenuItem("Tools/Items/Update Item 3D References")]
    public static void UpdateItemReferences()
    {
        Material sideMaterial = AssetDatabase.LoadAssetAtPath<Material>(SideMaterialPath);
        string[] itemGuids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { ItemFolder });

        int updated = 0;
        int missing = 0;

        foreach (string itemGuid in itemGuids)
        {
            string itemPath = AssetDatabase.GUIDToAssetPath(itemGuid);
            Item item = AssetDatabase.LoadAssetAtPath<Item>(itemPath);
            if (item == null || item.Type == ItemType.None)
                continue;

            string safeName = MakeSafeFileName(string.IsNullOrEmpty(item.itemName) ? item.name : item.itemName);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshFolder}/{safeName}_3D.asset");
            Material frontMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{safeName}_Front.mat");

            if (mesh == null || frontMaterial == null)
            {
                missing++;
                continue;
            }

            item.worldMesh = mesh;
            item.worldFrontMaterial = frontMaterial;
            item.worldSideMaterial = sideMaterial;
            EditorUtility.SetDirty(item);
            updated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Item3DReferenceUpdater] Updated {updated} item 3D reference(s). Missing visual assets for {missing} item(s).");
    }

    private static string MakeSafeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');

        return value;
    }
}
#endif
