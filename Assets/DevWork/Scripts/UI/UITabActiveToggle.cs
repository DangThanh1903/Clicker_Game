using UnityEngine;

public class UITabActiveToggle : MonoBehaviour
{
    [SerializeField] private UIManager uiManager;
    [SerializeField] private int activeTabIndex = 0;
    [SerializeField] private Behaviour[] enableWhenActive;

    private void OnEnable()
    {
        if (uiManager == null)
            uiManager = UIManager.Ins;

        if (uiManager != null)
        {
            uiManager.OnPageChanged += OnPageChanged;
            Apply(uiManager.CurrentIndex);
        }
        else
        {
            Apply(activeTabIndex);
        }
    }

    private void OnDisable()
    {
        if (uiManager != null)
            uiManager.OnPageChanged -= OnPageChanged;
    }

    private void OnPageChanged(int from, int to)
    {
        Apply(to);
    }

    private void Apply(int currentIndex)
    {
        bool active = currentIndex == activeTabIndex;
        if (enableWhenActive == null) return;

        foreach (var b in enableWhenActive)
        {
            if (b != null)
                b.enabled = active;
        }
    }
}
