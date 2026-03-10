using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecipeUnlockPopupView : PopupView
{
    [Header("UI")]
    [SerializeField] private TMP_Text unlockTitleText;
    [SerializeField] private TMP_Text recipeNameText;
    [SerializeField] private Image recipeIconImage;

    [Header("Texts")]
    [SerializeField] private string unlockTitle = "Unlocked";
    [SerializeField] private string fallbackRecipeName = "New Recipe";

    public void Bind(CraftNode node)
    {
        Item recipeItem = node != null ? node.GetPrimaryRecipeItem() : null;
        string recipeName = recipeItem != null && !string.IsNullOrWhiteSpace(recipeItem.itemName)
            ? recipeItem.itemName
            : (!string.IsNullOrWhiteSpace(node?.nodeName) ? node.nodeName : fallbackRecipeName);

        if (unlockTitleText != null)
            unlockTitleText.text = unlockTitle;

        if (recipeNameText != null)
            recipeNameText.text = recipeName;

        if (recipeIconImage != null)
        {
            bool hasIcon = recipeItem != null && recipeItem.icon != null;
            recipeIconImage.enabled = hasIcon;
            recipeIconImage.sprite = hasIcon ? recipeItem.icon : null;
            recipeIconImage.color = hasIcon ? Color.white : new Color(1f, 1f, 1f, 0f);
        }
    }
}
