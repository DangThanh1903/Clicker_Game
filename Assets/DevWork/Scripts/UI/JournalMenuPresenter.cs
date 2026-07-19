using System.Collections;
using System.Collections.Generic;
using EnhancedUI.EnhancedScroller;
using TMPro;
using UnityEngine;

public class JournalMenuPresenter : MonoBehaviour, IEnhancedScrollerDelegate
{
    [Header("Scroller")]
    [SerializeField] protected EnhancedScroller scroller;
    [SerializeField] protected CraftRecipeListCellView cellViewPrefab;
    [SerializeField, Min(32f)] protected float cellSize = 210f;
    [SerializeField] protected TMP_Text emptyText;

    protected readonly List<JournalStepViewModel> entries = new();

    private JournalManager boundJournalManager;
    private Coroutine bindCo;
    private Coroutine reloadCo;

    protected virtual void Awake()
    {
        if (scroller == null)
            scroller = GetComponentInChildren<EnhancedScroller>(true);
        if (cellViewPrefab == null)
            cellViewPrefab = GetComponentInChildren<CraftRecipeListCellView>(true);
    }

    protected virtual void OnEnable()
    {
        JournalManager.GetOrCreate();
        TryBindJournalManager();
        if (boundJournalManager == null && bindCo == null)
            bindCo = StartCoroutine(BindNextFrame());

        if (scroller != null)
            scroller.Delegate = this;

        Refresh();
    }

    protected virtual void OnDisable()
    {
        if (bindCo != null)
        {
            StopCoroutine(bindCo);
            bindCo = null;
        }

        if (reloadCo != null)
        {
            StopCoroutine(reloadCo);
            reloadCo = null;
        }

        UnbindJournalManager();
    }

    public virtual void Refresh()
    {
        entries.Clear();
        TryBindJournalManager();
        if (boundJournalManager != null && boundJournalManager.IsReady)
            entries.AddRange(boundJournalManager.GetCurrentMenuSteps());

        if (emptyText != null)
            emptyText.gameObject.SetActive(entries.Count == 0);

        if (scroller != null && cellViewPrefab != null)
            RequestReload();
    }

    public int GetNumberOfCells(EnhancedScroller scroller)
    {
        return entries.Count;
    }

    public float GetCellViewSize(EnhancedScroller scroller, int dataIndex)
    {
        return cellSize;
    }

    public EnhancedScrollerCellView GetCellView(EnhancedScroller scroller, int dataIndex, int cellIndex)
    {
        if (cellViewPrefab == null)
            return null;

        CraftRecipeListCellView cellView = scroller.GetCellView(cellViewPrefab) as CraftRecipeListCellView;
        if (cellView == null)
            return null;

        cellView.gameObject.SetActive(true);
        cellView.name = $"Journal Step {dataIndex}";
        cellView.SetJournalData(entries[dataIndex]);
        return cellView;
    }

    private IEnumerator BindNextFrame()
    {
        yield return null;
        bindCo = null;
        TryBindJournalManager();
        Refresh();
    }

    private void TryBindJournalManager()
    {
        if (boundJournalManager == JournalManager.Ins && boundJournalManager != null)
            return;

        UnbindJournalManager();
        boundJournalManager = JournalManager.Ins;
        if (boundJournalManager == null)
            return;

        boundJournalManager.MenuChanged += HandleMenuChanged;
    }

    private void UnbindJournalManager()
    {
        if (boundJournalManager != null)
            boundJournalManager.MenuChanged -= HandleMenuChanged;

        boundJournalManager = null;
    }

    private void HandleMenuChanged(IReadOnlyList<JournalStepViewModel> _)
    {
        Refresh();
    }

    private void RequestReload()
    {
        if (!isActiveAndEnabled)
            return;

        if (reloadCo != null)
            StopCoroutine(reloadCo);

        reloadCo = StartCoroutine(ReloadNextFrame());
    }

    private IEnumerator ReloadNextFrame()
    {
        yield return null;
        reloadCo = null;

        if (scroller == null || cellViewPrefab == null || !gameObject.activeInHierarchy)
            yield break;

        Canvas.ForceUpdateCanvases();
        scroller.ReloadData();
    }
}
