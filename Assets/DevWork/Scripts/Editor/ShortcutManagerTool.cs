#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;

public class ShortcutManagerTool : OdinEditorWindow
{
    [MenuItem("Tools/Shortcut Manager %#m")] // Ctrl + Shift + M
    private static void OpenWindow()
    {
        GetWindow<ShortcutManagerTool>("Shortcut Manager").Show();
    }

    [InfoBox("Click any button to load the scene. This auto-lists all scenes inside 'Assets/Scenes/'.")]
    [ListDrawerSettings(ShowFoldout = true, ShowPaging = false)]
    [PropertyOrder(1)]
    public List<SceneEntry> sceneButtons = new();

    [Button("Refresh Scene List"), GUIColor(1f, 0.7f, 0.2f)]
    private void RefreshSceneList()
    {
        sceneButtons.Clear();

        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });

        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);

            sceneButtons.Add(new SceneEntry(name, path));
        }

        Debug.Log($"[ShortcutManager] Found {sceneButtons.Count} scenes.");
    }

    [System.Serializable]
    public class SceneEntry
    {
        [ReadOnly]
        public string sceneName;

        [ReadOnly]
        public string scenePath;

        [Button("Load"), GUIColor(0.3f, 0.9f, 1f)]
        private void Load()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(scenePath);
                Debug.Log($"[ShortcutManager] Loaded scene: {sceneName}");
            }
        }

        public SceneEntry(string name, string path)
        {
            sceneName = name;
            scenePath = path;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (sceneButtons.Count == 0)
            RefreshSceneList();
    }
}
#endif
