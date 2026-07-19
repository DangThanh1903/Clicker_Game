using UnityEngine;

public sealed class JournalFeatureGate : MonoBehaviour
{
    [SerializeField] private string featureId;
    [SerializeField] private GameObject[] toggleObjects;

    private void OnEnable()
    {
        JournalManager manager = JournalManager.GetOrCreate();
        if (manager != null)
            manager.StateChanged += HandleJournalChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (JournalManager.Ins != null)
            JournalManager.Ins.StateChanged -= HandleJournalChanged;
    }

    private void HandleJournalChanged()
    {
        Refresh();
    }

    private void Refresh()
    {
        bool unlocked = JournalManager.Ins != null && JournalManager.Ins.IsFeatureUnlocked(featureId);
        if (toggleObjects == null)
            return;

        for (int i = 0; i < toggleObjects.Length; i++)
        {
            if (toggleObjects[i] != null)
                toggleObjects[i].SetActive(unlocked);
        }
    }
}
