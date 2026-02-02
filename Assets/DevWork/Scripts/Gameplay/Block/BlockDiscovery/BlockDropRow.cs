using TMPro;
using UnityEngine;

namespace Game.UI.Dictionary
{
    public class BlockDropRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Color revealedColor = Color.white;
        [SerializeField] private Color hiddenColor = new Color(0f, 0f, 0f, 0.85f);

        public void Bind(string text, bool revealed = true)
        {
            if (label == null) return;
            label.text = text;
            label.color = revealed ? revealedColor : hiddenColor;
        }
    }
}
