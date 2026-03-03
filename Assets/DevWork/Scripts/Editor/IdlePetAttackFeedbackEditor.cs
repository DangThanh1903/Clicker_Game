using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(IdlePetAttackFeedback))]
public class IdlePetAttackFeedbackEditor : Editor
{
    private SerializedProperty animatorProp;
    private SerializedProperty lookRootProp;
    private SerializedProperty tweenRootProp;
    private SerializedProperty visualModeProp;
    private SerializedProperty attackTriggerNameProp;
    private SerializedProperty specialAttackTriggerNameProp;
    private SerializedProperty specialAttackChanceProp;
    private SerializedProperty rotateTowardTargetProp;
    private SerializedProperty lockXAxisProp;
    private SerializedProperty useLookRootYawAsBaseProp;

    private SerializedProperty dotweenIdleEnabledProp;
    private SerializedProperty dotweenIdleOffsetProp;
    private SerializedProperty dotweenIdleDurationProp;
    private SerializedProperty dotweenIdleEaseProp;

    private SerializedProperty dotweenAttackDistanceProp;
    private SerializedProperty dotweenAttackForwardDurationProp;
    private SerializedProperty dotweenAttackReturnDurationProp;
    private SerializedProperty dotweenAttackForwardEaseProp;
    private SerializedProperty dotweenAttackReturnEaseProp;
    private SerializedProperty dotweenAttackTowardTargetProp;
    private SerializedProperty dotweenAttackIgnoreYProp;
    private SerializedProperty dotweenSpecialAttackChanceProp;
    private SerializedProperty dotweenSpecialDistanceMultiplierProp;
    private SerializedProperty dotweenSpecialSpinTurnsProp;
    private SerializedProperty dotweenSpecialSpinDurationProp;
    private SerializedProperty dotweenSpecialSpinEaseProp;

    private void OnEnable()
    {
        animatorProp = serializedObject.FindProperty("animator");
        lookRootProp = serializedObject.FindProperty("lookRoot");
        tweenRootProp = serializedObject.FindProperty("tweenRoot");
        visualModeProp = serializedObject.FindProperty("visualMode");
        attackTriggerNameProp = serializedObject.FindProperty("attackTriggerName");
        specialAttackTriggerNameProp = serializedObject.FindProperty("specialAttackTriggerName");
        specialAttackChanceProp = serializedObject.FindProperty("specialAttackChance");
        rotateTowardTargetProp = serializedObject.FindProperty("rotateTowardTarget");
        lockXAxisProp = serializedObject.FindProperty("lockXAxis");
        useLookRootYawAsBaseProp = serializedObject.FindProperty("useLookRootYawAsBase");

        dotweenIdleEnabledProp = serializedObject.FindProperty("dotweenIdleEnabled");
        dotweenIdleOffsetProp = serializedObject.FindProperty("dotweenIdleOffset");
        dotweenIdleDurationProp = serializedObject.FindProperty("dotweenIdleDuration");
        dotweenIdleEaseProp = serializedObject.FindProperty("dotweenIdleEase");

        dotweenAttackDistanceProp = serializedObject.FindProperty("dotweenAttackDistance");
        dotweenAttackForwardDurationProp = serializedObject.FindProperty("dotweenAttackForwardDuration");
        dotweenAttackReturnDurationProp = serializedObject.FindProperty("dotweenAttackReturnDuration");
        dotweenAttackForwardEaseProp = serializedObject.FindProperty("dotweenAttackForwardEase");
        dotweenAttackReturnEaseProp = serializedObject.FindProperty("dotweenAttackReturnEase");
        dotweenAttackTowardTargetProp = serializedObject.FindProperty("dotweenAttackTowardTarget");
        dotweenAttackIgnoreYProp = serializedObject.FindProperty("dotweenAttackIgnoreY");
        dotweenSpecialAttackChanceProp = serializedObject.FindProperty("dotweenSpecialAttackChance");
        dotweenSpecialDistanceMultiplierProp = serializedObject.FindProperty("dotweenSpecialDistanceMultiplier");
        dotweenSpecialSpinTurnsProp = serializedObject.FindProperty("dotweenSpecialSpinTurns");
        dotweenSpecialSpinDurationProp = serializedObject.FindProperty("dotweenSpecialSpinDuration");
        dotweenSpecialSpinEaseProp = serializedObject.FindProperty("dotweenSpecialSpinEase");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((IdlePetAttackFeedback)target), typeof(IdlePetAttackFeedback), false);
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(visualModeProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Aim", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(lookRootProp);
        EditorGUILayout.PropertyField(rotateTowardTargetProp);
        EditorGUILayout.PropertyField(lockXAxisProp);
        EditorGUILayout.PropertyField(useLookRootYawAsBaseProp);

        var mode = (PetAttackVisualMode)visualModeProp.enumValueIndex;
        if (mode == PetAttackVisualMode.Animator)
            DrawAnimatorSection();
        else
            DrawDotweenSection();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawAnimatorSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animator", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(animatorProp);
        EditorGUILayout.PropertyField(attackTriggerNameProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Special Attack", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(specialAttackTriggerNameProp);
        EditorGUILayout.PropertyField(specialAttackChanceProp);
    }

    private void DrawDotweenSection()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dotween", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(tweenRootProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dotween Idle", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(dotweenIdleEnabledProp);
        if (dotweenIdleEnabledProp.boolValue)
        {
            EditorGUILayout.PropertyField(dotweenIdleOffsetProp);
            EditorGUILayout.PropertyField(dotweenIdleDurationProp);
            EditorGUILayout.PropertyField(dotweenIdleEaseProp);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dotween Attack", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(dotweenAttackDistanceProp);
        EditorGUILayout.PropertyField(dotweenAttackForwardDurationProp);
        EditorGUILayout.PropertyField(dotweenAttackReturnDurationProp);
        EditorGUILayout.PropertyField(dotweenAttackForwardEaseProp);
        EditorGUILayout.PropertyField(dotweenAttackReturnEaseProp);
        EditorGUILayout.PropertyField(dotweenAttackTowardTargetProp);
        EditorGUILayout.PropertyField(dotweenAttackIgnoreYProp);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dotween Special Attack", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(dotweenSpecialAttackChanceProp);
        if (dotweenSpecialAttackChanceProp.floatValue > 0f)
        {
            EditorGUILayout.PropertyField(dotweenSpecialDistanceMultiplierProp);
            EditorGUILayout.PropertyField(dotweenSpecialSpinTurnsProp);
            EditorGUILayout.PropertyField(dotweenSpecialSpinDurationProp);
            EditorGUILayout.PropertyField(dotweenSpecialSpinEaseProp);
        }
    }
}
