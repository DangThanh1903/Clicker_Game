using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Pickaxe))]
public class PickaxeEditor : Editor
{
    private int itemGroupIndex = 2;

    private SerializedProperty itemNameProp;
    private SerializedProperty descriptionProp;
    private SerializedProperty iconProp;
    private SerializedProperty rarityProp;
    private SerializedProperty mergeableProp;
    private SerializedProperty mergeNextWeaponProp;
    private SerializedProperty modifiersProp;
    private SerializedProperty passiveBuffsProp;

    private void OnEnable()
    {
        itemNameProp = serializedObject.FindProperty("itemName");
        descriptionProp = serializedObject.FindProperty("description");
        iconProp = serializedObject.FindProperty("icon");
        rarityProp = serializedObject.FindProperty("rarity");
        mergeableProp = serializedObject.FindProperty("mergeable");
        mergeNextWeaponProp = serializedObject.FindProperty("mergeNextWeapon");
        modifiersProp = serializedObject.FindProperty("modifiers");
        passiveBuffsProp = serializedObject.FindProperty("passiveBuffs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((Pickaxe)target), typeof(Pickaxe), false);
        }

        EditorGUILayout.Space();
        DrawProp(itemNameProp);
        DrawProp(descriptionProp);
        DrawProp(iconProp);
        DrawProp(rarityProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Merge", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Preferred setup: configure merge chain in WeaponMergeDatabaseSO on InventoryController.\n" +
            "Fields below are legacy fallback.",
            MessageType.Info);
        DrawProp(mergeableProp, "Mergeable");
        if (mergeableProp != null && mergeableProp.boolValue)
            DrawProp(mergeNextWeaponProp, "Next Weapon");

        EditorGUILayout.Space();
        DrawProp(modifiersProp);
        DrawProp(passiveBuffsProp);

        EditorGUILayout.Space();
        if (GUILayout.Button("Rename Asset & Set Addressable ID to itemName"))
            ItemRenamerEditor.RenameAssetAndConfigureAddressables((Pickaxe)target, itemGroupIndex);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawProp(SerializedProperty prop, string label = null)
    {
        if (prop == null)
            return;

        if (string.IsNullOrEmpty(label))
            EditorGUILayout.PropertyField(prop, true);
        else
            EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
    }
}
