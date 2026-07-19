using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class JournalDebugResetOverlay : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private const string LocalSaveFile = "local_save.json";
    private const string JournalSaveFile = "journal_save.json";
    private const string QuestSaveFile = "quest_save.json";
    private const string TutorialSaveFile = "tutorial_progress.json";
    private const string DiscoverySaveFile = "block_discovery.json";

    private const string TutorialOnboardingKey = "tutorial.onboarding.v1.done";
    private const string TutorialRecipeKey = "tutorial.recipe.v1.done";
    private const string QuestProgressKey = "Q_PROGRESS_STATES";
    private const string QuestAchievementKey = "Q_ACHIEVEMENT_STATES";
    private const string QuestDailyKey = "Q_DAILY_KEY";
    private const string QuestDailyIdsKey = "Q_DAILY_IDS";
    private const string QuestDailyStatesKey = "Q_DAILY_STATES";

    private static JournalDebugResetOverlay instance;
    private bool isResetting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        var go = new GameObject(nameof(JournalDebugResetOverlay));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<JournalDebugResetOverlay>();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    [ContextMenu("Debug/Delete Saves And Reload")]
    private void DeleteSavesAndReload()
    {
        if (!Application.isPlaying || isResetting)
            return;

        StartCoroutine(DeleteSavesAndReload_Co());
    }

    private void OnGUI()
    {
        if (isResetting)
            return;

        const float width = 150f;
        const float height = 30f;
        Rect buttonRect = new Rect(Screen.width - width - 16f, 16f, width, height);
        if (GUI.Button(buttonRect, "Delete Save"))
            DeleteSavesAndReload();
    }

    private IEnumerator DeleteSavesAndReload_Co()
    {
        isResetting = true;

        DeleteLocalState();
        DeleteLegacyPlayerPrefs();
        DestroyPersistentRuntimeOwners();

        yield return null;

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
            SceneManager.LoadScene(activeScene.buildIndex);
        else
            SceneManager.LoadScene(activeScene.name);
    }

    private static void DeleteLocalState()
    {
        SaveCoordinator coordinator = SaveCoordinator.Ins;
        coordinator.Delete(LocalSaveFile, "DebugReset");
        coordinator.Delete(JournalSaveFile, "DebugReset");
        coordinator.Delete(QuestSaveFile, "DebugReset");
        coordinator.Delete(TutorialSaveFile, "DebugReset");
        coordinator.Delete(DiscoverySaveFile, "DebugReset");
    }

    private static void DeleteLegacyPlayerPrefs()
    {
        PlayerPrefs.DeleteKey(TutorialOnboardingKey);
        PlayerPrefs.DeleteKey(TutorialRecipeKey);
        PlayerPrefs.DeleteKey(QuestProgressKey);
        PlayerPrefs.DeleteKey(QuestAchievementKey);
        PlayerPrefs.DeleteKey(QuestDailyKey);
        PlayerPrefs.DeleteKey(QuestDailyIdsKey);
        PlayerPrefs.DeleteKey(QuestDailyStatesKey);
        PlayerPrefs.Save();
    }

    private static void DestroyPersistentRuntimeOwners()
    {
        DestroyIfAlive(JournalManager.Ins);
        DestroyIfAlive(QuestManager.Ins);
        DestroyIfAlive(DataSaver.Ins);
        DestroyIfAlive(BlockManager.Ins);
        DestroyIfAlive(Game.Discovery.BlockDiscoveryService.Ins);
        DestroyIfAlive(BiomeCompletionService.Ins);
        DestroyIfAlive(BiomeProgressionService.Instance);
    }

    private static void DestroyIfAlive(Component component)
    {
        if (component != null)
            Destroy(component.gameObject);
    }
#endif
}
