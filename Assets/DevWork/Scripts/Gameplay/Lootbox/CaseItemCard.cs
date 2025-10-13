using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CaseItemCard : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text amountText;   // new
    public Image background;

    public void Setup(Item item, int amount = 1)
    {
        if (item == null) return;

        icon.sprite = item.icon;
        icon.color = Vector4.one;
        nameText.text = item.GetColoredName();

        // Show "xN" only if > 1
        amountText.text = amount > 1 ? $"x{amount}" : string.Empty;

        if (background != null)
        {
            Color color;
            if (ColorUtility.TryParseHtmlString(RarityColors.GetColorHex(item.rarity), out color))
                background.color = color;
        }
    }
}
