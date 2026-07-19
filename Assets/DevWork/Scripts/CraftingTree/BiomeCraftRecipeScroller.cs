using UnityEngine;

public class BiomeCraftRecipeScroller : JournalMenuPresenter
{
    [Header("Legacy Tree Visual")]
    [Tooltip("Optional visual-only tree container to hide while this list is used. Do not assign an object that owns CraftNodeManager.")]
    [SerializeField] private GameObject legacyTreeVisualRoot;
    [SerializeField] private bool hideLegacyTreeVisual = true;

    protected override void OnEnable()
    {
        if (hideLegacyTreeVisual && legacyTreeVisualRoot != null)
            legacyTreeVisualRoot.SetActive(false);

        base.OnEnable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (hideLegacyTreeVisual && legacyTreeVisualRoot != null)
            legacyTreeVisualRoot.SetActive(true);
    }
}
