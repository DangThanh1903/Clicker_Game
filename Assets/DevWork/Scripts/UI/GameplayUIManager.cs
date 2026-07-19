using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameplayUIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text clickNumberUI;
    [SerializeField] private TMP_Text clickPerTickUI;
    [SerializeField] private TMP_Text diamondUI;
    [SerializeField] private TMP_Text blockNameUI;
    [SerializeField] private TMP_Text blockHealthUI;
    [SerializeField] private TMP_Text runTimerUI;
    [SerializeField] private Image manaUI;
    [SerializeField] private Sprite manaSprite;
    [SerializeField] private Sprite staminaSprite;
    [SerializeField] private Sprite idleSprite;

    private GameplayHudStatsBinder statsBinder;
    private GameplayJournalHudBinder journalHudBinder;
    private GameplayHudTargetBinder targetBinder;
    private GameplayHudResourceBinder resourceBinder;

    private void Awake()
    {
        statsBinder = new GameplayHudStatsBinder(clickPerTickUI, diamondUI);
        journalHudBinder = new GameplayJournalHudBinder(clickNumberUI);
        targetBinder = new GameplayHudTargetBinder(blockNameUI, blockHealthUI);
        resourceBinder = new GameplayHudResourceBinder(runTimerUI, manaUI, manaSprite, staminaSprite, idleSprite);
    }

    private void OnEnable()
    {
        EnsureBinders();
        journalHudBinder.Bind();
        statsBinder.Bind();
        targetBinder.Bind();
        resourceBinder.RefreshImmediate();
    }

    private void OnDisable()
    {
        journalHudBinder?.Dispose();
        statsBinder?.Dispose();
        targetBinder?.Unbind();
    }

    private void Update()
    {
        EnsureBinders();
        journalHudBinder.Tick();
        statsBinder.Tick();
        targetBinder.Tick();
        resourceBinder.Tick();
    }

    private void EnsureBinders()
    {
        statsBinder ??= new GameplayHudStatsBinder(clickPerTickUI, diamondUI);
        journalHudBinder ??= new GameplayJournalHudBinder(clickNumberUI);
        targetBinder ??= new GameplayHudTargetBinder(blockNameUI, blockHealthUI);
        resourceBinder ??= new GameplayHudResourceBinder(runTimerUI, manaUI, manaSprite, staminaSprite, idleSprite);
    }
}
