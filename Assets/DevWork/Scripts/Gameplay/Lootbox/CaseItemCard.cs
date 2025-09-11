using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CaseItemCard : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public TMP_Text amountText;   // new

    public void Setup(Item item, int amount = 1)
    {
        if (item == null) return;

        icon.sprite = item.icon;
        nameText.text = item.GetColoredName();

        // Show "xN" only if > 1
        amountText.text = amount > 1 ? $"x{amount}" : string.Empty;
    }
}
