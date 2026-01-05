using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Dictionary
{
    public class BlockDictionaryListItem : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button button;

        public void Bind(string blockName, bool discovered, Action onClick)
        {
            label.text = discovered ? blockName : "??? (Not discovered)";
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick?.Invoke());
        }
    }
}
