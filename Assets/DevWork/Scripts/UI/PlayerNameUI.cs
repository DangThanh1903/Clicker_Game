using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerNameUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private bool autoSaveOnEndEdit = true;

    private const int MaxNameLength = 16;
    private bool initialized;

    private void Awake()
    {
        if (nameInput == null)
            nameInput = GetComponentInChildren<TMP_InputField>();
    }

    private void Start()
    {
        if (nameInput != null)
        {
            nameInput.characterLimit = MaxNameLength;
            nameInput.onValueChanged.AddListener(OnNameChanged);
            if (autoSaveOnEndEdit)
                nameInput.onEndEdit.AddListener(OnEndEdit);
        }

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);

        StartCoroutine(InitFromSaver());
        UpdateInteractable();
    }

    private void OnDestroy()
    {
        if (nameInput != null)
        {
            nameInput.onValueChanged.RemoveListener(OnNameChanged);
            if (autoSaveOnEndEdit)
                nameInput.onEndEdit.RemoveListener(OnEndEdit);
        }

        if (saveButton != null)
            saveButton.onClick.RemoveListener(OnSaveClicked);
    }

    private IEnumerator InitFromSaver()
    {
        while (DataSaver.Ins == null)
            yield return null;

        if (!initialized)
        {
            initialized = true;
            string current = DataSaver.Ins.DisplayName;
            if (nameInput != null && !string.IsNullOrWhiteSpace(current))
                nameInput.SetTextWithoutNotify(current);
            UpdateInteractable();
        }
    }

    private void OnNameChanged(string _)
    {
        if (statusText != null)
            statusText.text = "";
        UpdateInteractable();
    }

    private void OnEndEdit(string _)
    {
        if (autoSaveOnEndEdit)
            SaveName();
    }

    private void OnSaveClicked()
    {
        SaveName();
    }

    private void SaveName()
    {
        if (DataSaver.Ins == null || nameInput == null)
            return;

        string raw = nameInput.text;
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (statusText != null)
                statusText.text = "Name required";
            UpdateInteractable();
            return;
        }

        DataSaver.Ins.SetDisplayName(raw);
        if (statusText != null)
            statusText.text = "Saved";
        UpdateInteractable();
    }

    private void UpdateInteractable()
    {
        if (saveButton == null || nameInput == null)
            return;

        bool hasName = !string.IsNullOrWhiteSpace(nameInput.text);
        saveButton.interactable = hasName;
    }
}
