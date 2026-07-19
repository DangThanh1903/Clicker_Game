using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;

public class PopupButtons : MonoBehaviour
{
    [Header("Assign in Inspector")]
    [SerializeField] private Button button;
    [SerializeField] private PopupView popupPrefab;
    [SerializeField] private string requiredFeatureId;
    [SerializeField] private bool hideWhenLocked = true;
    [SerializeField] private CanvasGroup gateCanvasGroup;

    private JournalManager boundJournalManager;
    private LayoutElement gateLayoutElement;

    void Awake()
    {
        if (!button) button = GetComponent<Button>();
        gateLayoutElement = GetComponent<LayoutElement>();
        if (hideWhenLocked && gateLayoutElement == null)
            gateLayoutElement = gameObject.AddComponent<LayoutElement>();
        if (gateCanvasGroup == null)
            gateCanvasGroup = GetComponent<CanvasGroup>();
        if (gateCanvasGroup == null)
            gateCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (button)
            button.onClick.AddListener(OpenPopup);

        JournalManager.GetOrCreate();
        TryBindJournalManager();
        RefreshGate();
    }

    private void OnEnable()
    {
        TryBindJournalManager();
        RefreshGate();
    }

    private void OnDisable()
    {
        if (boundJournalManager != null)
            boundJournalManager.StateChanged -= HandleJournalStateChanged;

        boundJournalManager = null;
    }

    private void OpenPopup()
    {
        if (!IsUnlocked())
            return;

        _ = OpenPopupAsync();
    }

    private async Task OpenPopupAsync()
    {
        if (PopupController.Instance && popupPrefab)
        {
            try
            {
                await PopupController.Instance.Show(popupPrefab);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PopupButton] Failed to open popup: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning("[PopupButton] Missing PopupController or Popup Prefab.");
        }
    }

    private void TryBindJournalManager()
    {
        if (boundJournalManager == JournalManager.Ins && boundJournalManager != null)
            return;

        if (boundJournalManager != null)
            boundJournalManager.StateChanged -= HandleJournalStateChanged;

        boundJournalManager = JournalManager.Ins;
        if (boundJournalManager != null)
            boundJournalManager.StateChanged += HandleJournalStateChanged;
    }

    private void HandleJournalStateChanged()
    {
        RefreshGate();
    }

    private void RefreshGate()
    {
        bool unlocked = IsUnlocked();

        if (button != null)
            button.interactable = unlocked;

        if (gateCanvasGroup == null)
            return;

        if (hideWhenLocked)
        {
            gateCanvasGroup.alpha = unlocked ? 1f : 0f;
            gateCanvasGroup.interactable = unlocked;
            gateCanvasGroup.blocksRaycasts = unlocked;
            if (gateLayoutElement != null)
                gateLayoutElement.ignoreLayout = !unlocked;
            return;
        }

        gateCanvasGroup.alpha = 1f;
        gateCanvasGroup.interactable = unlocked;
        gateCanvasGroup.blocksRaycasts = unlocked;
        if (gateLayoutElement != null)
            gateLayoutElement.ignoreLayout = false;
    }

    private bool IsUnlocked()
    {
        return string.IsNullOrWhiteSpace(requiredFeatureId) ||
               (JournalManager.Ins != null && JournalManager.Ins.IsFeatureUnlocked(requiredFeatureId));
    }
}
