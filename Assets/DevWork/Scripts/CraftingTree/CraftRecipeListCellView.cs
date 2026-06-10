using System;
using System.Text;
using EnhancedUI.EnhancedScroller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftRecipeListCellView : EnhancedScrollerCellView
{
    [Header("Refs")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statText;
    [SerializeField] private TMP_Text ingredientText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Button craftButton;
    [SerializeField] private TMP_Text craftButtonText;
    [SerializeField] private Button selectButton;
    [SerializeField] private CanvasGroup contentGroup;

    private CraftRecipeListEntry entry;
    private Action<CraftRecipeListEntry> onCraftClicked;
    private Action<CraftRecipeListEntry> onSelected;

    private void Awake()
    {
        if (selectButton == null)
            selectButton = GetComponent<Button>();
        if (contentGroup == null)
            contentGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    public void SetData(
        CraftRecipeListEntry data,
        Action<CraftRecipeListEntry> craftClicked,
        Action<CraftRecipeListEntry> selected)
    {
        entry = data;
        onCraftClicked = craftClicked;
        onSelected = selected;

        Item resultItem = entry != null ? entry.resultItem : null;
        int resultQuantity = entry != null ? entry.resultQuantity : 0;

        if (icon != null)
        {
            icon.sprite = resultItem != null ? resultItem.icon : null;
            icon.color = resultItem != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        if (titleText != null)
        {
            string itemName = resultItem != null ? resultItem.itemName : "Unknown";
            titleText.text = resultQuantity > 1 ? $"{itemName} x{resultQuantity}" : itemName;
        }

        if (statText != null)
            statText.text = entry != null && !string.IsNullOrWhiteSpace(entry.statLine) ? entry.statLine : string.Empty;

        if (ingredientText != null)
            ingredientText.text = BuildIngredientText(entry);

        bool canCraft = entry != null && entry.canCraft && entry.canAddResult && !entry.IsLocked;
        if (craftButton != null)
            craftButton.interactable = canCraft;
        if (craftButtonText != null)
            craftButtonText.text = canCraft ? "CRAFT" : "MISSING";
        if (stateText != null)
            stateText.text = ResolveStateText(entry);
        if (contentGroup != null)
            contentGroup.alpha = entry != null && entry.IsLocked ? 0.45f : 1f;

        BindButtons();
    }

    private void BindButtons()
    {
        if (craftButton != null)
        {
            craftButton.onClick.RemoveListener(HandleCraftClicked);
            craftButton.onClick.AddListener(HandleCraftClicked);
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleSelected);
            selectButton.onClick.AddListener(HandleSelected);
        }
    }

    private void UnbindButtons()
    {
        if (craftButton != null)
            craftButton.onClick.RemoveListener(HandleCraftClicked);
        if (selectButton != null)
            selectButton.onClick.RemoveListener(HandleSelected);
    }

    private void HandleCraftClicked()
    {
        if (entry != null)
            onCraftClicked?.Invoke(entry);
    }

    private void HandleSelected()
    {
        if (entry != null)
            onSelected?.Invoke(entry);
    }

    private static string BuildIngredientText(CraftRecipeListEntry entry)
    {
        if (entry == null || entry.ingredients == null || entry.ingredients.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < entry.ingredients.Count; i++)
        {
            RecipeIngredientStatus ingredient = entry.ingredients[i];
            if (ingredient.item == null)
                continue;

            string mark = ingredient.HasEnough ? "\u2713" : "X";
            sb.Append(ingredient.item.itemName);
            sb.Append(" ");
            sb.Append(ingredient.available);
            sb.Append("/");
            sb.Append(ingredient.required);
            sb.Append(" ");
            sb.Append(mark);

            if (i < entry.ingredients.Count - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ResolveStateText(CraftRecipeListEntry entry)
    {
        if (entry == null)
            return string.Empty;
        if (entry.IsLocked)
            return "LOCKED";
        if (!entry.canAddResult)
            return "BAG FULL";
        if (entry.canCraft)
            return "READY";
        return "MISSING";
    }
}
