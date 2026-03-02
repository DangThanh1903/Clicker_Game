using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Pickaxe))]
public class PickaxeEditor : Editor
{
    private SerializedProperty itemNameProp;
    private SerializedProperty descriptionProp;
    private SerializedProperty iconProp;
    private SerializedProperty rarityProp;
    private SerializedProperty modifiersProp;
    private SerializedProperty passiveBuffsProp;
    private SerializedProperty currentStateProp;
    private SerializedProperty holdBeamVfxPrefabProp;
    private SerializedProperty holdBeamStartOffsetProp;
    private SerializedProperty idlePetVisualPrefabProp;
    private SerializedProperty idlePetSpawnLocalEulerProp;

    private void OnEnable()
    {
        itemNameProp = serializedObject.FindProperty("itemName");
        descriptionProp = serializedObject.FindProperty("description");
        iconProp = serializedObject.FindProperty("icon");
        rarityProp = serializedObject.FindProperty("rarity");
        modifiersProp = serializedObject.FindProperty("modifiers");
        passiveBuffsProp = serializedObject.FindProperty("passiveBuffs");
        currentStateProp = serializedObject.FindProperty("currentState");
        holdBeamVfxPrefabProp = serializedObject.FindProperty("holdBeamVfxPrefab");
        holdBeamStartOffsetProp = serializedObject.FindProperty("holdBeamStartOffset");
        idlePetVisualPrefabProp = serializedObject.FindProperty("idlePetVisualPrefab");
        idlePetSpawnLocalEulerProp = serializedObject.FindProperty("idlePetSpawnLocalEuler");
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
        DrawProp(modifiersProp);
        DrawProp(passiveBuffsProp);

        EditorGUILayout.Space();
        DrawProp(currentStateProp);
        DrawStateVisualSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawStateVisualSection()
    {
        if (currentStateProp == null)
            return;

        var state = (PickaxeType)currentStateProp.enumValueIndex;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("State Visuals", EditorStyles.boldLabel);

        switch (state)
        {
            case PickaxeType.Hold:
                DrawProp(holdBeamVfxPrefabProp, "Beam Prefab");
                DrawProp(holdBeamStartOffsetProp, "Beam Start Offset");
                break;
            case PickaxeType.Idle:
                DrawProp(idlePetVisualPrefabProp, "Pet Prefab");
                DrawProp(idlePetSpawnLocalEulerProp, "Pet Spawn Local Euler");
                break;
            default:
                EditorGUILayout.HelpBox("Normal state has no extra visual fields.", MessageType.Info);
                break;
        }
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
