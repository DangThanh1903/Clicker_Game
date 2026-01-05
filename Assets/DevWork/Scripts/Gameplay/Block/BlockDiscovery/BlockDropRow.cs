using TMPro;
using UnityEngine;

namespace Game.UI.Dictionary
{
    public class BlockDropRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        public void Bind(string text) => label.text = text;
    }
}
