using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class PopupView : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] float openDuration = 0.22f;
    [SerializeField] float closeDuration = 0.18f;
    [SerializeField] Vector3 fromScale = new Vector3(0.92f, 0.92f, 1f);
    [SerializeField] Ease openEase = Ease.OutBack;
    [SerializeField] Ease closeEase = Ease.InSine;
    [SerializeField] bool timeScaleIndependent = true;

    CanvasGroup cg;
    RectTransform rt;
    Tween currentTween;

    void Awake()
    {
        Init();
    }

    // 🔁 Called on each reuse from the pool
    void OnEnable()
    {
        Init();
        KillTween();
        rt.localScale = fromScale;
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    public async Task OpenAsync()
    {
        Init();
        KillTween();

        cg.blocksRaycasts = true;
        cg.interactable = true;

        var seq = DOTween.Sequence().SetUpdate(timeScaleIndependent);
        seq.Join(rt.DOScale(1f, openDuration).SetEase(openEase));
        seq.Join(cg.DOFade(1f, openDuration));
        currentTween = seq;
        await WaitForTweenOrDisable(seq);
        currentTween = null;
    }

    public async Task CloseAsync()
    {
        Init();
        KillTween();
        cg.interactable = false;

        var seq = DOTween.Sequence().SetUpdate(timeScaleIndependent);
        seq.Join(rt.DOScale(fromScale, closeDuration).SetEase(closeEase));
        seq.Join(cg.DOFade(0f, closeDuration));
        currentTween = seq;
        await WaitForTweenOrDisable(seq);
        currentTween = null;

        // DO NOT deactivate here — controller will Despawn after animation
        cg.blocksRaycasts = false;
    }

    void OnDisable() => KillTween();

    void KillTween()
    {
        if (currentTween != null && currentTween.IsActive())
            currentTween.Kill(false);
        currentTween = null;
    }

    void Init()
    {
        if (cg == null)
            cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        if (rt == null)
            rt = transform as RectTransform;
    }

    async Task WaitForTweenOrDisable(Tween tween)
    {
        if (tween == null) return;
        while (gameObject != null && gameObject.activeInHierarchy && tween.IsActive() && tween.IsPlaying())
            await Task.Yield();
    }
}
