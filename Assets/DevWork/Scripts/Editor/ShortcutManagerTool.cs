// Assets/Editor/ToolbarSceneSwitcher.cs
#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class ToolbarSceneSwitcher
{
    private static VisualElement _rightZone;
    private static IMGUIContainer _imguiContainer;

    private static string[] _scenePaths = Array.Empty<string>();
    private static string[] _sceneNames = Array.Empty<string>();
    private static int _selectedIndex;

    static ToolbarSceneSwitcher()
    {
        EditorApplication.update += TryInstallOnce;
        RefreshScenes();
    }

    private static void TryInstallOnce()
    {
        // Find the internal UnityEditor.Toolbar object
        var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
        if (toolbarType == null) return;

        var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
        if (toolbars == null || toolbars.Length == 0) return;

        // Get UIElements root
        var rootField = toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
        if (rootField == null) return;

        var root = rootField.GetValue(toolbars[0]) as VisualElement;
        if (root == null) return;

        // Find the right-aligned container: "ToolbarZoneRightAlign"
        _rightZone = root.Q("ToolbarZoneRightAlign");
        if (_rightZone == null)
        {
            // Fallbacks (older editor versions)
            _rightZone = root.Q("RightAlign") ?? root;
        }

        if (_imguiContainer != null || _rightZone == null) return;

        _imguiContainer = new IMGUIContainer(DrawToolbarGUI)
        {
            style =
            {
                marginLeft = 6,
                marginRight = 6
            }
        };
        _rightZone.Add(_imguiContainer);

        // Installed; stop polling
        EditorApplication.update -= TryInstallOnce;
    }

    private static void RefreshScenes()
    {
        var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        _scenePaths = guids.Select(g => AssetDatabase.GUIDToAssetPath(g)).ToArray();
        _sceneNames = _scenePaths.Select(p => System.IO.Path.GetFileNameWithoutExtension(p)).ToArray();

        // Keep selected index valid
        if (_sceneNames.Length == 0) _selectedIndex = 0;
        else _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _sceneNames.Length - 1);
    }

    private static void DrawToolbarGUI()
    {
        if (_sceneNames == null || _sceneNames.Length == 0)
        {
            if (GUILayout.Button(new GUIContent("↻ Scenes"), ToolbarButtonStyle()))
            {
                RefreshScenes();
                ShowTempNotification("Scene list refreshed");
            }
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("↻", "Refresh scene list"), ToolbarMiniButtonStyle(), GUILayout.Width(24)))
            {
                RefreshScenes();
                ShowTempNotification("Scene list refreshed");
            }

            _selectedIndex = EditorGUILayout.Popup(_selectedIndex, _sceneNames, ToolbarPopupStyle(), GUILayout.MaxWidth(220));

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button(new GUIContent("Load", "Open selected scene"), ToolbarButtonStyle(), GUILayout.Width(60)))
                {
                    LoadSelectedScene();
                }
            }
        }
    }

    private static void LoadSelectedScene()
    {
        if (_scenePaths == null || _scenePaths.Length == 0) return;
        var path = _scenePaths[_selectedIndex];
        if (string.IsNullOrEmpty(path)) return;

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(path);
            ShowTempNotification($"Loaded: {_sceneNames[_selectedIndex]}");
        }
    }

    private static GUIStyle ToolbarButtonStyle()
    {
        var s = new GUIStyle("Button");
        s.fixedHeight = 20;
        s.margin = new RectOffset(2, 2, 2, 2);
        return s;
    }

    private static GUIStyle ToolbarMiniButtonStyle()
    {
        var s = new GUIStyle("Button");
        s.fixedHeight = 20;
        s.margin = new RectOffset(2, 2, 2, 2);
        s.fontSize = 11;
        return s;
    }

    private static GUIStyle ToolbarPopupStyle()
    {
        var s = new GUIStyle(EditorStyles.popup);
        s.fixedHeight = 20;
        s.margin = new RectOffset(4, 4, 2, 2);
        return s;
    }

    private static void ShowTempNotification(string text)
    {
        // Show a quick notification in the SceneView
        SceneView.lastActiveSceneView?.ShowNotification(new GUIContent(text));
    }
}
#endif
