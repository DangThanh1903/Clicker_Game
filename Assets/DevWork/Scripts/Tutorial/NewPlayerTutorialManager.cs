using System.Collections;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Serialization;

[Obsolete("Legacy tutorial is disabled. Use Journal onboarding instead.")]
public class NewPlayerTutorialManager : MonoBehaviour
{
    private const float AutoStepDelay = 7f;
    private const string TutorialProgressFileName = "tutorial_progress.json";
    private static readonly SaveCoordinator saveCoordinator = SaveCoordinator.Ins;
    private static TutorialProgressData tutorialProgressData;

    [Header("Overlay Source (Scene or Prefab)")]
    [SerializeField, FormerlySerializedAs("tutorialOverlayMiddle"), FormerlySerializedAs("tutorialOverlayTopHalf")]
    private TutorialOverlayView tutorialOverlay;

    [Header("Overlay Parent")]
    [SerializeField] private Transform overlayRoot;

    [Header("First-Time Gates")]
    [SerializeField] private bool enableLegacyTutorial = false;
    [SerializeField] private bool runOnlyFirstTime = true;
    [SerializeField] private string onboardingDoneKey = "tutorial.onboarding.v1.done";
    [SerializeField] private string recipeDoneKey = "tutorial.recipe.v1.done";

    [Header("Indexes")]
    [SerializeField] private int inventoryNavButtonIndex;
    [SerializeField] private int recipeNavButtonIndex = -1;
    [SerializeField] private int inventoryPageIndex;
    [SerializeField] private int craftingPageIndex = 1;
    [SerializeField] private int sortTrashPageIndex = 2;

    [Header("Optional Targets")]
    [SerializeField] private RectTransform breakBlockTarget;
    [SerializeField] private RectTransform staminaTargetOverride;
    [SerializeField] private RectTransform inventoryNavTargetOverride;
    [SerializeField] private RectTransform firstItemSlotTargetOverride;
    [SerializeField] private RectTransform statsTargetOverride;
    [SerializeField] private RectTransform craftingTargetOverride;
    [SerializeField] private RectTransform recipeNavTargetOverride;
    [SerializeField] private RectTransform trashCanTargetOverride;
    [SerializeField] private RectTransform sortButtonTargetOverride;
    [SerializeField] private RectTransform recipeCraftTargetOverride;

    [Header("Recipe Tutorial Target")]
    [SerializeField] private bool recipeTutorialUseSpecificNode = true;
    [SerializeField] private string recipeTutorialNodeName = "ClayMixture";

    [Header("Tutorial Starter Materials")]
    [SerializeField] private bool grantStarterMaterialsOnRecipeTutorialStart = true;
    [SerializeField] private string starterMaterialRecipeNodeName = "ClayMixture";
    [SerializeField, Min(0)] private int starterDirtAmount = 10;
    [SerializeField, Min(0)] private int starterClayAmount = 2;
    [SerializeField] private Item starterDirtItem;
    [SerializeField] private Item starterClayItem;

    [Header("Fallback Pointer Positions")]
    [SerializeField] private Vector2 breakBlockFallbackNormalized = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 inventoryFallbackNormalized = new Vector2(0.12f, 0.1f);
    [SerializeField] private Vector2 craftingFallbackNormalized = new Vector2(0.9f, 0.12f);
    [SerializeField] private Vector2 centerFallbackNormalized = new Vector2(0.5f, 0.5f);

