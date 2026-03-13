#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

public static class ItemLocalizationQuickMigrator
{
    const string NAMES_TABLE   = "Items_Names";
    const string DESCS_TABLE   = "Items_Descriptions";
    const string ASSET_FOLDER  = "Assets/Localization";
    const string SOURCE_LOCALE = "en";
    const string SEARCH_FOLDER = "Assets";

    [MenuItem("Tools/Localization/Migrate Items (LocalizedString)")]
    public static void Migrate()
    {
        var locales = new List<Locale>(LocalizationEditorSettings.GetLocales());
        if (locales.Count == 0)
        {
            // Táº¡o vĂ  thĂªm Locale asset
            var newLocale = Locale.CreateLocale(SOURCE_LOCALE);
            AssetDatabase.CreateAsset(newLocale, $"Assets/{SOURCE_LOCALE}.asset");
            LocalizationEditorSettings.AddLocale(newLocale);
            DevLog.Log($"[Localization] Added missing locale: {SOURCE_LOCALE}");
            locales = new List<Locale>(LocalizationEditorSettings.GetLocales());
        }

        var namesCol = GetOrCreateCollection(NAMES_TABLE, ASSET_FOLDER, locales);
        var descsCol = GetOrCreateCollection(DESCS_TABLE, ASSET_FOLDER, locales);

        var srcLocale = locales.Find(l => l.Identifier.Code == SOURCE_LOCALE) ?? locales[0];
        var namesEN = namesCol.GetTable(srcLocale.Identifier) as StringTable;
        var descsEN = descsCol.GetTable(srcLocale.Identifier) as StringTable;

        var guids = AssetDatabase.FindAssets("t:Item", new[] { SEARCH_FOLDER });
        int count = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (!obj) continue;

                var so = new SerializedObject(obj);
                var nameProp = so.FindProperty("itemName");     // LocalizedString
                var descProp = so.FindProperty("description");  // LocalizedString

                string baseKey = $"item_{Path.GetFileNameWithoutExtension(path).ToLower()}_{guid[..6]}";
                string nameKey = baseKey;
                string descKey = baseKey + "_desc";

                if (namesEN.GetEntry(nameKey) == null)
                    namesEN.AddEntry(nameKey, "");

                if (descsEN.GetEntry(descKey) == null)
                    descsEN.AddEntry(descKey, "");


                SetLocalizedString(nameProp, NAMES_TABLE, nameKey);
                SetLocalizedString(descProp, DESCS_TABLE, descKey);

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(obj);
                count++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        DevLog.Log($"Migrated {count} items. Export CSV (EN) vĂ  dá»‹ch VI sau Ä‘Ă³ Build Addressables.");
    }

    static StringTableCollection GetOrCreateCollection(string tableName, string assetDir, IList<Locale> locales)
    {
        var col = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if (col == null)
            col = LocalizationEditorSettings.CreateStringTableCollection(tableName, assetDir, locales);
        return col;
    }

    static void SetLocalizedString(SerializedProperty prop, string tableName, string key)
    {
        if (prop == null || prop.propertyType != SerializedPropertyType.Generic) return;
        var t = prop.FindPropertyRelative("m_TableReference");
        var e = prop.FindPropertyRelative("m_TableEntryReference");
        t.FindPropertyRelative("m_TableCollectionName").stringValue = tableName;
        t.FindPropertyRelative("m_TableCollectionNameGuid").stringValue = "";
        e.FindPropertyRelative("m_KeyId").longValue = 0;
        e.FindPropertyRelative("m_Key").stringValue = key;
    }
}
#endif

