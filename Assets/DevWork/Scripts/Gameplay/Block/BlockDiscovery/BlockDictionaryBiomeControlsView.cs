using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Dictionary
{
    [DisallowMultipleComponent]
    public sealed class BlockDictionaryBiomeControlsView : MonoBehaviour
    {
        [SerializeField] private TMP_Text currentBiomeText;
        [SerializeField] private TMP_Text selectedBiomeText;
        [SerializeField] private Button previousBiomeButton;
        [SerializeField] private Button nextBiomeButton;
        [SerializeField] private Button moveToBiomeButton;
        [SerializeField] private TMP_Text moveToBiomeLabel;

        public TMP_Text CurrentBiomeText => currentBiomeText;
        public TMP_Text SelectedBiomeText => selectedBiomeText;
        public Button PreviousBiomeButton => previousBiomeButton;
        public Button NextBiomeButton => nextBiomeButton;
        public Button MoveToBiomeButton => moveToBiomeButton;
        public TMP_Text MoveToBiomeLabel => moveToBiomeLabel;

        public void BindReferences(
            TMP_Text currentText,
            TMP_Text selectedText,
            Button previousButton,
            Button nextButton,
            Button moveButton,
            TMP_Text moveLabel)
        {
            currentBiomeText = currentText;
            selectedBiomeText = selectedText;
            previousBiomeButton = previousButton;
            nextBiomeButton = nextButton;
            moveToBiomeButton = moveButton;
            moveToBiomeLabel = moveLabel;
            EnsureReferences();
        }

        public void EnsureReferences()
        {
            if (moveToBiomeLabel == null && moveToBiomeButton != null)
                moveToBiomeLabel = moveToBiomeButton.GetComponentInChildren<TMP_Text>(true);
        }

        public bool HasAnyReference()
        {
            return currentBiomeText != null ||
                   selectedBiomeText != null ||
                   previousBiomeButton != null ||
                   nextBiomeButton != null ||
                   moveToBiomeButton != null ||
                   moveToBiomeLabel != null;
        }

        public void Apply(string currentBiomeName, string viewedBiomeName, bool hasChoices, bool canMove, string moveButtonText)
        {
            if (currentBiomeText != null)
                currentBiomeText.text = currentBiomeName;

            if (selectedBiomeText != null)
                selectedBiomeText.text = viewedBiomeName;

            if (previousBiomeButton != null)
                previousBiomeButton.interactable = hasChoices;

            if (nextBiomeButton != null)
                nextBiomeButton.interactable = hasChoices;

            if (moveToBiomeButton != null)
                moveToBiomeButton.interactable = canMove;

            if (moveToBiomeLabel != null)
                moveToBiomeLabel.text = moveButtonText;
        }
    }
}