    [Header("References")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private InventoryController inventoryController;
    [SerializeField] private InventorySlider inventorySlider;
    [SerializeField] private CraftingController craftingController;
    [SerializeField] private CraftNodeManager craftNodeManager;

    [Header("Runtime Resolve")]
    [SerializeField, Min(0.1f)] private float craftManagerResolveIntervalSeconds = 0.5f;

    private readonly CompositeDisposable signalSubscriptions = new CompositeDisposable();

    private TutorialOverlayView overlayInstance;
    private CraftNodeManager subscribedCraftNodeManager;
    private float nextCraftManagerResolveTime;

    private bool isOnboardingRunning;
    private bool isRecipeTutorialRunning;
    private bool pendingRecipeTutorial;
    private bool breakBlockTriggered;
    private bool craftedItemTriggered;
    private bool hasFirstItem;

    private Item firstItem;
    private int firstItemQty;
    private CraftNode pendingUnlockedNode;

    private void Start()
    {
        if (!enableLegacyTutorial)
        {
            enabled = false;
            return;
        }

        ResolveReferencesOnce();
        CacheOverlays();
        SubscribeSignals();
        HookEvents();
        StartCoroutine(CoRunTutorialFlow());
    }

    private void OnDestroy()
    {
        signalSubscriptions.Dispose();
        UnhookEvents();
    }

    private void Update()
    {
        TryAutoResolveCraftNodeManager();
    }

    private void ResolveReferencesOnce()
    {
        if (uiManager == null)
            uiManager = UIManager.Ins;

        if (inventoryController == null)
            inventoryController = InventoryController.Instance;

        if (inventorySlider == null && inventoryController != null)
            inventorySlider = inventoryController.InventorySlider;

        if (craftingController == null && inventoryController != null)
            craftingController = inventoryController.CraftingController;
    }

    private void CacheOverlays()
    {
        overlayInstance = BindOrInstantiateOverlay(tutorialOverlay, "TutorialOverlay");
    }

    private void SubscribeSignals()
    {
        QuestSignals.OnBreakBlock
            .Subscribe(_ => breakBlockTriggered = true)
            .AddTo(signalSubscriptions);

        QuestSignals.OnCraftItem
            .Subscribe(_ => craftedItemTriggered = true)
            .AddTo(signalSubscriptions);

        QuestSignals.OnCollectItem
            .Subscribe(x =>
            {
                if (hasFirstItem)
                    return;

                hasFirstItem = true;
                firstItemQty = Mathf.Max(1, x.amount);
            })
            .AddTo(signalSubscriptions);
    }

    private void HookEvents()
    {
        if (inventoryController != null)
            inventoryController.OnMainInventoryItemAdded += HandleInventoryItemAdded;

        RebindCraftNodeManager(craftNodeManager);
        TryAutoResolveCraftNodeManager(force: true);
    }

    private void UnhookEvents()
    {
        if (inventoryController != null)
            inventoryController.OnMainInventoryItemAdded -= HandleInventoryItemAdded;

        RebindCraftNodeManager(null);
    }

    private void TryAutoResolveCraftNodeManager(bool force = false)
    {
        float interval = Mathf.Max(0.1f, craftManagerResolveIntervalSeconds);
        if (!force && Time.unscaledTime < nextCraftManagerResolveTime)
            return;

        nextCraftManagerResolveTime = Time.unscaledTime + interval;

        if (subscribedCraftNodeManager != null && subscribedCraftNodeManager.isActiveAndEnabled)
        {
            TryTriggerRecipeTutorialFromCurrentState();
            return;
        }

        CraftNodeManager resolved = FindRuntimeCraftNodeManager();
        if (resolved != subscribedCraftNodeManager)
            RebindCraftNodeManager(resolved);
    }

    private CraftNodeManager FindRuntimeCraftNodeManager()
    {
        if (craftNodeManager != null &&
            craftNodeManager.gameObject.scene.IsValid() &&
            craftNodeManager.isActiveAndEnabled)
        {
            return craftNodeManager;
        }

        CraftNodeManager[] managers = FindObjectsByType<CraftNodeManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        CraftNodeManager fallback = null;
        foreach (var manager in managers)
        {
            if (manager == null || !manager.gameObject.scene.IsValid())
                continue;

            if (manager.isActiveAndEnabled)
                return manager;

            if (fallback == null)
                fallback = manager;
        }

        return fallback;
    }

    private void RebindCraftNodeManager(CraftNodeManager manager)
    {
        if (subscribedCraftNodeManager == manager)
        {
            craftNodeManager = manager;
            return;
        }

        if (subscribedCraftNodeManager != null)
            subscribedCraftNodeManager.OnNodeUnlocked -= HandleNodeUnlocked;

        subscribedCraftNodeManager = manager;
        craftNodeManager = manager;

        if (subscribedCraftNodeManager != null)
            subscribedCraftNodeManager.OnNodeUnlocked += HandleNodeUnlocked;

        TryTriggerRecipeTutorialFromCurrentState();
    }

    private void TryTriggerRecipeTutorialFromCurrentState()
    {
        if (subscribedCraftNodeManager == null)
            return;
        if (!ShouldRunRecipeTutorial() || isRecipeTutorialRunning)
            return;
        if (pendingRecipeTutorial || pendingUnlockedNode != null)
            return;

        CraftNode unlockedNode = null;
        foreach (var node in subscribedCraftNodeManager.allNodes)
        {
            if (node == null)
                continue;

            if (node.State != CraftNodeState.Locked && IsRecipeTutorialTargetNode(node))
            {
                unlockedNode = node;
                break;
            }
        }

        if (unlockedNode == null)
            return;

        pendingUnlockedNode = unlockedNode;
        if (isOnboardingRunning)
        {
            pendingRecipeTutorial = true;
            return;
        }

        StartCoroutine(CoRunRecipeUnlockTutorial(unlockedNode));
    }

    private void HandleInventoryItemAdded(Item item, int amount)
    {
        if (hasFirstItem || item == null || amount <= 0)
            return;

        hasFirstItem = true;
        firstItem = item;
        firstItemQty = amount;
    }

    private void HandleNodeUnlocked(CraftNode node)
    {
        if (!ShouldRunRecipeTutorial() || isRecipeTutorialRunning)
            return;
        if (!IsRecipeTutorialTargetNode(node))
            return;

        pendingUnlockedNode = node;

        if (isOnboardingRunning)
        {
            pendingRecipeTutorial = true;
            return;
        }

        StartCoroutine(CoRunRecipeUnlockTutorial(node));
    }

    private IEnumerator CoRunTutorialFlow()
    {
        if (runOnlyFirstTime && !ShouldRunOnboarding() && !ShouldRunRecipeTutorial())
        {
            Destroy(gameObject);
            yield break;
        }

        if (ShouldRunOnboarding())
            yield return CoRunOnboardingTutorial();

        if (pendingRecipeTutorial && ShouldRunRecipeTutorial())
        {
            pendingRecipeTutorial = false;
            yield return CoRunRecipeUnlockTutorial(pendingUnlockedNode);
        }
    }

    private IEnumerator CoRunOnboardingTutorial()
    {
        isOnboardingRunning = true;
        breakBlockTriggered = false;

        bool useNextForBreak = breakBlockTarget == null;
        ShowStep("Break a block to start collecting resources.", breakBlockTarget, breakBlockFallbackNormalized, useNextForBreak);
        yield return WaitUntilOrNext(() => breakBlockTriggered, useNextForBreak);

        RectTransform staminaTarget = ResolveStaminaTarget();
        ShowStep(
            "This bar is Stamina. In Normal mode, having stamina boosts click damage. When stamina is low, damage drops until stamina regenerates.",
            staminaTarget,
            centerFallbackNormalized,
            true);
        yield return WaitDelayOrNext(AutoStepDelay, true);

        RectTransform inventoryTarget = ResolveInventoryNavTarget();
        bool useNextForInventory = inventoryTarget == null;
        ShowStep("Tap Inventory here.", inventoryTarget, inventoryFallbackNormalized, useNextForInventory);
        yield return WaitUntilOrNext(() => uiManager != null && uiManager.CurrentIndex == inventoryPageIndex, useNextForInventory);

        string itemName = firstItem != null ? firstItem.itemName : "items";
        int qty = Mathf.Max(1, firstItemQty);
        ShowStep($"Great. You got x{qty} {itemName}. This is where items appear.", ResolveFirstItemSlotTarget(), centerFallbackNormalized, true);
        yield return WaitDelayOrNext(AutoStepDelay, true);

        if (inventorySlider != null)
            inventorySlider.GoToStatsPage();

        ShowStep("Here you can see your stats and character info.", ResolveStatsTarget(), centerFallbackNormalized, true);
        yield return WaitDelayOrNext(AutoStepDelay, true);

        if (inventorySlider != null)
            inventorySlider.GoToPage(craftingPageIndex);

        ShowStep("This is your crafting table. Use materials to craft items.", ResolveCraftingTarget(), centerFallbackNormalized, true);
        yield return WaitDelayOrNext(AutoStepDelay, true);

        if (inventorySlider != null)
        {
            inventorySlider.GoToPage(sortTrashPageIndex);
            yield return WaitUntil(() => inventorySlider.CurrentPage == sortTrashPageIndex);
        }

        RectTransform trashTarget = ResolveTrashTarget();
        if (trashTarget != null)
        {
            ShowStep("This is the trash can. Dropped items here will be removed.", trashTarget, centerFallbackNormalized, true);
            yield return WaitDelayOrNext(AutoStepDelay, true);
        }

        RectTransform sortTarget = ResolveSortTarget();
        if (sortTarget != null)
        {
            ShowStep("Tap Sort to organize your inventory quickly.", sortTarget, centerFallbackNormalized, true);
            yield return WaitDelayOrNext(AutoStepDelay, true);
        }

        ShowStep("That's all. You are ready to play.", null, centerFallbackNormalized, true, false);
        yield return WaitDelayOrNext(AutoStepDelay, true);

        HideActiveOverlay();
        MarkOnboardingDone();
        isOnboardingRunning = false;
    }

    private IEnumerator CoRunRecipeUnlockTutorial(CraftNode unlockedNode)
    {
        if (!ShouldRunRecipeTutorial())
            yield break;
        if (!IsRecipeTutorialTargetNode(unlockedNode))
            yield break;

        isRecipeTutorialRunning = true;
        craftedItemTriggered = false;

        // Let unlock popup spawn first, then wait until all popups are closed.
        yield return WaitForPopupsToClose();
        yield return GrantStarterMaterialsForRecipeTutorial(unlockedNode);

        string recipeName = GetRecipeTutorialDisplayName(unlockedNode);

        RectTransform recipeNavTarget = ResolveRecipeNavTarget();
        bool useNextForRecipeNav = recipeNavTarget == null;
        ShowStep($"Recipe available: {recipeName}. Tap Recipe here.", recipeNavTarget, craftingFallbackNormalized, useNextForRecipeNav);
        yield return WaitUntilOrNext(() => IsCraftingTableReady(unlockedNode), useNextForRecipeNav);

        RectTransform recipeNodeTarget = ResolveRecipeNodeTarget(unlockedNode);
        bool useNextForRecipe = recipeNodeTarget == null;
        ShowStep($"Tap {recipeName} recipe node.", recipeNodeTarget, centerFallbackNormalized, useNextForRecipe);
        yield return WaitUntilOrNext(() => IsRecipePanelShowingForNode(unlockedNode), useNextForRecipe);

        RectTransform craftTarget = ResolveCraftActionTarget();
        bool useNextForCraft = craftTarget == null;
        ShowStep("Tap Craft button to craft this recipe.", craftTarget, centerFallbackNormalized, useNextForCraft);

        while (!craftedItemTriggered)
        {
            if (useNextForCraft && overlayInstance != null && overlayInstance.ConsumeNextPressed())
                break;

            yield return null;
        }

        ShowStep("That's all. You can craft this recipe anytime.", null, centerFallbackNormalized, true, false);
        yield return WaitDelayOrNext(AutoStepDelay, true);

        HideActiveOverlay();
        MarkRecipeTutorialDone();

        isRecipeTutorialRunning = false;
        pendingUnlockedNode = null;
    }

    private static IEnumerator WaitForPopupsToClose()
    {
        // Unlock popup is enqueued on the same frame as node unlock.
        // Wait one frame so PopupController stack can be updated.
        yield return null;

        while (PopupController.Instance != null && PopupController.Instance.IsAnyPopupOpen())
            yield return null;
    }

    private static IEnumerator WaitUntil(System.Func<bool> predicate)
    {
        while (!predicate())
            yield return null;
    }

    private IEnumerator GrantStarterMaterialsForRecipeTutorial(CraftNode node)
    {
        if (!grantStarterMaterialsOnRecipeTutorialStart)
            yield break;
        if (!IsStarterMaterialRecipeNode(node))
            yield break;

        if (starterDirtAmount <= 0 && starterClayAmount <= 0)
            yield break;

        int timeoutFrames = 120;
        while (inventoryController == null && timeoutFrames-- > 0)
        {
            ResolveReferencesOnce();
            yield return null;
        }

        if (inventoryController == null)
        {
            Debug.LogWarning("Tutorial: InventoryController missing, cannot grant starter materials.");
            yield break;
        }

        if (starterDirtAmount > 0)
            inventoryController.TryAddItemToInventory(new InventoryItem(starterDirtItem, starterDirtAmount));

        if (starterClayAmount > 0)
            inventoryController.TryAddItemToInventory(new InventoryItem(starterClayItem, starterClayAmount));
    }

    private bool IsStarterMaterialRecipeNode(CraftNode node)
    {
        if (node == null)
            return false;

        if (string.IsNullOrWhiteSpace(starterMaterialRecipeNodeName))
            return true;

        string expected = NormalizeRecipeKey(starterMaterialRecipeNodeName);
        if (string.IsNullOrEmpty(expected))
            return true;

        if (string.Equals(NormalizeRecipeKey(node.nodeName), expected, System.StringComparison.Ordinal))
            return true;

        Item recipeItem = node.GetPrimaryRecipeItem();
        if (recipeItem != null)
        {
            if (string.Equals(NormalizeRecipeKey(recipeItem.itemName), expected, System.StringComparison.Ordinal))
                return true;

            if (string.Equals(NormalizeRecipeKey(recipeItem.name), expected, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private IEnumerator WaitUntilOrNext(System.Func<bool> predicate, bool allowNextButton)
    {
        while (!predicate())
        {
            if (allowNextButton && overlayInstance != null && overlayInstance.ConsumeNextPressed())
                break;

            yield return null;
        }
    }

    private IEnumerator WaitDelayOrNext(float seconds, bool allowNextButton)
    {
        if (seconds <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (allowNextButton && overlayInstance != null && overlayInstance.ConsumeNextPressed())
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void ShowStep(
        string message,
        RectTransform target,
        Vector2 fallbackNormalized,
        bool showNextButton = false,
        bool showHandPointer = true)
    {
        TutorialOverlayView overlay = overlayInstance;
        if (overlay == null)
            return;

        overlay.Show(message, target, fallbackNormalized, showNextButton, showHandPointer);
    }

    private void HideActiveOverlay()
    {
        if (overlayInstance != null)
            overlayInstance.Hide();
    }

    private RectTransform ResolveInventoryNavTarget()
    {
        if (inventoryNavTargetOverride != null)
            return inventoryNavTargetOverride;

        if (uiManager == null)
            return null;

        var button = uiManager.GetNavButton(inventoryNavButtonIndex);
        return button != null ? button.transform as RectTransform : null;
    }

    private RectTransform ResolveStaminaTarget()
    {
        return staminaTargetOverride;
    }

    private RectTransform ResolveFirstItemSlotTarget()
    {
        if (firstItemSlotTargetOverride != null)
            return firstItemSlotTargetOverride;

        if (inventoryController != null && inventoryController.TryGetFirstNonEmptyInventorySlot(out RectTransform slot, out _))
            return slot;

        return null;
    }

    private RectTransform ResolveStatsTarget()
    {
        if (statsTargetOverride != null)
            return statsTargetOverride;

        if (inventoryController == null)
            return null;

        var stat = inventoryController.GetFirstStatText();
        if (stat != null)
            return stat.rectTransform;

        return inventoryController.DescriptionText != null ? inventoryController.DescriptionText.rectTransform : null;
    }

    private RectTransform ResolveCraftingTarget()
    {
        if (craftingTargetOverride != null)
            return craftingTargetOverride;

        return craftingController != null ? craftingController.transform as RectTransform : null;
    }

    private RectTransform ResolveRecipeNavTarget()
    {
        if (recipeNavTargetOverride != null)
            return recipeNavTargetOverride;

        if (uiManager != null && recipeNavButtonIndex >= 0)
        {
            Button navButton = uiManager.GetNavButton(recipeNavButtonIndex);
            if (navButton != null)
                return navButton.transform as RectTransform;
        }

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var button in buttons)
        {
            if (button == null || !button.gameObject.scene.IsValid())
                continue;

            string objectName = button.gameObject.name;
            if (!string.IsNullOrWhiteSpace(objectName) &&
                objectName.IndexOf("recipe", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return button.transform as RectTransform;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null &&
                !string.IsNullOrWhiteSpace(label.text) &&
                label.text.IndexOf("recipe", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return button.transform as RectTransform;
            }
        }

        return ResolveCraftingTarget();
    }

    private RectTransform ResolveTrashTarget()
    {
        return trashCanTargetOverride;
    }

    private RectTransform ResolveSortTarget()
    {
        if (sortButtonTargetOverride != null)
            return sortButtonTargetOverride;

        if (inventoryController != null && inventoryController.SortInventoryButton != null)
            return inventoryController.SortInventoryButton.transform as RectTransform;

        return null;
    }

    private RectTransform ResolveRecipeCraftTarget()
    {
        if (recipeCraftTargetOverride != null)
            return recipeCraftTargetOverride;

        RectTransform crafting = ResolveCraftingTarget();
        if (crafting != null)
            return crafting;

        return ResolveSortTarget();
    }

    private bool IsCraftingTableReady(CraftNode node)
    {
        if (node != null && node.gameObject != null && node.gameObject.activeInHierarchy)
            return true;

        RectTransform crafting = ResolveCraftingTarget();
        return crafting != null && crafting.gameObject.activeInHierarchy;
    }

    private RectTransform ResolveRecipeNodeTarget(CraftNode node)
    {
        if (node == null)
            return null;

        return node.transform as RectTransform;
    }

    private RectTransform ResolveCraftActionTarget()
    {
        if (recipeCraftTargetOverride != null)
            return recipeCraftTargetOverride;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var button in buttons)
        {
            if (button == null || button.onClick == null)
                continue;

            var clickEvent = button.onClick;
            int eventCount = clickEvent.GetPersistentEventCount();
            for (int i = 0; i < eventCount; i++)
            {
                if (!string.Equals(clickEvent.GetPersistentMethodName(i), "OnClickAutoFillRecipe", System.StringComparison.Ordinal))
                    continue;

                if (clickEvent.GetPersistentTarget(i) is CraftRecipePanel)
                    return button.transform as RectTransform;
            }
        }

        return ResolveRecipeCraftTarget();
    }

    private bool IsRecipePanelShowingForNode(CraftNode node)
    {
        CraftRecipePanel panel = ResolveRecipePanel(node);
        if (panel == null || panel.currentRecipe == null)
            return false;

        Item targetItem = GetTargetRecipeItem(node);
        if (targetItem == null)
            return true;

        return panel.currentRecipe.result != null && panel.currentRecipe.result.itemData == targetItem;
    }

    private CraftRecipePanel ResolveRecipePanel(CraftNode node)
    {
        if (node != null && node.recipePanel != null)
            return node.recipePanel;

        return FindFirstObjectByType<CraftRecipePanel>(FindObjectsInactive.Include);
    }

    private Item GetTargetRecipeItem(CraftNode node)
    {
        return node != null ? node.GetPrimaryRecipeItem() : null;
    }

    private bool IsRecipeTutorialTargetNode(CraftNode node)
    {
        if (node == null)
            return false;

        if (!recipeTutorialUseSpecificNode)
            return true;

        if (string.IsNullOrWhiteSpace(recipeTutorialNodeName))
            return true;

        string expected = NormalizeRecipeKey(recipeTutorialNodeName);
        if (string.IsNullOrEmpty(expected))
            return true;

        if (string.Equals(NormalizeRecipeKey(node.nodeName), expected, System.StringComparison.Ordinal))
            return true;

        Item recipeItem = GetTargetRecipeItem(node);
        if (recipeItem != null)
        {
            if (string.Equals(NormalizeRecipeKey(recipeItem.itemName), expected, System.StringComparison.Ordinal))
                return true;

            if (string.Equals(NormalizeRecipeKey(recipeItem.name), expected, System.StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private string GetRecipeTutorialDisplayName(CraftNode node)
    {
        if (recipeTutorialUseSpecificNode && !string.IsNullOrWhiteSpace(recipeTutorialNodeName))
            return recipeTutorialNodeName.Trim();

        if (node != null && !string.IsNullOrWhiteSpace(node.nodeName))
            return node.nodeName;

        return "a new recipe";
    }

    private static string NormalizeRecipeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        System.Text.StringBuilder sb = new System.Text.StringBuilder(trimmed.Length);
        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (char.IsWhiteSpace(c) || c == '_' || c == '-')
                continue;

            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

    private bool ShouldRunOnboarding()
    {
        return !runOnlyFirstTime || !IsTutorialDone(onboardingDoneKey);
    }

    private bool ShouldRunRecipeTutorial()
    {
        return !runOnlyFirstTime || !IsTutorialDone(recipeDoneKey);
    }

    private void MarkOnboardingDone()
    {
        MarkTutorialDone(onboardingDoneKey);
    }

    private void MarkRecipeTutorialDone()
    {
        MarkTutorialDone(recipeDoneKey);
    }

    [ContextMenu("Reset Tutorial Progress")]
    private void ResetTutorialProgress()
    {
        ResetTutorialKey(onboardingDoneKey);
        ResetTutorialKey(recipeDoneKey);
    }

    private static bool IsTutorialDone(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        EnsureTutorialProgressLoaded();
        if (tutorialProgressData.completedKeys.Contains(key))
            return true;

        if (!PlayerPrefs.HasKey(key) || PlayerPrefs.GetInt(key, 0) != 1)
            return false;

        tutorialProgressData.completedKeys.Add(key);
        SaveTutorialProgress();
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
        return true;
    }

    private static void MarkTutorialDone(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        EnsureTutorialProgressLoaded();
        if (!tutorialProgressData.completedKeys.Contains(key))
        {
            tutorialProgressData.completedKeys.Add(key);
            SaveTutorialProgress();
        }

        if (PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    private static void ResetTutorialKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        EnsureTutorialProgressLoaded();
        tutorialProgressData.completedKeys.Remove(key);
        SaveTutorialProgress();

        if (PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    private static void EnsureTutorialProgressLoaded()
    {
        if (tutorialProgressData != null)
            return;

        tutorialProgressData = new TutorialProgressData();
        if (!saveCoordinator.TryLoadJson(TutorialProgressFileName, out tutorialProgressData, "Tutorial"))
        {
            tutorialProgressData = new TutorialProgressData();
            return;
        }

        tutorialProgressData.completedKeys ??= new List<string>();
    }

    private static void SaveTutorialProgress()
    {
        saveCoordinator.TrySaveJson(TutorialProgressFileName, tutorialProgressData ?? new TutorialProgressData(), "Tutorial");
    }

    [Serializable]
    private class TutorialProgressData
    {
        public List<string> completedKeys = new List<string>();
    }

    private TutorialOverlayView BindOrInstantiateOverlay(TutorialOverlayView source, string runtimeName)
    {
        if (source == null)
            return null;

        TutorialOverlayView overlay = source;
        if (!source.gameObject.scene.IsValid())
        {
            overlay = Instantiate(source, GetOverlayParent());
            overlay.name = runtimeName;
        }

        overlay.Hide();
        return overlay;
    }

    private Transform GetOverlayParent()
    {
        return overlayRoot != null ? overlayRoot : transform;
    }
}
