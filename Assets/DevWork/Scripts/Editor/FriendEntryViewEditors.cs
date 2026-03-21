using UnityEditor;

[CustomEditor(typeof(FriendListItemView))]
public sealed class FriendListItemViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptField();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Common", EditorStyles.boldLabel);
        Draw("displayNameText");
        Draw("profileButton");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Friend Row", EditorStyles.boldLabel);
        Draw("sinceText");
        Draw("giftButton");
        Draw("removeButton");

        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property);
    }

    private void DrawScriptField()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            MonoScript script = MonoScript.FromMonoBehaviour((FriendListItemView)target);
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        }
    }
}

[CustomEditor(typeof(FriendRequestItemView))]
public sealed class FriendRequestItemViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptField();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Common", EditorStyles.boldLabel);
        Draw("displayNameText");
        Draw("profileButton");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Request Row", EditorStyles.boldLabel);
        Draw("createdAtText");
        Draw("acceptButton");
        Draw("rejectButton");
        Draw("cancelButton");

        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property);
    }

    private void DrawScriptField()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            MonoScript script = MonoScript.FromMonoBehaviour((FriendRequestItemView)target);
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        }
    }
}

[CustomEditor(typeof(FriendSearchResultItemView))]
public sealed class FriendSearchResultItemViewEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawScriptField();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Common", EditorStyles.boldLabel);
        Draw("avatarImage");
        Draw("fallbackAvatar");
        Draw("displayNameText");
        Draw("profileButton");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Search Row", EditorStyles.boldLabel);
        Draw("clicksText");
        Draw("playtimeText");
        Draw("addButton");

        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property);
    }

    private void DrawScriptField()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            MonoScript script = MonoScript.FromMonoBehaviour((FriendSearchResultItemView)target);
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        }
    }
}
