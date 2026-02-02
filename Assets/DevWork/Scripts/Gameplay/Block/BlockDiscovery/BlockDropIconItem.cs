using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Dictionary
{
    public class BlockDropIconItem : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Color revealedColor = Color.white;
        [SerializeField] private Color hiddenColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Sprite fallbackSprite;

        public void Bind(Sprite sprite, bool revealed)
        {
            if (icon == null) return;

            var useSprite = sprite != null ? sprite : fallbackSprite;
            icon.sprite = useSprite;
            icon.enabled = useSprite != null;
            icon.color = revealed ? revealedColor : hiddenColor;
        }
    }
}
