using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CraftRecipePanel : MonoBehaviour
{
    [Header("Refs")]
    public RecipeDatabase recipeDB;

    [Header("Ingredients 2x2 (index 0..3)")]
    public Image[] ingIcons = new Image[4];
    public TextMeshProUGUI[] ingQtyTexts = new TextMeshProUGUI[4];

    [Header("Result")]
    public Image resultIcon;
    public TextMeshProUGUI resultQtyText;

    [Header("Panel Root")]
    public GameObject root;
    [Header("Panel Root")]
    public TMP_Text Description;

    [Header("Auto Craft Refs")]
    [SerializeField] private InventoryUIManager inventoryUIManager;
    [SerializeField] private CraftingController craftingController;
    [SerializeField] private InventorySlider inventorySlider;
    [SerializeField] private UIManager mainPageManager;

    [Header("Missing Flash")]
    [SerializeField] private int missingFlashCount = 2;
    [SerializeField] private float missingFlashInterval = 0.12f;

    public Recipe currentRecipe { get; private set; }

    private Coroutine flashCoroutine;

    public void ShowForItem(Item targetItem)
    {
        if (recipeDB == null || targetItem == null)
        {
            ShowEmpty("No recipe DB / item");
            return;
        }

        var list = recipeDB.GetRecipesByResultItem(targetItem);

        if (Description)
        {
            Description.text = targetItem.GetFormattedDescription();
        }
        
        if (list.Count == 0)
        {
            ShowEmpty("No recipe found");
            return;
        }

        // láº¥y recipe Ä‘áº§u tiĂªn (náº¿u cĂ³ nhiá»u báº¡n cĂ³ thá»ƒ lĂ m UI Ä‘á»ƒ chuyá»ƒn tab)
        ShowRecipe(list[0]);
    }

    public void ShowRecipe(Recipe recipe)
    {
        if (recipe == null)
        {
            ShowEmpty("Null recipe");
            return;
        }

        currentRecipe = recipe;

        // Fill ingredients (Ä‘Ă£ normalize 4 slot trong DB; náº¿u khĂ´ng, tá»± báº£o vá»‡ null)
        for (int i = 0; i < 4; i++)
        {
            var it = (i < recipe.ingredients.Count) ? recipe.ingredients[i] : null;
            var data = it?.itemData;
            var qty = it?.quantity?.Value ?? 0;

            if (ingIcons != null && i < ingIcons.Length && ingIcons[i])
            {
                ingIcons[i].sprite = data != null && data.Type != ItemType.None ? data.icon : null;
                ingIcons[i].color = data != null && data.Type != ItemType.None ? Color.white : new Color(1,1,1,0); // áº©n náº¿u null
            }

            if (ingQtyTexts != null && i < ingQtyTexts.Length && ingQtyTexts[i])
            {
                ingQtyTexts[i].text = (data && qty > 0) ? $"x{qty}" : "";
            }
        }

        // Fill result
        var resData = recipe.result?.itemData;
        var resQty  = recipe.result?.quantity?.Value ?? 0;

        if (resultIcon)
        {
            resultIcon.sprite = resData ? resData.icon : null;
            resultIcon.color = resData ? Color.white : new Color(1,1,1,0);
        }
        if (resultQtyText)
        {
            resultQtyText.text = (resData && resQty > 0) ? $"x{resQty}" : "";
        }

        if (root) root.SetActive(true);
            else gameObject.SetActive(true);
    }

    public void OnClickAutoFillRecipe()
    {
        ResolveRefs();

        if (currentRecipe == null)
        {
            Debug.LogWarning("CraftRecipePanel: currentRecipe is null.");
            return;
        }

        if (inventoryUIManager == null || craftingController == null)
        {
            Debug.LogWarning("CraftRecipePanel: missing refs to InventoryUIManager or CraftingController.");
            return;
        }

        var inventoryData = inventoryUIManager.GetInventoryData(InventoryType.Inventory);
        if (inventoryData == null)
        {
            Debug.LogWarning("CraftRecipePanel: InventoryData (Inventory) not found.");
            return;
        }

        if (!craftingController.CheckRecipeIngredients(currentRecipe, inventoryData, out var missingSlots))
        {
            FlashMissingSlots(missingSlots);
            return;
        }

        if (mainPageManager != null)
            mainPageManager.GoToPage(0);

        if (inventorySlider != null)
            inventorySlider.GoToPage(1);

        if (!craftingController.TryAutoFillRecipe(currentRecipe, inventoryData, out missingSlots))
            FlashMissingSlots(missingSlots);
    }

    public void ShowEmpty(string reason = "")
    {
        currentRecipe = null;
        // XoĂ¡/áº©n sáº¡ch slot
        for (int i = 0; i < 4; i++)
        {
            if (ingIcons != null && i < ingIcons.Length && ingIcons[i])
            {
                ingIcons[i].sprite = null;
                ingIcons[i].color = new Color(1,1,1,0);
            }
            if (ingQtyTexts != null && i < ingQtyTexts.Length && ingQtyTexts[i])
            {
                ingQtyTexts[i].text = "";
            }
        }
        if (resultIcon)
        {
            resultIcon.sprite = null;
            resultIcon.color = new Color(1,1,1,0);
        }
        if (resultQtyText) resultQtyText.text = "";

        if (root) root.SetActive(true);
        else gameObject.SetActive(true);

        // (tuá»³ chá»n) cĂ³ thá»ƒ hiá»ƒn thá»‹ má»™t label â€œNo recipeâ€
        DevLog.Log($"RecipePanel: {reason}");
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
        else gameObject.SetActive(false);
    }

    private void ResolveRefs()
    {
        if (mainPageManager == null)
            mainPageManager = UIManager.Ins;

        if (inventoryUIManager == null && InventoryController.Instance != null)
            inventoryUIManager = InventoryController.Instance.InventoryUIManager;

        if (craftingController == null && InventoryController.Instance != null)
            craftingController = InventoryController.Instance.CraftingController;

        if (inventorySlider == null && InventoryController.Instance != null)
            inventorySlider = InventoryController.Instance.InventorySlider;
    }

    private void FlashMissingSlots(List<int> slots)
    {
        if (slots == null || slots.Count == 0)
            return;

        if (ingIcons == null || ingIcons.Length == 0)
            return;

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(FlashMissingSlotsRoutine(slots));
    }

    private IEnumerator FlashMissingSlotsRoutine(List<int> slots)
    {
        var originalColors = new Dictionary<int, Color>();

        foreach (var i in slots)
        {
            if (i >= 0 && i < ingIcons.Length && ingIcons[i] != null)
                originalColors[i] = ingIcons[i].color;
        }

        for (int t = 0; t < missingFlashCount; t++)
        {
            foreach (var i in slots)
            {
                if (i >= 0 && i < ingIcons.Length && ingIcons[i] != null)
                    ingIcons[i].color = Color.red;
            }

            yield return new WaitForSecondsRealtime(missingFlashInterval);

            foreach (var i in slots)
            {
                if (i >= 0 && i < ingIcons.Length && ingIcons[i] != null && originalColors.ContainsKey(i))
                    ingIcons[i].color = originalColors[i];
            }

            yield return new WaitForSecondsRealtime(missingFlashInterval);
        }
    }
}

