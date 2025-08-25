// CraftRecipePanel.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

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

    public void ShowForItem(Item targetItem)
    {
        if (recipeDB == null || targetItem == null)
        {
            ShowEmpty("No recipe DB / item");
            return;
        }

        var list = recipeDB.GetRecipesByResultItem(targetItem);
        if (list.Count == 0)
        {
            ShowEmpty("No recipe found");
            return;
        }

        // lấy recipe đầu tiên (nếu có nhiều bạn có thể làm UI để chuyển tab)
        ShowRecipe(list[0]);
    }

    public void ShowRecipe(RecipeDatabase.Recipe recipe)
    {
        if (recipe == null)
        {
            ShowEmpty("Null recipe");
            return;
        }

        // Fill ingredients (đã normalize 4 slot trong DB; nếu không, tự bảo vệ null)
        for (int i = 0; i < 4; i++)
        {
            var it = (i < recipe.ingredients.Count) ? recipe.ingredients[i] : null;
            var data = it?.itemData;
            var qty = it?.quantity?.Value ?? 0;

            if (ingIcons != null && i < ingIcons.Length && ingIcons[i])
            {
                ingIcons[i].sprite = data ? data.icon : null;
                ingIcons[i].color = data ? Color.white : new Color(1,1,1,0); // ẩn nếu null
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

    public void ShowEmpty(string reason = "")
    {
        // Xoá/ẩn sạch slot
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

        // (tuỳ chọn) có thể hiển thị một label “No recipe”
        Debug.Log($"RecipePanel: {reason}");
    }

    public void Hide()
    {
        if (root) root.SetActive(false);
        else gameObject.SetActive(false);
    }
}
