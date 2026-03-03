using System.Collections;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;

public class NewPlayerTutorialManager : MonoBehaviour
{
    private const float AutoStepDelay = 7f;

    [Header("Overlay Source (Scene or Prefab)")]
    [SerializeField, FormerlySerializedAs("tutorialOverlayMiddle"), FormerlySerializedAs("tutorialOverlayTopHalf")]
    private TutorialOverlayView tutorialOverlay;

    [Header("Overlay Parent")]
    [SerializeField] private Transform overlayRoot;

    [Header("First-Time Gates")]
    [SerializeField] private bool runOnlyFirstTime = true;
    [SerializeField] private string onboardingDoneKey = "tutorial.onboarding.v1.done";
    [SerializeField] private string recipeDoneKey = "tutorial.recipe.v1.done";

    [Header("Indexes")]
    [SerializeField] private int inventoryNavButtonIndex;
    [SerializeField] private int inventoryPageIndex;
    [SerializeField] private int craftingPageIndex = 1;
    [SerializeField] private int sortTrashPageIndex = 2;

    [Header("Optional Targets")]
    [SerializeField] private RectTransform breakBlockTarget;
    [SerializeField] private RectTransform inventoryNavTargetOverride;
    [SerializeField] private RectTransform firstItemSlotTargetOverride;
    [SerializeField] private RectTransform statsTargetOverride;
    [SerializeField] private RectTransform craftingTargetOverride;
    [SerializeField] private RectTransform trashCanTargetOverride;
    [SerializeField] private RectTransform sortButtonTargetOverride;
    [SerializeField] private RectTransform recipeCraftTargetOverride;

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

    private readonly CompositeDisposable signalSubscriptions = new CompositeDisposable();

    private TutorialOverlayView overlayInstance;

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

        if (craftNodeManager != null)
            craftNodeManager.OnNodeUnlocked += HandleNodeUnlocked;
    }

    private void UnhookEvents()
    {
        if (inventoryController != null)
            inventoryController.OnMainInventoryItemAdded -= HandleInventoryItemAdded;

        if (craftNodeManager != null)
            craftNodeManager.OnNodeUnlocked -= HandleNodeUnlocked;
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

        isRecipeTutorialRunning = true;
        craftedItemTriggered = false;

        string recipeName = unlockedNode != null && !string.IsNullOrEmpty(unlockedNode.nodeName)
            ? unlockedNode.nodeName
            : "a new recipe";

        RectTransform inventoryTarget = ResolveInventoryNavTarget();
        bool useNextForInventory = inventoryTarget == null;
        ShowStep($"New recipe unlocked: {recipeName}. Open Inventory.", inventoryTarget, inventoryFallbackNormalized, useNextForInventory);
        yield return WaitUntilOrNext(() => uiManager != null && uiManager.CurrentIndex == inventoryPageIndex, useNextForInventory);

        RectTransform craftingTabTarget = inventorySlider != null && inventorySlider.RightButton != null
            ? inventorySlider.RightButton.transform as RectTransform
            : ResolveCraftingTarget();

        bool useNextForCraftingTab = craftingTabTarget == null;
        ShowStep("Go to Crafting tab.", craftingTabTarget, craftingFallbackNormalized, useNextForCraftingTab);
        yield return WaitUntilOrNext(() => inventorySlider == null || inventorySlider.CurrentPage == craftingPageIndex, useNextForCraftingTab);

        RectTransform craftTarget = ResolveRecipeCraftTarget();
        bool useNextForCraft = craftTarget == null;
        ShowStep("Craft this unlocked recipe now.", craftTarget, centerFallbackNormalized, useNextForCraft);

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

    private static IEnumerator WaitUntil(System.Func<bool> predicate)
    {
        while (!predicate())
            yield return null;
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

    private bool ShouldRunOnboarding()
    {
        return !runOnlyFirstTime || PlayerPrefs.GetInt(onboardingDoneKey, 0) == 0;
    }

    private bool ShouldRunRecipeTutorial()
    {
        return !runOnlyFirstTime || PlayerPrefs.GetInt(recipeDoneKey, 0) == 0;
    }

    private void MarkOnboardingDone()
    {
        PlayerPrefs.SetInt(onboardingDoneKey, 1);
        PlayerPrefs.Save();
    }

    private void MarkRecipeTutorialDone()
    {
        PlayerPrefs.SetInt(recipeDoneKey, 1);
        PlayerPrefs.Save();
    }

    [ContextMenu("Reset Tutorial Progress")]
    private void ResetTutorialProgress()
    {
        PlayerPrefs.DeleteKey(onboardingDoneKey);
        PlayerPrefs.DeleteKey(recipeDoneKey);
        PlayerPrefs.Save();
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
